using Microsoft.AspNetCore.Mvc;
using Online;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        try
        {
            string discoveryUrl = $"opc.tcp://{ipAddress}:{port}";
            var endpoints = await _opcUaService.DiscoverEndpointsAsync(discoveryUrl);

            // --- CORREZIONE APPLICATA QUI ---
            // Ho cambiato e.ApplicationName.Text in e.Server.ApplicationName.Text
            // per accedere correttamente al nome dell'applicazione.
            var result = endpoints.Select(e => new {
                ApplicationName = e.Server.ApplicationName.Text,
                EndpointUrl = e.EndpointUrl,
                SecurityMode = e.SecurityMode.ToString()
            }).ToList();
            // --- FINE CORREZIONE ---

            return Json(result);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { message = $"Discovery fallito: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Connect(string endpointUrl)
    {
        try
        {
            await _opcUaService.ConnectAsync(endpointUrl);
            return Ok();
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { message = $"Connessione fallita: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Browse(string? nodeId)
    {
        try
        {
            var nodes = await _opcUaService.BrowseAsync(nodeId);
            return Json(nodes);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { message = $"Browsing fallito: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReadValues([FromBody] ReadValuesRequest request)
    {
        try
        {
            var values = await _opcUaService.ReadValuesAsync(request.NodeIds);
            return Json(values);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { message = $"Lettura fallita: {ex.Message}" });
        }
    }

    [HttpPost("OpcUa/Start/DbLogging")]
    public async Task<IActionResult> StartDbLogging(string nodeId, string displayName)
    {
        await _opcUaService.StartDbLogging(nodeId, displayName);
        return Ok();
    }

    [HttpPost("OpcUa/Stop/DbLogging")]
    public async Task<IActionResult> StopDbLogging(string nodeId)
    {
        await _opcUaService.StopDbLogging(nodeId);
        return Ok();
    }

    [HttpPost("OpcUa/Start/TelegramAlarming")]
    public async Task<IActionResult> StartTelegramAlarming(string nodeId, string displayName)
    {
        await _opcUaService.StartTelegramAlarming(nodeId, displayName);
        return Ok();
    }

    [HttpPost("OpcUa/Stop/TelegramAlarming")]
    public async Task<IActionResult> StopTelegramAlarming(string nodeId)
    {
        await _opcUaService.StopTelegramAlarming(nodeId);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        return Json(new { isConnected = _opcUaService.IsConnected, endpointUrl = _opcUaService.ConnectedEndpointUrl });
    }

    [HttpGet]
    public async Task<IActionResult> GetMonitoringStatus()
    {
        var status = await _opcUaService.GetMonitoringStatus();
        return Json(status);
    }
}

public class ReadValuesRequest
{
    public List<string> NodeIds { get; set; }
}
