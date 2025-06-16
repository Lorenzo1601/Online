using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Online.Data;
using Online.Models;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Online
{
    public class OpcUaService : IDisposable
    {
        private Session? _session;
        private ApplicationConfiguration? _config;
        private Subscription? _subscription;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OpcUaService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        // --- MODIFIED: State management separated for clarity and function ---
        private readonly ConcurrentDictionary<string, MonitoredItem> _monitoredItems = new();
        private readonly ConcurrentDictionary<string, bool> _dbLoggingEnabledNodes = new();
        private readonly ConcurrentDictionary<string, bool> _telegramAlarmingEnabledNodes = new();
        private readonly ConcurrentDictionary<string, StatusCode> _lastKnownQualities = new();
        // --- END MODIFICATION ---

        public bool IsConnected => _session?.Connected ?? false;
        public string? ConnectedEndpointUrl => _session?.Endpoint.EndpointUrl;

        public OpcUaService(
            IServiceScopeFactory scopeFactory,
            ILogger<OpcUaService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            InitializeClient().Wait();
        }

        private async Task InitializeClient()
        {
            _config = new ApplicationConfiguration()
            {
                ApplicationName = "OpcUaBrowserClient",
                ApplicationUri = Utils.Format(@"urn:{0}:OpcUaBrowserClient", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Client,
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = "OpcUaBrowserClient" },
                    TrustedIssuerCertificates = new CertificateTrustList { StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    TrustedPeerCertificates = new CertificateTrustList { StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateStoreIdentifier { StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                CertificateValidator = new CertificateValidator()
            };

            _config.CertificateValidator.CertificateValidation += (sender, e) => { if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted) e.Accept = true; };
            await _config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance(_config);
            await application.CheckApplicationInstanceCertificate(false, 2048);
        }

        // --- ADDED: Methods to start/stop DB logging and Telegram alarming ---

        public Task StartDbLogging(string nodeId, string displayName)
        {
            _logger.LogInformation("Richiesta di avvio DB logging per il nodo: {nodeId}", nodeId);
            _dbLoggingEnabledNodes.TryAdd(nodeId, true);
            return EnsureMonitoredAsync(nodeId, displayName);
        }

        public Task StopDbLogging(string nodeId)
        {
            _logger.LogInformation("Richiesta di stop DB logging per il nodo: {nodeId}", nodeId);
            _dbLoggingEnabledNodes.TryRemove(nodeId, out _);
            return CleanupMonitoredItemIfNeeded(nodeId);
        }

        public Task StartTelegramAlarming(string nodeId, string displayName)
        {
            _logger.LogInformation("Richiesta di avvio allarmi Telegram per il nodo: {nodeId}", nodeId);
            _telegramAlarmingEnabledNodes.TryAdd(nodeId, true);
            return EnsureMonitoredAsync(nodeId, displayName);
        }

        public Task StopTelegramAlarming(string nodeId)
        {
            _logger.LogInformation("Richiesta di stop allarmi Telegram per il nodo: {nodeId}", nodeId);
            _telegramAlarmingEnabledNodes.TryRemove(nodeId, out _);
            _lastKnownQualities.TryRemove(nodeId, out _); // Clean up quality tracking
            return CleanupMonitoredItemIfNeeded(nodeId);
        }

        // --- END ADDED ---

        private Task EnsureMonitoredAsync(string nodeId, string displayName)
        {
            if (_monitoredItems.ContainsKey(nodeId))
            {
                _logger.LogInformation("Il nodo {nodeId} è già monitorato.", nodeId);
                return Task.CompletedTask;
            }

            if (!IsConnected || _subscription == null) throw new InvalidOperationException("Sessione non attiva.");

            var item = new MonitoredItem(_subscription.DefaultItem)
            {
                DisplayName = displayName,
                StartNodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                MonitoringMode = MonitoringMode.Reporting,
                SamplingInterval = 1000,
                QueueSize = 10,
                DiscardOldest = true
            };

            item.Notification += MonitoredItem_Notification;
            _subscription.AddItem(item);
            _subscription.ApplyChanges();
            _monitoredItems.TryAdd(nodeId, item);
            _logger.LogInformation("Avviato monitoraggio per il nodo: {nodeId}", nodeId);
            return Task.CompletedTask;
        }

        private Task CleanupMonitoredItemIfNeeded(string nodeId)
        {
            if (!_dbLoggingEnabledNodes.ContainsKey(nodeId) && !_telegramAlarmingEnabledNodes.ContainsKey(nodeId))
            {
                _logger.LogInformation("Il nodo {nodeId} non è più monitorato per nessuna funzione. Rimozione sottoscrizione.", nodeId);
                if (_monitoredItems.TryRemove(nodeId, out var itemToRemove))
                {
                    _subscription?.RemoveItem(itemToRemove);
                    _subscription?.ApplyChanges();
                }
            }
            else
            {
                _logger.LogInformation("Il nodo {nodeId} è ancora monitorato per un'altra funzione.", nodeId);
            }
            return Task.CompletedTask;
        }


        public async Task ConnectAsync(string endpointUrl)
        {
            if (_session != null && _session.Connected)
            {
                if (_session.Endpoint.EndpointUrl == endpointUrl) return;
                await DisconnectAsync();
            }

            var endpointDescription = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
            var endpointConfiguration = EndpointConfiguration.Create(_config);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

            _session = await Session.Create(_config, configuredEndpoint, true, _config.ApplicationName, 60000, new UserIdentity(), null);

            _subscription = new Subscription(_session.DefaultSubscription) { PublishingInterval = 1000 };
            _session.AddSubscription(_subscription);
            _subscription.Create();
        }

        public Task DisconnectAsync()
        {
            if (_session != null)
            {
                var sessionToClose = _session;
                _session = null;
                // Clear all states on disconnect
                _monitoredItems.Clear();
                _dbLoggingEnabledNodes.Clear();
                _telegramAlarmingEnabledNodes.Clear();
                _lastKnownQualities.Clear();
                return sessionToClose.CloseAsync();
            }
            return Task.CompletedTask;
        }

        private void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            if (e.NotificationValue is not MonitoredItemNotification notification || notification.Value == null) return;

            var value = notification.Value;
            var nodeId = monitoredItem.StartNodeId.ToString();
            var currentQuality = value.StatusCode;

            _logger.LogInformation("[DEBUG] Notifica ricevuta per '{displayName}'. Valore: {value}, Qualità: {quality}", monitoredItem.DisplayName, value.Value, currentQuality);

            // --- DECOUPLED LOGIC: Check for each function independently ---

            // 1. Database Logging
            if (_dbLoggingEnabledNodes.ContainsKey(nodeId))
            {
                _logger.LogInformation("[DB LOG] Il nodo {nodeId} è abilitato per il DB logging. Salvataggio...", nodeId);
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var logEntry = new MacchinaOpcUaLog
                    {
                        Nome = monitoredItem.DisplayName,
                        Nodo = nodeId,
                        Valore = value.Value?.ToString(),
                        Qualita = StatusCode.LookupSymbolicId(value.StatusCode.Code),
                        Timestamp = value.SourceTimestamp.ToLocalTime()
                    };
                    dbContext.MacchineOpcUaLog.Add(logEntry);
                    dbContext.SaveChanges();
                }
            }

            // 2. Telegram Alarming
            if (_telegramAlarmingEnabledNodes.ContainsKey(nodeId))
            {
                _logger.LogInformation("[TELEGRAM ALARM] Il nodo {nodeId} è abilitato per gli allarmi Telegram. Controllo qualità...", nodeId);
                _lastKnownQualities.TryGetValue(nodeId, out var lastQuality);

                if (lastQuality != null && currentQuality != lastQuality)
                {
                    _logger.LogInformation("[TELEGRAM ALARM] Qualità cambiata per {nodeId}! Da {last} a {current}. Invio notifica.", nodeId, lastQuality, currentQuality);
                    var message = $"⚠️ *Cambio Qualità Tag OPC UA* ⚠️\n\n" +
                                  $"*Tag*: `{monitoredItem.DisplayName}`\n" +
                                  $"*Nodo*: `{nodeId}`\n" +
                                  $"*Qualità Precedente*: `{Opc.Ua.StatusCodes.GetBrowseName(lastQuality.Code)}`\n" +
                                  $"*Nuova Qualità*: `{Opc.Ua.StatusCodes.GetBrowseName(currentQuality.Code)}`";
                    _ = SendTelegramNotificationAsync(message);
                }
                else if (lastQuality == null)
                {
                    _logger.LogInformation("[TELEGRAM ALARM] Prima lettura qualità per {nodeId}. Memorizzo per il prossimo controllo.", nodeId);
                }

                _lastKnownQualities[nodeId] = currentQuality;
            }
        }

        // --- NEW: Returns the state of both monitoring types ---
        public Task<object> GetMonitoringStatus()
        {
            return Task.FromResult<object>(new
            {
                DbLoggingNodes = _dbLoggingEnabledNodes.Keys.ToList(),
                TelegramAlarmingNodes = _telegramAlarmingEnabledNodes.Keys.ToList()
            });
        }

        public Task<List<string>> GetLoggedNodeIds()
        {
            // This is kept for backwards compatibility but GetMonitoringStatus is preferred
            return Task.FromResult(_dbLoggingEnabledNodes.Keys.ToList());
        }

        private async Task SendTelegramNotificationAsync(string message)
        {
            _logger.LogInformation("[DEBUG] Tentativo di invio notifica Telegram.");
            var botToken = _configuration["Telegram:BotToken"];
            var chatId = _configuration["Telegram:ChatId"];

            if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId) || botToken == "your_bot_token")
            {
                _logger.LogWarning("Telegram BotToken o ChatId non configurato. Impossibile inviare la notifica.");
                return;
            }

            var httpClient = _httpClientFactory.CreateClient("TelegramNotifier");
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}&parse_mode=Markdown";
            _logger.LogInformation("[DEBUG] Chiamata URL Telegram: {url}", url.Replace(botToken, "[REDACTED]"));

            try
            {
                var response = await httpClient.PostAsync(url, null);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Errore durante l'invio del messaggio Telegram: {errorContent}");
                }
                else
                {
                    _logger.LogInformation("Notifica Telegram inviata con successo.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eccezione durante l'invio del messaggio Telegram.");
            }
        }

        // Other methods (BrowseAsync, ReadValuesAsync, CreateNodeViewModel, Dispose) remain unchanged
        #region Unchanged Methods
        public async Task<List<EndpointDescription>> DiscoverEndpointsAsync(string discoveryUrl)
        {
            var discoveryClient = DiscoveryClient.Create(_config, new Uri(discoveryUrl));
            EndpointDescriptionCollection endpoints = await discoveryClient.GetEndpointsAsync(null);
            return endpoints.ToList();
        }

        public Task<List<OpcNodeViewModel>> BrowseAsync(string? nodeId)
        {
            if (!IsConnected) throw new InvalidOperationException("Sessione non attiva.");

            var nodeViewModels = new List<OpcNodeViewModel>();
            NodeId nodeToBrowse = nodeId == null ? ObjectIds.ObjectsFolder : new NodeId(nodeId);
            var browseDescription = new BrowseDescription { NodeId = nodeToBrowse, BrowseDirection = BrowseDirection.Forward, ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences, IncludeSubtypes = true, NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable | NodeClass.Method), ResultMask = (uint)BrowseResultMask.All };
            _session.Browse(null, null, 0, new BrowseDescriptionCollection { browseDescription }, out var results, out _);

            if (results != null && results.Count > 0)
            {
                foreach (var rd in results[0].References)
                {
                    nodeViewModels.Add(CreateNodeViewModel(_session, rd));
                }
            }
            return Task.FromResult(nodeViewModels);
        }

        public Task<List<OpcNodeViewModel>> ReadValuesAsync(List<string> nodeIds)
        {
            if (!IsConnected || nodeIds == null || !nodeIds.Any())
            {
                return Task.FromResult(new List<OpcNodeViewModel>());
            }

            var updatedValues = new List<OpcNodeViewModel>();
            var nodesToRead = new ReadValueIdCollection(nodeIds.Select(id => new ReadValueId { NodeId = new NodeId(id), AttributeId = Attributes.Value }));
            _session.Read(null, 0, TimestampsToReturn.Neither, nodesToRead, out DataValueCollection values, out _);

            for (int i = 0; i < values.Count; i++)
            {
                updatedValues.Add(new OpcNodeViewModel
                {
                    NodeId = nodeIds[i],
                    Value = values[i].Value?.ToString() ?? "null",
                    Status = StatusCode.LookupSymbolicId(values[i].StatusCode.Code)
                });
            }
            return Task.FromResult(updatedValues);
        }

        private OpcNodeViewModel CreateNodeViewModel(Session session, ReferenceDescription rd)
        {
            NodeId currentNodeId = (NodeId)rd.NodeId;
            var nodeModel = new OpcNodeViewModel { NodeId = currentNodeId.ToString(), DisplayName = rd.DisplayName.Text, NodeClass = rd.NodeClass.ToString() };
            try
            {
                var childDesc = new BrowseDescription() { NodeId = currentNodeId, BrowseDirection = BrowseDirection.Forward, ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences, IncludeSubtypes = true, NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable), ResultMask = (uint)BrowseResultMask.All };
                session.Browse(null, null, 1, new BrowseDescriptionCollection { childDesc }, out var childResults, out _);
                nodeModel.HasChildren = childResults.Count > 0 && childResults.Any(cr => cr.References.Count > 0);
            }
            catch { nodeModel.HasChildren = false; }
            if (rd.NodeClass == NodeClass.Variable)
            {
                try
                {
                    DataValue dataValue = session.ReadValue(currentNodeId);
                    nodeModel.Value = dataValue.Value?.ToString() ?? "null";
                    nodeModel.Status = StatusCode.LookupSymbolicId(dataValue.StatusCode.Code);
                }
                catch { nodeModel.Value = "N/D"; nodeModel.Status = "ErroreLettura"; }
            }
            return nodeModel;
        }

        public void Dispose() { DisconnectAsync().Wait(); }
        #endregion
    }
}
