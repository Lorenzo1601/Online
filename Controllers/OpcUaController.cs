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

    public class OpcUaController : Controller
    {
        /// <summary>
        /// Scopre i server disponibili a un dato indirizzo IP e porta.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Discover(string ipAddress, string port)
        {
            if (string.IsNullOrEmpty(ipAddress))
            {
                return BadRequest("L'indirizzo del server non può essere vuoto.");
            }

            string portToUse = string.IsNullOrWhiteSpace(port) ? "4840" : port;
            string discoveryUrl = $"opc.tcp://{ipAddress.Trim()}:{portToUse.Trim()}";
            var endpointViewModels = new List<EndpointViewModel>();

            try
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
                        TrustedIssuerCertificates = new CertificateTrustList(),
                        TrustedPeerCertificates = new CertificateTrustList(),
                        RejectedCertificateStore = new CertificateStoreIdentifier(),
                        AutoAcceptUntrustedCertificates = true
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                    CertificateValidator = new CertificateValidator()
                };

                config.SecurityConfiguration.TrustedIssuerCertificates.StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities";
                config.SecurityConfiguration.TrustedPeerCertificates.StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications";
                config.SecurityConfiguration.RejectedCertificateStore.StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates";

                config.CertificateValidator.CertificateValidation += (sender, e) =>
                {
                    if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted) e.Accept = true;
                };

                await config.Validate(ApplicationType.Client);

                var application = new ApplicationInstance(config);
                if (!await application.CheckApplicationInstanceCertificate(false, 2048))
                {
                    throw new Exception("Creazione del certificato dell'applicazione fallita.");
                }

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
                string errorMessage = $"Errore durante la scoperta dei server a '{discoveryUrl}': {ex.Message}";
                return this.StatusCode(500, new { message = errorMessage });
            }
        }


        /// <summary>
        /// Naviga i nodi di un endpoint OPC UA specifico.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Browse(string endpointUrl, string? nodeId)
        {
            if (string.IsNullOrEmpty(endpointUrl))
            {
                return BadRequest("L'URL dell'endpoint non può essere vuoto.");
            }

            var nodeViewModels = new List<OpcNodeViewModel>();

            try
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

                config.CertificateValidator.CertificateValidation += (sender, e) =>
                {
                    if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted) e.Accept = true;
                };

                await config.Validate(ApplicationType.Client);

                var application = new ApplicationInstance(config);
                if (!await application.CheckApplicationInstanceCertificate(false, 2048))
                {
                    throw new Exception("Creazione del certificato dell'applicazione fallita.");
                }

                var endpointDescription = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

                using (var session = await Session.Create(config, configuredEndpoint, true, config.ApplicationName, 60000, new UserIdentity(), null))
                {
                    NodeId nodeToBrowse = nodeId == null ? ObjectIds.ObjectsFolder : new NodeId(nodeId);

                    var browseDescription = new BrowseDescription()
                    {
                        NodeId = nodeToBrowse,
                        BrowseDirection = BrowseDirection.Forward,
                        ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                        IncludeSubtypes = true,
                        NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                        ResultMask = (uint)BrowseResultMask.All
                    };

                    session.Browse(null, null, 0, new BrowseDescriptionCollection { browseDescription }, out var results, out var diagnosticInfos);
                    ReferenceDescriptionCollection references = results[0].References;

                    foreach (var rd in references)
                    {
                        NodeId currentNodeId = (NodeId)rd.NodeId;
                        // MODIFICA: Aggiungo la classe del nodo al modello
                        var nodeModel = new OpcNodeViewModel
                        {
                            NodeId = currentNodeId.ToString(),
                            DisplayName = rd.DisplayName.Text,
                            NodeClass = rd.NodeClass.ToString() // <-- Aggiunto per la UI
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
    }
}
