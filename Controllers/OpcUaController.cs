using Microsoft.AspNetCore.Mvc;
using Online.Models;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Online.Controllers
{
    public class EndpointViewModel
    {
        public string ApplicationName { get; set; }
        public string EndpointUrl { get; set; }
        public string SecurityMode { get; set; }
    }

    // Nuovo modello per la richiesta di lettura valori
    public class ReadValuesRequest
    {
        public string EndpointUrl { get; set; }
        public List<string> NodeIds { get; set; }
    }

    public class OpcUaController : Controller
    {
        [HttpPost]
        public async Task<IActionResult> Discover(string ipAddress, string port)
        {
            if (string.IsNullOrEmpty(ipAddress)) return BadRequest("L'indirizzo del server non può essere vuoto.");
            string portToUse = string.IsNullOrWhiteSpace(port) ? "4840" : port;
            string discoveryUrl = $"opc.tcp://{ipAddress.Trim()}:{portToUse.Trim()}";
            var endpointViewModels = new List<EndpointViewModel>();

            try
            {
                var config = await GetClientConfiguration();
                var discoveryClient = DiscoveryClient.Create(new Uri(discoveryUrl));
                EndpointDescriptionCollection endpoints = await discoveryClient.GetEndpointsAsync(null);

                foreach (var ep in endpoints)
                {
                    endpointViewModels.Add(new EndpointViewModel
                    {
                        ApplicationName = ep.Server.ApplicationName.Text,
                        EndpointUrl = ep.EndpointUrl,
                        SecurityMode = ep.SecurityMode.ToString()
                    });
                }
                return Json(endpointViewModels);
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { message = $"Errore durante la scoperta: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Browse(string endpointUrl, string? nodeId)
        {
            if (string.IsNullOrEmpty(endpointUrl)) return BadRequest("L'URL dell'endpoint non può essere vuoto.");
            var nodeViewModels = new List<OpcNodeViewModel>();
            try
            {
                var config = await GetClientConfiguration();
                var endpointDescription = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

                using (var session = await Session.Create(config, configuredEndpoint, true, config.ApplicationName, 60000, new UserIdentity(), null))
                {
                    NodeId nodeToBrowse = nodeId == null ? ObjectIds.ObjectsFolder : new NodeId(nodeId);
                    var browseDescription = new BrowseDescription { NodeId = nodeToBrowse, BrowseDirection = BrowseDirection.Forward, ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences, IncludeSubtypes = true, NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable | NodeClass.Method), ResultMask = (uint)BrowseResultMask.All };
                    session.Browse(null, null, 0, new BrowseDescriptionCollection { browseDescription }, out var results, out _);

                    foreach (var rd in results[0].References)
                    {
                        var nodeModel = CreateNodeViewModel(session, rd);
                        nodeViewModels.Add(nodeModel);
                    }
                }
                return Json(nodeViewModels);
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { message = $"Errore durante la navigazione: {ex.Message}" });
            }
        }

        // --- NUOVA AZIONE PER AGGIORNARE I VALORI ---
        [HttpPost]
        public async Task<IActionResult> ReadValues([FromBody] ReadValuesRequest request)
        {
            if (string.IsNullOrEmpty(request.EndpointUrl) || request.NodeIds == null || !request.NodeIds.Any())
            {
                return BadRequest("Richiesta non valida.");
            }

            var updatedValues = new List<OpcNodeViewModel>();
            try
            {
                var config = await GetClientConfiguration();
                var endpointDescription = CoreClientUtils.SelectEndpoint(request.EndpointUrl, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

                using (var session = await Session.Create(config, configuredEndpoint, true, config.ApplicationName, 60000, new UserIdentity(), null))
                {
                    var nodesToRead = new ReadValueIdCollection(request.NodeIds.Select(id => new ReadValueId { NodeId = new NodeId(id), AttributeId = Attributes.Value }));
                    session.Read(null, 0, TimestampsToReturn.Neither, nodesToRead, out DataValueCollection values, out _);

                    for (int i = 0; i < values.Count; i++)
                    {
                        updatedValues.Add(new OpcNodeViewModel
                        {
                            NodeId = request.NodeIds[i],
                            Value = values[i].Value?.ToString() ?? "null",
                            Status = Opc.Ua.StatusCode.LookupSymbolicId(values[i].StatusCode.Code)
                        });
                    }
                }
                return Json(updatedValues);
            }
            catch (Exception ex)
            {
                // In caso di errore, restituisce una lista vuota per non interrompere il polling
                return Json(new List<OpcNodeViewModel>());
            }
        }


        // --- METODI HELPER ---
        private async Task<ApplicationConfiguration> GetClientConfiguration()
        {
            var config = new ApplicationConfiguration()
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

            config.CertificateValidator.CertificateValidation += (sender, e) => { if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted) e.Accept = true; };
            await config.Validate(ApplicationType.Client);
            if (!await new ApplicationInstance(config).CheckApplicationInstanceCertificate(false, 2048))
            {
                throw new Exception("Creazione certificato fallita.");
            }
            return config;
        }

        private OpcNodeViewModel CreateNodeViewModel(Session session, ReferenceDescription rd)
        {
            NodeId currentNodeId = (NodeId)rd.NodeId;
            var nodeModel = new OpcNodeViewModel
            {
                NodeId = currentNodeId.ToString(),
                DisplayName = rd.DisplayName.Text,
                NodeClass = rd.NodeClass.ToString()
            };

            try
            {
                var childBrowseDescription = new BrowseDescription() { NodeId = currentNodeId, BrowseDirection = BrowseDirection.Forward, ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences, IncludeSubtypes = true, NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable), ResultMask = (uint)BrowseResultMask.All };
                session.Browse(null, null, 1, new BrowseDescriptionCollection { childBrowseDescription }, out var childResults, out _);
                nodeModel.HasChildren = childResults.Count > 0 && childResults[0].References.Count > 0;
            }
            catch { nodeModel.HasChildren = false; }

            if (rd.NodeClass == NodeClass.Variable)
            {
                try
                {
                    DataValue value = session.ReadValue(currentNodeId);
                    nodeModel.Value = value.Value?.ToString() ?? "null";
                    nodeModel.Status = Opc.Ua.StatusCode.LookupSymbolicId(value.StatusCode.Code);
                }
                catch (Exception readEx)
                {
                    nodeModel.Value = "N/D";
                    nodeModel.Status = $"ErroreLettura: {readEx.Message}";
                }
            }
            return nodeModel;
        }
    }
}
