using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Online.Data;
using Online.Models;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Online
{
    public class OpcUaService : IDisposable
    {
        private Session? _session;
        private ApplicationConfiguration? _config;
        private Subscription? _subscription;
        private readonly ConcurrentDictionary<string, MonitoredItem> _loggedItems = new();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OpcUaService> _logger;

        public bool IsConnected => _session?.Connected ?? false;
        public string? ConnectedEndpointUrl => _session?.Endpoint.EndpointUrl;

        public OpcUaService(IServiceScopeFactory scopeFactory, ILogger<OpcUaService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
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

        public async Task<List<EndpointDescription>> DiscoverEndpointsAsync(string discoveryUrl)
        {
            var discoveryClient = DiscoveryClient.Create(_config, new Uri(discoveryUrl));
            EndpointDescriptionCollection endpoints = await discoveryClient.GetEndpointsAsync(null);
            return endpoints.ToList();
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
                _loggedItems.Clear();
                return sessionToClose.CloseAsync();
            }
            return Task.CompletedTask;
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

        public Task StartLogging(string nodeId, string displayName)
        {
            if (!IsConnected || _subscription == null) throw new InvalidOperationException("Sessione non attiva per avviare il logging.");

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
            _loggedItems.TryAdd(nodeId, item);
            _logger.LogInformation("Avviata registrazione per il nodo: {nodeId}", nodeId);
            return Task.CompletedTask;
        }

        public Task StopLogging(string nodeId)
        {
            if (!IsConnected || _subscription == null) return Task.CompletedTask;

            if (_loggedItems.TryRemove(nodeId, out var itemToRemove))
            {
                _subscription.RemoveItem(itemToRemove);
                _subscription.ApplyChanges();
                _logger.LogInformation("Interrotta registrazione per il nodo: {nodeId}", nodeId);
            }
            return Task.CompletedTask;
        }

        public Task<List<string>> GetLoggedNodeIds()
        {
            return Task.FromResult(_loggedItems.Keys.ToList());
        }

        private void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            if (e.NotificationValue is MonitoredItemNotification notification && notification.Value != null)
            {
                var value = notification.Value;
                _logger.LogInformation("Nuovo valore per {displayName}: {value}", monitoredItem.DisplayName, value.Value);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var logEntry = new MacchinaOpcUaLog
                    {
                        // L'Id non viene impostato qui, sarà generato dal DB
                        Nome = monitoredItem.DisplayName,
                        Nodo = monitoredItem.StartNodeId.ToString(),
                        Valore = value.Value?.ToString(),
                        Qualita = StatusCode.LookupSymbolicId(value.StatusCode.Code),
                        Timestamp = value.SourceTimestamp.ToLocalTime()
                    };

                    // --- CORREZIONE: Usiamo Entity Framework per l'INSERT ---
                    // Questo permette al database di gestire l'auto-incremento dell'Id.
                    dbContext.MacchineOpcUaLog.Add(logEntry);
                    dbContext.SaveChanges();
                    // --- FINE CORREZIONE ---
                }
            }
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
                    DataValue value = session.ReadValue(currentNodeId);
                    nodeModel.Value = value.Value?.ToString() ?? "null";
                    nodeModel.Status = StatusCode.LookupSymbolicId(value.StatusCode.Code);
                }
                catch { nodeModel.Value = "N/D"; nodeModel.Status = "ErroreLettura"; }
            }
            return nodeModel;
        }

        public void Dispose() { DisconnectAsync().Wait(); }
    }
}
