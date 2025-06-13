using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Online.Models; // Assicurati di avere i modelli nello scope

namespace Online.Controllers
{
    // --- DEFINIZIONI MANCANTI AGGIUNTE QUI ---
    public class EndpointViewModel
    {
        public string ApplicationName { get; set; }
        public string EndpointUrl { get; set; }
        public string SecurityMode { get; set; }
    }

    public class ReadValuesRequest
    {
        public List<string> NodeIds { get; set; }
    }
    // --- FINE DEFINIZIONI ---

    public class OpcUaController : Controller
    {
        private readonly OpcUaService _opcUaService;

        public OpcUaController(OpcUaService opcUaService)
        {
            _opcUaService = opcUaService;
        }

        [HttpPost]
        public async Task<IActionResult> Discover(string ipAddress, string port)
        {
            if (string.IsNullOrEmpty(ipAddress)) return BadRequest("L'indirizzo del server non può essere vuoto.");
            string portToUse = string.IsNullOrWhiteSpace(port) ? "4840" : port;
            string discoveryUrl = $"opc.tcp://{ipAddress.Trim()}:{portToUse.Trim()}";

            try
            {
                var endpoints = await _opcUaService.DiscoverEndpointsAsync(discoveryUrl);
                var viewModel = endpoints.Select(ep => new EndpointViewModel
                {
                    ApplicationName = ep.Server.ApplicationName.Text,
                    EndpointUrl = ep.EndpointUrl,
                    SecurityMode = ep.SecurityMode.ToString()
                }).ToList();
                return Json(viewModel);
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { message = $"Errore durante la scoperta: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Connect(string endpointUrl)
        {
            if (string.IsNullOrEmpty(endpointUrl)) return BadRequest("L'URL dell'endpoint non può essere vuoto.");
            try
            {
                await _opcUaService.ConnectAsync(endpointUrl);
                return Ok(new { message = "Connesso con successo." });
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { message = $"Connessione fallita: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult GetStatus()
        {
            return Json(new
            {
                isConnected = _opcUaService.IsConnected,
                endpointUrl = _opcUaService.ConnectedEndpointUrl
            });
        }

        [HttpPost]
        public async Task<IActionResult> Browse(string? nodeId)
        {
            if (!_opcUaService.IsConnected) return BadRequest("Nessuna sessione attiva.");
            try
            {
                var nodes = await _opcUaService.BrowseAsync(nodeId);
                return Json(nodes);
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { message = $"Errore durante la navigazione: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReadValues([FromBody] ReadValuesRequest request)
        {
            if (!_opcUaService.IsConnected || request.NodeIds == null || !request.NodeIds.Any())
            {
                return BadRequest("Richiesta non valida.");
            }
            var values = await _opcUaService.ReadValuesAsync(request.NodeIds);
            return Json(values);
        }

        // --- AZIONI DI LOGGING AGGIUNTE ---
        [HttpPost]
        public async Task<IActionResult> StartLogging(string nodeId, string displayName)
        {
            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(displayName)) return BadRequest("NodeId e DisplayName sono obbligatori.");
            await _opcUaService.StartLogging(nodeId, displayName);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> StopLogging(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return BadRequest("NodeId è obbligatorio.");
            await _opcUaService.StopLogging(nodeId);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetLoggedNodes()
        {
            var nodeIds = await _opcUaService.GetLoggedNodeIds();
            return Json(nodeIds);
        }
        // --- FINE AZIONI DI LOGGING ---
    }
}
