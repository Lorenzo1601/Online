using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using online.Models;
using Online.Data;
using Online.Models;
using Online.Services;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Online
{
    public class OpcUaMultiServerService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OpcUaMultiServerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationConfiguration _clientConfig;
        private readonly ConcurrentDictionary<string, OpcServerInstance> _serverInstances = new();
        private readonly MonitoringConfigService _monitoringConfigService;

        public OpcUaMultiServerService(
            IServiceScopeFactory scopeFactory,
            ILogger<OpcUaMultiServerService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            MonitoringConfigService monitoringConfigService)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _monitoringConfigService = monitoringConfigService;
            _clientConfig = BuildClientConfiguration();

            LoadAndConnectSavedServers();
        }

        private void LoadAndConnectSavedServers()
        {
            _logger.LogInformation("Caricamento connessioni server salvate...");
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // --- MODIFICA CHIAVE ---
            // Aggiunto il filtro per caricare solo le connessioni di tipo "opcua".
            var savedConnections = dbContext.Connessioni.Where(c => c.Tipo == "opcua").ToList();
            _logger.LogInformation($"Trovate {savedConnections.Count} connessioni di tipo 'opcua'.");

            foreach (var conn in savedConnections)
            {
                var serverMonitoringConfig = _monitoringConfigService.GetConfigForServer(conn.NomeMacchina);
                var serverInstance = new OpcServerInstance(
                    conn.NomeMacchina, conn.IP_Address, conn.Porta,
                    _clientConfig, _scopeFactory, _logger, _configuration, _httpClientFactory,
                    _monitoringConfigService,
                    serverMonitoringConfig
                );
                _serverInstances.TryAdd(conn.NomeMacchina, serverInstance);
                Task.Run(() => serverInstance.ConnectAsync());
            }
        }

        public async Task EditServerAsync(ConnessioneEditModel model)
        {
            _logger.LogInformation("Avvio modifica per '{OriginalNomeMacchina}'", model.OriginalNomeMacchina);

            if (_serverInstances.TryRemove(model.OriginalNomeMacchina, out var oldInstance))
            {
                await oldInstance.DisconnectAsync();
                oldInstance.Dispose();
            }

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var connectionToUpdate = await dbContext.Connessioni.AsNoTracking().FirstOrDefaultAsync(c => c.NomeMacchina == model.OriginalNomeMacchina);
            if (connectionToUpdate != null)
            {
                if (model.OriginalNomeMacchina != model.NomeMacchina)
                {
                    var oldConfig = _monitoringConfigService.GetConfigForServer(model.OriginalNomeMacchina);
                    var newConfig = _monitoringConfigService.GetConfigForServer(model.NomeMacchina);
                    newConfig.DbLoggingNodes = oldConfig.DbLoggingNodes;
                    newConfig.TelegramAlarmingNodes = oldConfig.TelegramAlarmingNodes;

                    await _monitoringConfigService.AddDbLoggingNodeAsync(model.NomeMacchina, string.Empty, string.Empty);
                    await _monitoringConfigService.RemoveServerConfigAsync(model.OriginalNomeMacchina);

                    using var transaction = await dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // --- MODIFICA CHIAVE ---
                        // Aggiunta la proprietà 'Tipo' per mantenerla durante l'aggiornamento.
                        var newConnection = new Connessione { NomeMacchina = model.NomeMacchina, IP_Address = model.IP_Address, Porta = model.Porta, Tipo = connectionToUpdate.Tipo };
                        dbContext.Connessioni.Add(newConnection);
                        await dbContext.SaveChangesAsync();
                        await dbContext.Database.ExecuteSqlRawAsync("UPDATE macchineopcua SET NomeMacchina = {0} WHERE NomeMacchina = {1}", model.NomeMacchina, model.OriginalNomeMacchina);
                        dbContext.Connessioni.Remove(connectionToUpdate);
                        await dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Errore durante la transazione di modifica per '{OriginalNomeMacchina}'", model.OriginalNomeMacchina);
                        throw;
                    }
                }
                else
                {
                    // --- MODIFICA CHIAVE ---
                    // Aggiunta la proprietà 'Tipo' per mantenerla durante l'aggiornamento.
                    var entityToUpdate = new Connessione { NomeMacchina = model.NomeMacchina, IP_Address = model.IP_Address, Porta = model.Porta, Tipo = connectionToUpdate.Tipo };
                    dbContext.Connessioni.Update(entityToUpdate);
                    await dbContext.SaveChangesAsync();
                }
            }
            await AddAndConnectServerAsync(model.NomeMacchina, model.IP_Address, model.Porta, false);
        }

        public async Task DeleteServerAsync(string nomeMacchina)
        {
            if (_serverInstances.TryRemove(nomeMacchina, out var instance))
            {
                await instance.DisconnectAsync();
                instance.Dispose();
            }
            await _monitoringConfigService.RemoveServerConfigAsync(nomeMacchina);
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var connessioneDb = await dbContext.Connessioni.FindAsync(nomeMacchina);
            if (connessioneDb != null)
            {
                dbContext.Connessioni.Remove(connessioneDb);
                await dbContext.SaveChangesAsync();
            }
        }

        private ApplicationConfiguration BuildClientConfiguration() { var config = new ApplicationConfiguration() { ApplicationName = "OpcUaWebAppClient", ApplicationType = ApplicationType.Client, ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }, SecurityConfiguration = new SecurityConfiguration { ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = "OpcUaWebAppClient" }, TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" }, TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" }, RejectedCertificateStore = new CertificateStoreIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" }, AutoAcceptUntrustedCertificates = true }, CertificateValidator = new CertificateValidator() }; config.CertificateValidator.CertificateValidation += (validator, eventArgs) => { if (ServiceResult.IsBad(eventArgs.Error)) eventArgs.Accept = true; }; config.Validate(ApplicationType.Client).Wait(); var application = new ApplicationInstance(config); application.CheckApplicationInstanceCertificate(false, 2048).Wait(); return config; }

        public async Task AddAndConnectServerAsync(string nomeMacchina, string ipAddress, int port, bool saveToDb = true)
        {
            if (_serverInstances.ContainsKey(nomeMacchina))
            {
                _logger.LogWarning("Server '{nomeMacchina}' già registrato.", nomeMacchina); return;
            }
            if (saveToDb)
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (!await dbContext.Connessioni.AnyAsync(c => c.NomeMacchina == nomeMacchina))
                {
                    // --- MODIFICA CHIAVE ---
                    // Aggiunta la proprietà 'Tipo' con valore "opcua" durante la creazione.
                    dbContext.Connessioni.Add(new Connessione { NomeMacchina = nomeMacchina, IP_Address = ipAddress, Porta = port, Tipo = "opcua" });
                    await dbContext.SaveChangesAsync();
                }
            }
            var serverMonitoringConfig = _monitoringConfigService.GetConfigForServer(nomeMacchina);
            var serverInstance = new OpcServerInstance(nomeMacchina, ipAddress, port, _clientConfig, _scopeFactory, _logger, _configuration, _httpClientFactory, _monitoringConfigService, serverMonitoringConfig);
            _serverInstances.TryAdd(nomeMacchina, serverInstance);
            await serverInstance.ConnectAsync(); _logger.LogInformation("Aggiunto server '{nomeMacchina}' e tentata connessione.", nomeMacchina);
        }

        public OpcServerInstance? GetServerInstance(string nomeMacchina) => _serverInstances.GetValueOrDefault(nomeMacchina);
        public List<OpcServerInstance> GetAllServerInstances() => _serverInstances.Values.ToList();
    }

    public class OpcServerInstance : IDisposable
    {
        public string NomeMacchina { get; }
        public string IpAddress { get; }
        public int Port { get; }
        public bool IsConnected => _session?.Connected ?? false;
        public bool IsAttemptingReconnection { get; private set; }

        private Session? _session;
        private Subscription? _subscription;
        private readonly ApplicationConfiguration _clientConfig;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConcurrentDictionary<string, MonitoredItem> _monitoredItems = new();
        private Timer? _reconnectionTimer;
        private readonly object _reconnectionLock = new();
        private readonly MonitoringConfigService _monitoringConfigService;
        private readonly ServerMonitoringConfig _serverConfig;

        public OpcServerInstance(
            string nomeMacchina, string ipAddress, int port, ApplicationConfiguration clientConfig,
            IServiceScopeFactory scopeFactory, ILogger logger, IConfiguration configuration, IHttpClientFactory httpClientFactory,
            MonitoringConfigService monitoringConfigService, ServerMonitoringConfig serverConfig)
        {
            NomeMacchina = nomeMacchina; IpAddress = ipAddress; Port = port; _clientConfig = clientConfig;
            _scopeFactory = scopeFactory; _logger = logger; _configuration = configuration; _httpClientFactory = httpClientFactory;
            _monitoringConfigService = monitoringConfigService;
            _serverConfig = serverConfig;
        }

        public async Task<bool> ConnectAsync()
        {
            if (IsConnected) return true;
            try
            {
                string endpointUrl = $"opc.tcp://{IpAddress}:{Port}";
                var endpointDescription = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(_clientConfig);
                var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                _session = await Session.Create(_clientConfig, configuredEndpoint, true, _clientConfig.ApplicationName, 60000, new UserIdentity(), null);
                _subscription = new Subscription(_session.DefaultSubscription) { PublishingInterval = 1000 };
                _session.AddSubscription(_subscription);
                _subscription.Create();
                _logger.LogInformation("Connessione per '{NomeMacchina}' stabilita.", NomeMacchina);
                StopReconnectionLoop();
                await ResubscribeMonitoredItems();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connessione fallita per '{NomeMacchina}'.", NomeMacchina);
                StartReconnectionLoop();
                return false;
            }
        }

        private async Task ResubscribeMonitoredItems()
        {
            if (!IsConnected) return;
            _logger.LogInformation("Ripristino delle sottoscrizioni per {NomeMacchina}...", NomeMacchina);
            _monitoredItems.Clear();

            var allNodesToMonitor = _serverConfig.DbLoggingNodes.Values
                .Concat(_serverConfig.TelegramAlarmingNodes.Values)
                .DistinctBy(v => v.NodeId)
                .ToDictionary(v => v.NodeId, v => v.DisplayName);

            foreach (var node in allNodesToMonitor)
            {
                await EnsureMonitoredAsync(node.Key, node.Value);
            }
            _logger.LogInformation("{Count} sottoscrizioni ripristinate per {NomeMacchina}.", allNodesToMonitor.Count, NomeMacchina);
        }

        public async Task StartDbLogging(string nodeId, string displayName)
        {
            await _monitoringConfigService.AddDbLoggingNodeAsync(NomeMacchina, nodeId, displayName);
            await EnsureMonitoredAsync(nodeId, displayName);
        }

        public async Task StopDbLogging(string nodeId)
        {
            await _monitoringConfigService.RemoveDbLoggingNodeAsync(NomeMacchina, nodeId);
            await CleanupMonitoredItemIfNeeded(nodeId);
        }

        public async Task StartTelegramAlarming(string nodeId, string displayName)
        {
            await _monitoringConfigService.AddTelegramAlarmingNodeAsync(NomeMacchina, nodeId, displayName);
            await EnsureMonitoredAsync(nodeId, displayName);
        }

        public async Task StopTelegramAlarming(string nodeId)
        {
            await _monitoringConfigService.RemoveTelegramAlarmingNodeAsync(NomeMacchina, nodeId);
            await CleanupMonitoredItemIfNeeded(nodeId);
        }

        private void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            if (monitoredItem.Handle is not OpcServerInstance serverInstance) { return; }
            if (e.NotificationValue is not MonitoredItemNotification notification || notification.Value == null) return;

            var value = notification.Value;
            var nodeId = monitoredItem.StartNodeId.ToString();

            if (serverInstance._serverConfig.DbLoggingNodes.ContainsKey(nodeId))
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logEntry = new MacchinaOpcUaLog { NomeMacchina = serverInstance.NomeMacchina, Nome = monitoredItem.DisplayName, Nodo = nodeId, Valore = value.Value?.ToString(), Qualita = Opc.Ua.StatusCodes.GetBrowseName(value.StatusCode.Code), Timestamp = value.SourceTimestamp.ToLocalTime() };
                dbContext.MacchineOpcUaLog.Add(logEntry);
                dbContext.SaveChanges();
            }

            if (serverInstance._serverConfig.TelegramAlarmingNodes.ContainsKey(nodeId))
            {
                if (value.StatusCode.Code != Opc.Ua.StatusCodes.Good)
                {
                    var message = $"⚠️ *Allarme Qualità OPC UA*\n\n" + $"*Macchina*: `{serverInstance.NomeMacchina}`\n" + $"*Tag*: `{monitoredItem.DisplayName}`\n" + $"*Qualità*: `{Opc.Ua.StatusCodes.GetBrowseName(value.StatusCode.Code)}`";
                    _ = SendTelegramNotificationAsync(message);
                }
            }
        }

        private Task EnsureMonitoredAsync(string nodeId, string displayName)
        {
            if (string.IsNullOrEmpty(nodeId) || _monitoredItems.ContainsKey(nodeId)) return Task.CompletedTask;
            if (!IsConnected || _subscription == null) return Task.CompletedTask;

            var item = new MonitoredItem(_subscription.DefaultItem) { DisplayName = displayName, StartNodeId = new NodeId(nodeId), Handle = this };
            item.Notification += MonitoredItem_Notification;
            _subscription.AddItem(item);
            _subscription.ApplyChanges();
            _monitoredItems.TryAdd(nodeId, item);
            _logger.LogInformation("Avviato monitoraggio per nodo: {nodeId} su macchina {NomeMacchina}", nodeId, NomeMacchina);
            return Task.CompletedTask;
        }

        private Task CleanupMonitoredItemIfNeeded(string nodeId)
        {
            if (!_serverConfig.DbLoggingNodes.ContainsKey(nodeId) && !_serverConfig.TelegramAlarmingNodes.ContainsKey(nodeId))
            {
                if (_monitoredItems.TryRemove(nodeId, out var itemToRemove))
                {
                    _subscription?.RemoveItem(itemToRemove);
                    _subscription?.ApplyChanges();
                }
            }
            return Task.CompletedTask;
        }

        public Task<List<OpcNodeViewModel>> BrowseAsync(string? nodeId)
        {
            if (!IsConnected || _session == null)
            {
                throw new InvalidOperationException($"Sessione con '{NomeMacchina}' non è attiva.");
            }
            var nodeViewModels = new List<OpcNodeViewModel>();
            try
            {
                NodeId nodeToBrowse = nodeId == null ? ObjectIds.ObjectsFolder : new NodeId(nodeId);
                var browseDescription = new BrowseDescription { NodeId = nodeToBrowse, BrowseDirection = BrowseDirection.Forward, ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences, IncludeSubtypes = true, NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable | NodeClass.Method), ResultMask = (uint)BrowseResultMask.All };
                _session.Browse(null, null, 0, new BrowseDescriptionCollection { browseDescription }, out var results, out _);
                if (results != null && results.Count > 0)
                {
                    foreach (var rd in results[0].References)
                        nodeViewModels.Add(CreateNodeViewModel(_session, rd));
                }
            }
            catch (ServiceResultException sre) when (sre.StatusCode == Opc.Ua.StatusCodes.BadNotConnected)
            {
                StartReconnectionLoop();
                throw new InvalidOperationException($"La connessione con il server '{NomeMacchina}' è stata persa.");
            }
            return Task.FromResult(nodeViewModels);
        }

        private OpcNodeViewModel CreateNodeViewModel(Session session, ReferenceDescription rd)
        {
            NodeId currentNodeId = (NodeId)rd.NodeId;
            var nodeModel = new OpcNodeViewModel
            {
                NodeId = currentNodeId.ToString(),
                DisplayName = rd.DisplayName.Text,
                NodeClass = rd.NodeClass.ToString(),
                IsDbLogging = _serverConfig.DbLoggingNodes.ContainsKey(currentNodeId.ToString()),
                IsTelegramAlarming = _serverConfig.TelegramAlarmingNodes.ContainsKey(currentNodeId.ToString())
            };
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
                    nodeModel.Status = Opc.Ua.StatusCode.LookupSymbolicId(dataValue.StatusCode.Code);
                }
                catch { nodeModel.Value = "N/D"; nodeModel.Status = "ErroreLettura"; }
            }
            return nodeModel;
        }

        private void StartReconnectionLoop() { lock (_reconnectionLock) { if (IsAttemptingReconnection) return; IsAttemptingReconnection = true; _logger.LogInformation("Avvio loop di riconnessione per {NomeMacchina}", NomeMacchina); _session?.Dispose(); _session = null; _reconnectionTimer = new Timer(ReconnectCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)); } }
        private void StopReconnectionLoop() { lock (_reconnectionLock) { if (!IsAttemptingReconnection) return; _reconnectionTimer?.Dispose(); _reconnectionTimer = null; IsAttemptingReconnection = false; _logger.LogInformation("Loop di riconnessione interrotto per {NomeMacchina}", NomeMacchina); } }
        private async void ReconnectCallback(object? state) { _logger.LogInformation("Tentativo di riconnessione per {NomeMacchina}...", NomeMacchina); await ConnectAsync(); }
        public Task<List<OpcNodeViewModel>> ReadValuesAsync(List<string> nodeIds) { if (!IsConnected || _session == null || nodeIds == null || !nodeIds.Any()) return Task.FromResult(new List<OpcNodeViewModel>()); try { var updatedValues = new List<OpcNodeViewModel>(); var nodesToRead = new ReadValueIdCollection(nodeIds.Select(id => new ReadValueId { NodeId = new NodeId(id), AttributeId = Attributes.Value })); _session.Read(null, 0, TimestampsToReturn.Neither, nodesToRead, out DataValueCollection values, out _); for (int i = 0; i < values.Count; i++) updatedValues.Add(new OpcNodeViewModel { NodeId = nodeIds[i], Value = values[i].Value?.ToString() ?? "null", Status = Opc.Ua.StatusCode.LookupSymbolicId(values[i].StatusCode.Code) }); return Task.FromResult(updatedValues); } catch (ServiceResultException sre) when (sre.StatusCode == Opc.Ua.StatusCodes.BadNotConnected) { StartReconnectionLoop(); throw new InvalidOperationException($"La connessione con il server '{NomeMacchina}' è stata persa."); } }
        private async Task SendTelegramNotificationAsync(string message) { var botToken = _configuration["Telegram:BotToken"]; var chatId = _configuration["Telegram:ChatId"]; if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId)) return; var httpClient = _httpClientFactory.CreateClient("TelegramNotifier"); var url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}&parse_mode=Markdown"; await httpClient.PostAsync(url, null); }
        public async Task DisconnectAsync() { if (_session != null) await _session.CloseAsync(); }
        public void Dispose() { _reconnectionTimer?.Dispose(); DisconnectAsync().Wait(); _session?.Dispose(); }
    }
}
