using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Online.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Online.Controllers
{
    public class OpcUaController : Controller
    {
        private readonly OpcUaService _opcUaService;
        private readonly ILogger<OpcUaController> _logger;
        private readonly IWebHostEnvironment _env;

        public OpcUaController(OpcUaService opcUaService, ILogger<OpcUaController> logger, IWebHostEnvironment env)
        {
            _opcUaService = opcUaService;
            _logger = logger;
            _env = env;
        }

        [HttpPost]
        public async Task<IActionResult> Discover(string ipAddress, string port)
        {
            try
            {
                var discoveryUrl = $"opc.tcp://{ipAddress}:{port}";
                var endpoints = await _opcUaService.DiscoverEndpointsAsync(discoveryUrl);
                return Ok(endpoints);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discovery failed for {ip}:{port}", ipAddress, port);
                return BadRequest(new { message = $"Discovery failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Connect(string endpointUrl)
        {
            try
            {
                await _opcUaService.ConnectAsync(endpointUrl);
                return Ok(new { message = "Connected successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection failed for endpoint {endpointUrl}", endpointUrl);
                return BadRequest(new { message = $"Connection failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Browse(string? nodeId)
        {
            try
            {
                var nodes = await _opcUaService.BrowseAsync(nodeId);
                return Ok(nodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Browse failed for node {nodeId}", nodeId);
                return BadRequest(new { message = $"Browse failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReadValues([FromBody] NodeIdRequest request)
        {
            try
            {
                var values = await _opcUaService.ReadValuesAsync(request.NodeIds);
                return Ok(values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read values.");
                return BadRequest(new { message = "Failed to read values." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> StartLogging(string nodeId, string displayName)
        {
            try
            {
                await _opcUaService.StartLogging(nodeId, displayName);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start logging for node {nodeId}", nodeId);
                return BadRequest(new { message = "Failed to start logging." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> StopLogging(string nodeId)
        {
            try
            {
                await _opcUaService.StopLogging(nodeId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop logging for node {nodeId}", nodeId);
                return BadRequest(new { message = "Failed to stop logging." });
            }
        }

        [HttpGet]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                IsConnected = _opcUaService.IsConnected,
                EndpointUrl = _opcUaService.ConnectedEndpointUrl
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetLoggedNodes()
        {
            var loggedNodes = await _opcUaService.GetLoggedNodeIds();
            return Ok(loggedNodes);
        }

        [HttpPost]
        public IActionResult SaveConnectionInfo([FromBody] OpcUaConnectionSettings settings)
        {
            if (settings == null) return BadRequest();

            try
            {
                string appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                var jsonString = System.IO.File.ReadAllText(appSettingsPath);
                var jsonObj = JsonNode.Parse(jsonString)!.AsObject();

                var connectionSettingsNode = jsonObj["OpcUaConnectionSettings"]?.AsObject() ?? new JsonObject();
                jsonObj["OpcUaConnectionSettings"] = connectionSettingsNode;

                connectionSettingsNode["LastIpAddress"] = settings.LastIpAddress;
                connectionSettingsNode["LastPort"] = settings.LastPort;

                var options = new JsonSerializerOptions { WriteIndented = true };
                var updatedJsonString = jsonObj.ToJsonString(options);
                System.IO.File.WriteAllText(appSettingsPath, updatedJsonString);

                return Ok(new { message = "Impostazioni di connessione salvate." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il salvataggio delle impostazioni di connessione OPC UA.");
                return StatusCode(500, new { message = "Errore interno del server durante il salvataggio." });
            }
        }
    }

    public class NodeIdRequest
    {
        public List<string> NodeIds { get; set; } = new List<string>();
    }
}