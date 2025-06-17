using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Online.Data;
using Online.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Online.Controllers
{
    public class OpcUaController : Controller
    {
        private readonly OpcUaMultiServerService _opcUaService;
        private readonly ApplicationDbContext _context;

        public OpcUaController(OpcUaMultiServerService opcUaService, ApplicationDbContext context)
        {
            _opcUaService = opcUaService;
            _context = context;
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

            var existingConnection = await _context.Connessioni.FindAsync(nomeMacchina);
            if (existingConnection == null)
            {
                var nuovaConnessione = new Connessione
                {
                    NomeMacchina = nomeMacchina,
                    IP_Address = ipAddress,
                    Porta = port,
                    Tipo = "opcua"
                };
                _context.Connessioni.Add(nuovaConnessione);
                await _context.SaveChangesAsync();
            }

            await _opcUaService.AddAndConnectServerAsync(nomeMacchina, ipAddress, port);
            return RedirectToAction("Connection");
        }

        [HttpPost("OpcUa/EditServer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditServer(ConnessioneEditModel model)
        {
            // Assicurati che il modello ConnessioneEditModel contenga una proprietà
            // 'OriginalNomeMacchina' per gestire la modifica della chiave primaria.
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "I dati per la modifica non sono validi.";
                return RedirectToAction("Connection");
            }
            try
            {
                // Cerca la connessione nel database usando il nome originale.
                var connessioneDaAggiornare = await _context.Connessioni.FindAsync(model.OriginalNomeMacchina);

                if (connessioneDaAggiornare != null)
                {
                    // Controlla se il nome della macchina (chiave primaria) è stato modificato.
                    if (model.OriginalNomeMacchina != model.NomeMacchina)
                    {
                        // --- LOGICA PER CAMBIARE LA CHIAVE PRIMARIA ---

                        // 1. Verifica che il nuovo nome non sia già utilizzato da un'altra macchina.
                        var nuovoNomeEsiste = await _context.Connessioni.AnyAsync(c => c.NomeMacchina == model.NomeMacchina);
                        if (nuovoNomeEsiste)
                        {
                            TempData["ErrorMessage"] = $"Il nome macchina '{model.NomeMacchina}' è già in uso.";
                            return RedirectToAction("Connection");
                        }

                        // 2. Rimuovi la vecchia connessione.
                        _context.Connessioni.Remove(connessioneDaAggiornare);

                        // 3. Crea e aggiungi la nuova connessione con i dati aggiornati.
                        var nuovaConnessione = new Connessione
                        {
                            NomeMacchina = model.NomeMacchina, // Nuovo nome
                            IP_Address = model.IP_Address,
                            Porta = model.Porta,
                            Tipo = connessioneDaAggiornare.Tipo // Mantiene il tipo originale
                        };
                        _context.Connessioni.Add(nuovaConnessione);
                    }
                    else
                    {
                        // --- LOGICA DI AGGIORNAMENTO NORMALE (SENZA CAMBIO CHIAVE) ---
                        connessioneDaAggiornare.IP_Address = model.IP_Address;
                        connessioneDaAggiornare.Porta = model.Porta;
                        _context.Connessioni.Update(connessioneDaAggiornare);
                    }

                    // Salva le modifiche (o la rimozione/aggiunta) nel database.
                    await _context.SaveChangesAsync();
                }

                // Chiama il servizio per aggiornare l'istanza in memoria.
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

            var connessioneDaEliminare = await _context.Connessioni.FindAsync(nomeMacchina);
            if (connessioneDaEliminare != null)
            {
                _context.Connessioni.Remove(connessioneDaEliminare);
                await _context.SaveChangesAsync();
            }

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

    public class ReadValuesRequest
    {
        public string NomeMacchina { get; set; }
        public System.Collections.Generic.List<string> NodeIds { get; set; }
    }
}
