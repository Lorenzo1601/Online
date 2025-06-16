using Microsoft.AspNetCore.Mvc;
using Online.Models;
using System.Threading.Tasks;

namespace Online.Controllers
{
    public class OpcUaController : Controller
    {
        private readonly OpcUaMultiServerService _opcUaService;

        public OpcUaController(OpcUaMultiServerService opcUaService)
        {
            _opcUaService = opcUaService;
        }

        public IActionResult Index() => View("~/Views/Home/OpcUa.cshtml");

        public IActionResult Connection()
        {
            var serverInstances = _opcUaService.GetAllServerInstances();
            return View("~/Views/Home/OpcUaConnection.cshtml", serverInstances);
        }

        [HttpPost]
        public async Task<IActionResult> AddServer(string nomeMacchina, string ipAddress, int port)
        {
            if (string.IsNullOrWhiteSpace(nomeMacchina))
            {
                TempData["Error"] = "Il nome macchina è obbligatorio.";
                return RedirectToAction("Index", "Home");
            }
            await _opcUaService.AddAndConnectServerAsync(nomeMacchina, ipAddress, port);
            return RedirectToAction("Connection");
        }

        [HttpPost("OpcUa/EditServer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditServer(ConnessioneEditModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "I dati per la modifica non sono validi.";
                return RedirectToAction("Connection");
            }
            try
            {
                await _opcUaService.EditServerAsync(model);
                TempData["SuccessMessage"] = "Connessione aggiornata con successo. La pagina verrà ricaricata.";
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = $"Errore durante la modifica: {ex.Message}";
            }
            return RedirectToAction("Connection");
        }

        [HttpPost("OpcUa/DeleteServer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteServer(string nomeMacchina)
        {
            if (string.IsNullOrEmpty(nomeMacchina)) return BadRequest();
            await _opcUaService.DeleteServerAsync(nomeMacchina);
            TempData["SuccessMessage"] = $"Connessione '{nomeMacchina}' e i relativi log sono stati eliminati.";
            return RedirectToAction("Connection");
        }

        [HttpGet("OpcUa/GetAllServersStatus")]
        public IActionResult GetAllServersStatus()
        {
            var statuses = _opcUaService.GetAllServerInstances()
                .Select(i => new { nomeMacchina = i.NomeMacchina, isConnected = i.IsConnected, isReconnecting = i.IsAttemptingReconnection })
                .ToList();
            return Json(statuses);
        }

        [HttpPost("OpcUa/Browse")]
        public async Task<IActionResult> Browse(string nomeMacchina, string? nodeId)
        {
            var instance = _opcUaService.GetServerInstance(nomeMacchina);
            if (instance == null) return NotFound("Server non trovato.");
            try
            {
                var nodes = await instance.BrowseAsync(nodeId);
                return Json(nodes);
            }
            catch (System.InvalidOperationException ex)
            {
                return StatusCode(410, new { message = ex.Message });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = $"Browsing fallito: {ex.Message}" });
            }
        }

        [HttpPost("OpcUa/ReadValues")]
        public async Task<IActionResult> ReadValues([FromBody] ReadValuesRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.NomeMacchina)) return BadRequest("Richiesta non valida.");
            var instance = _opcUaService.GetServerInstance(request.NomeMacchina);
            if (instance == null) return NotFound("Server non trovato.");
            try
            {
                var values = await instance.ReadValuesAsync(request.NodeIds);
                return Json(values);
            }
            catch (System.InvalidOperationException ex)
            {
                return StatusCode(410, new { message = ex.Message });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = $"Lettura fallita: {ex.Message}" });
            }
        }

        [HttpPost("OpcUa/Start/DbLogging")]
        public async Task<IActionResult> StartDbLogging(string nomeMacchina, string nodeId, string displayName)
        {
            var i = _opcUaService.GetServerInstance(nomeMacchina);
            if (i == null) return NotFound();
            await i.StartDbLogging(nodeId, displayName);
            return Ok();
        }

        [HttpPost("OpcUa/Stop/DbLogging")]
        public async Task<IActionResult> StopDbLogging(string nomeMacchina, string nodeId)
        {
            var i = _opcUaService.GetServerInstance(nomeMacchina);
            if (i == null) return NotFound();
            await i.StopDbLogging(nodeId);
            return Ok();
        }

        [HttpPost("OpcUa/Start/TelegramAlarming")]
        public async Task<IActionResult> StartTelegramAlarming(string nomeMacchina, string nodeId, string displayName)
        {
            var i = _opcUaService.GetServerInstance(nomeMacchina);
            if (i == null) return NotFound();
            await i.StartTelegramAlarming(nodeId, displayName);
            return Ok();
        }

        [HttpPost("OpcUa/Stop/TelegramAlarming")]
        public async Task<IActionResult> StopTelegramAlarming(string nomeMacchina, string nodeId)
        {
            var i = _opcUaService.GetServerInstance(nomeMacchina);
            if (i == null) return NotFound();
            await i.StopTelegramAlarming(nodeId);
            return Ok();
        }
    }

    public class ReadValuesRequest { public string NomeMacchina { get; set; } public System.Collections.Generic.List<string> NodeIds { get; set; } }
}
