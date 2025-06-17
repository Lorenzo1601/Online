using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Online.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Text;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Threading;
using Online.Data;
using Microsoft.AspNetCore.Hosting;
// Aggiunto per la gestione della cultura (separatori decimali)
using System.Globalization;

namespace Online.Controllers
{
    public class TelegramNotificationRequest
    {
        public string NomeMacchina { get; set; }
        public string IpAddress { get; set; }
        public bool IsNowOnline { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ChartDataPoint
    {
        public string Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class ChartDataViewModel
    {
        public string ChartName { get; set; }
        public List<ChartDataPoint> Data { get; set; }
    }

    public class ChartFileDeleteModel
    {
        public string FileName { get; set; }
    }


    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private static readonly HttpClient _httpClient = new HttpClient();

        private static Dictionary<string, bool> _lastKnownMachineServerStatus = new Dictionary<string, bool>();
        private static readonly object _statusLock = new object();

        private static Queue<TelegramNotificationRequest> _telegramNotificationQueue = new Queue<TelegramNotificationRequest>();
        private static readonly object _queueLock = new object();
        private static Timer _telegramNotificationTimer;
        private const int TELEGRAM_MESSAGE_INTERVAL_MS = 1500;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment hostingEnvironment)
        {
            _logger = logger;
            _context = context;
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;

            if (_telegramNotificationTimer == null)
            {
                _logger.LogInformation("Inizializzazione del Timer per le notifiche Telegram.");
                _telegramNotificationTimer = new Timer(ProcessTelegramNotificationQueueCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(TELEGRAM_MESSAGE_INTERVAL_MS));
            }
        }

        private async void ProcessTelegramNotificationQueueCallback(object state)
        {
            TelegramNotificationRequest requestToProcess = null;
            bool hasItem = false;

            lock (_queueLock)
            {
                if (_telegramNotificationQueue.Count > 0)
                {
                    requestToProcess = _telegramNotificationQueue.Dequeue();
                    hasItem = true;
                }
            }

            if (hasItem && requestToProcess != null)
            {
                try
                {
                    _logger.LogInformation("Processo notifica Telegram dalla coda per {NomeMacchina} ({IpAddress}), stato online: {IsOnline}",
                        requestToProcess.NomeMacchina, requestToProcess.IpAddress, requestToProcess.IsNowOnline);

                    bool success = await SendTelegramNotification(requestToProcess.NomeMacchina, requestToProcess.IpAddress, requestToProcess.IsNowOnline);

                    if (success)
                    {
                        lock (_statusLock)
                        {
                            _lastKnownMachineServerStatus[requestToProcess.IpAddress] = requestToProcess.IsNowOnline;
                            _logger.LogInformation("Nuovo stato {IsNowOnline} memorizzato per {IpAddress} dopo invio notifica da coda.",
                                requestToProcess.IsNowOnline, requestToProcess.IpAddress);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Fallito invio notifica da coda per {NomeMacchina} ({IpAddress}).",
                                        requestToProcess.NomeMacchina, requestToProcess.IpAddress);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Eccezione durante il processamento della notifica Telegram dalla coda per {NomeMacchina} ({IpAddress}).",
                                        requestToProcess.NomeMacchina, requestToProcess.IpAddress);
                }
            }
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Caricamento pagina Index.");
            var macchine = await _context.Macchine.OrderBy(m => m.NomeMacchina).ToListAsync();
            var viewModel = new IndexViewModel
            {
                Macchine = macchine,
                NuovaMacchina = new MacchinaInputModel()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "NuovaMacchina")] MacchinaInputModel inputModel)
        {
            _logger.LogInformation("Tentativo di creazione macchina: Nome={NomeMacchina}, IP={IPAddress}", inputModel.NomeMacchina, inputModel.IP_Address);
            if (ModelState.IsValid)
            {
                try
                {
                    var existingMacchina = await _context.Macchine
                        .FirstOrDefaultAsync(m => m.NomeMacchina == inputModel.NomeMacchina && m.IP_Address == inputModel.IP_Address);
                    if (existingMacchina != null)
                    {
                        TempData["ErrorMessage"] = "Una macchina con questo Nome e Indirizzo IP esiste già.";
                    }
                    else
                    {
                        var macchina = new Macchina { NomeMacchina = inputModel.NomeMacchina, IP_Address = inputModel.IP_Address };
                        _context.Add(macchina);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Macchina aggiunta con successo!";
                        lock (_statusLock) { _lastKnownMachineServerStatus.Remove(macchina.IP_Address); }
                        return RedirectToAction(nameof(Index));
                    }
                }
                catch (Exception ex) { TempData["ErrorMessage"] = "Errore durante l'aggiunta: " + ex.Message; _logger.LogError(ex, "Errore in Create"); }
            }
            else { TempData["ErrorMessage"] = "Errore nei dati inseriti per la nuova macchina."; }
            var macchineList = await _context.Macchine.ToListAsync();
            var viewModel = new IndexViewModel { Macchine = macchineList, NuovaMacchina = inputModel };
            return View("Index", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind(Prefix = "modalEditModel")] MacchinaEditModel editModel)
        {
            _logger.LogInformation("Tentativo di modifica macchina: Originale({OriginalNomeMacchina}, {OriginalIPAddress}) -> Nuovo({NomeMacchina}, {IPAddress})",
                editModel.OriginalNomeMacchina, editModel.OriginalIP_Address, editModel.NomeMacchina, editModel.IP_Address);

            if (ModelState.IsValid)
            {
                var macchinaToUpdate = await _context.Macchine
                                                    .AsNoTracking()
                                                    .FirstOrDefaultAsync(m => m.NomeMacchina == editModel.OriginalNomeMacchina && m.IP_Address == editModel.OriginalIP_Address);

                if (macchinaToUpdate == null)
                {
                    TempData["ErrorMessage"] = "Macchina originale non trovata per la modifica.";
                    _logger.LogWarning("Macchina originale non trovata: {OriginalNomeMacchina}, {OriginalIPAddress}", editModel.OriginalNomeMacchina, editModel.OriginalIP_Address);
                    return RedirectToAction(nameof(Index));
                }

                bool pkNomeMacchinaChanged = macchinaToUpdate.NomeMacchina != editModel.NomeMacchina;
                bool pkIpAddressChanged = macchinaToUpdate.IP_Address != editModel.IP_Address;
                bool pkChanged = pkNomeMacchinaChanged || pkIpAddressChanged;
                string oldIpForStatusReset = macchinaToUpdate.IP_Address;

                if (pkChanged)
                {
                    _logger.LogInformation("La chiave primaria è cambiata. Procedo con rimozione e aggiunta.");
                    var existingWithNewKey = await _context.Macchine
                                                            .AsNoTracking()
                                                            .FirstOrDefaultAsync(m => m.NomeMacchina == editModel.NomeMacchina && m.IP_Address == editModel.IP_Address);

                    if (existingWithNewKey != null)
                    {
                        _logger.LogWarning("Nuova chiave duplicata durante modifica: {NomeMacchina}, {IPAddress}", editModel.NomeMacchina, editModel.IP_Address);
                        ModelState.AddModelError(string.Empty, "Una macchina con il novo Nome e Indirizzo IP specificati esiste già.");
                        TempData["ErrorMessage"] = "Una macchina con il nuovo Nome e Indirizzo IP specificati esiste già.";
                        TempData["EditModel_OriginalNomeMacchina"] = editModel.OriginalNomeMacchina;
                        TempData["EditModel_OriginalIP_Address"] = editModel.OriginalIP_Address;
                        TempData["EditModel_NomeMacchina"] = editModel.NomeMacchina;
                        TempData["EditModel_IP_Address"] = editModel.IP_Address;
                        TempData["EditErrors_Json"] = JsonSerializer.Serialize(ModelState.ToSerializableDictionary());
                        TempData["OpenEditModal"] = true;
                        return RedirectToAction(nameof(Index));
                    }

                    var entityToRemove = new Macchina { NomeMacchina = editModel.OriginalNomeMacchina, IP_Address = editModel.OriginalIP_Address };
                    _context.Entry(entityToRemove).State = EntityState.Deleted;

                    var nuovaMacchina = new Macchina { NomeMacchina = editModel.NomeMacchina, IP_Address = editModel.IP_Address };
                    _context.Macchine.Add(nuovaMacchina);
                }
                else
                {
                    _logger.LogInformation("Chiave primaria non cambiata.");
                }

                try
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Macchina aggiornata con successo!";
                    _logger.LogInformation("Macchina aggiornata con successo.");

                    if (pkIpAddressChanged)
                    {
                        lock (_statusLock)
                        {
                            if (!string.IsNullOrEmpty(oldIpForStatusReset)) _lastKnownMachineServerStatus.Remove(oldIpForStatusReset);
                            _lastKnownMachineServerStatus.Remove(editModel.IP_Address);
                        }
                        _logger.LogInformation("Stato ping resettato per IP vecchio '{OldIP}' e nuovo '{NewIP}'.", oldIpForStatusReset, editModel.IP_Address);
                    }
                    else if (pkNomeMacchinaChanged)
                    {
                        _logger.LogInformation("Solo il nome macchina è cambiato, stato ping per IP '{IP}' non resettato.", editModel.IP_Address);
                    }

                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "DbUpdateException durante l'aggiornamento in Edit.");
                    TempData["ErrorMessage"] = "Errore database durante l'aggiornamento. Controllare i log per dettagli (possibile violazione di chiave).";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore generico durante l'aggiornamento in Edit.");
                    TempData["ErrorMessage"] = "Errore generico durante l'aggiornamento della macchina.";
                }
                return RedirectToAction(nameof(Index));
            }

            _logger.LogWarning("ModelState NON è valido per la modifica.");
            TempData["EditModel_OriginalNomeMacchina"] = editModel.OriginalNomeMacchina;
            TempData["EditModel_OriginalIP_Address"] = editModel.OriginalIP_Address;
            TempData["EditModel_NomeMacchina"] = editModel.NomeMacchina;
            TempData["EditModel_IP_Address"] = editModel.IP_Address;
            TempData["EditErrors_Json"] = JsonSerializer.Serialize(ModelState.ToSerializableDictionary());
            TempData["OpenEditModal"] = true;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string nomeMacchina, string ipAddress)
        {
            _logger.LogInformation("Tentativo di eliminazione macchina: {NomeMacchina}, {IPAddress}", nomeMacchina, ipAddress);
            var macchina = await _context.Macchine.FirstOrDefaultAsync(m => m.NomeMacchina == nomeMacchina && m.IP_Address == ipAddress);
            if (macchina == null) { TempData["ErrorMessage"] = "Macchina non trovata."; return RedirectToAction(nameof(Index)); }
            try
            {
                _context.Macchine.Remove(macchina);
                await _context.SaveChangesAsync();
                lock (_statusLock) { _lastKnownMachineServerStatus.Remove(ipAddress); }
                TempData["SuccessMessage"] = "Macchina eliminata con successo.";
            }
            catch (Exception ex) { TempData["ErrorMessage"] = "Errore durante l'eliminazione: " + ex.Message; _logger.LogError(ex, "Errore in Delete"); }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> PingIpAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return BadRequest(new { reachable = false, error = "Indirizzo IP non fornito." });
            bool isReachable = false; string errorMessage = null;
            try
            {
                using (Ping pingSender = new Ping())
                {
                    PingReply reply = await pingSender.SendPingAsync(ipAddress, 1500);
                    isReachable = (reply.Status == IPStatus.Success);
                    if (!isReachable) errorMessage = reply.Status.ToString();
                }
            }
            catch (Exception ex) { errorMessage = ex.Message; _logger.LogError(ex, "Errore PingIpAddress per {IP}", ipAddress); }
            return Json(new { reachable = isReachable, error = errorMessage });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult NotifyTelegramOfStatusChange([FromBody] StatusChangeNotificationModel notification)
        {
            if (notification == null || string.IsNullOrWhiteSpace(notification.IpAddress) || string.IsNullOrWhiteSpace(notification.NomeMacchina))
            {
                return BadRequest(new { success = false, message = "Dati di notifica non validi." });
            }
            _logger.LogInformation("Ricevuta richiesta notifica per {NomeMacchina} ({IpAddress}), online: {IsNowOnline}",
                notification.NomeMacchina, notification.IpAddress, notification.IsNowOnline);

            bool previousServerStatusKnown; bool previousServerOnlineStatus = false;
            lock (_statusLock)
            {
                previousServerStatusKnown = _lastKnownMachineServerStatus.TryGetValue(notification.IpAddress, out previousServerOnlineStatus);
            }

            if (!previousServerStatusKnown || previousServerOnlineStatus != notification.IsNowOnline)
            {
                _logger.LogInformation("Accodamento notifica Telegram per {NomeMacchina} ({IpAddress}).", notification.NomeMacchina, notification.IpAddress);
                lock (_queueLock)
                {
                    _telegramNotificationQueue.Enqueue(new TelegramNotificationRequest
                    {
                        NomeMacchina = notification.NomeMacchina,
                        IpAddress = notification.IpAddress,
                        IsNowOnline = notification.IsNowOnline,
                        Timestamp = DateTime.UtcNow
                    });
                }
                return Ok(new { success = true, message = "Notifica accodata." });
            }
            else
            {
                return Ok(new { success = false, message = "Nessun cambio di stato rilevante." });
            }
        }

        private async Task<bool> SendTelegramNotification(string nomeMacchina, string ipAddress, bool isOnline)
        {
            string botToken = _configuration["Telegram:BotToken"];
            string chatId = _configuration["Telegram:ChatId"];
            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId) || botToken == "IL_TUO_BOT_TOKEN_TELEGRAM" || chatId == "IL_TUO_CHAT_ID_TELEGRAM")
            {
                _logger.LogWarning("Token/ChatID Telegram non configurati."); return false;
            }
            string statusEmoji = isOnline ? "✅" : "❌"; string statusText = isOnline ? "ONLINE" : "OFFLINE";
            string message = $"{statusEmoji} *Stato Macchina Aggiornato*\n\n*Macchina*: {EscapeMarkdownV2(nomeMacchina)}\n*IP*: `{ipAddress}`\n*Nuovo Stato*: _{statusText}_";
            if (!isOnline) message += $"\n\n_Segnalato il: {DateTime.Now:dd/MM/yyyy HH:mm:ss}_";
            string requestUri = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = new { chat_id = chatId, text = message, parse_mode = "MarkdownV2" };
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(requestUri, content);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode) { _logger.LogInformation("Notifica Telegram inviata per {IP}", ipAddress); return true; }
                else { _logger.LogError("Errore invio Telegram per {IP}. HTTP: {Code}. Resp: {Resp}", ipAddress, response.StatusCode, responseContent); return false; }
            }
            catch (Exception ex) { _logger.LogError(ex, "Eccezione invio Telegram per {IP}", ipAddress); return false; }
        }

        private string EscapeMarkdownV2(string text) { if (string.IsNullOrEmpty(text)) return string.Empty; var specialChars = new[] { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" }; foreach (var c in specialChars) text = text.Replace(c, "\\" + c); return text; }

        private string EscapeCsvField(string field) { if (string.IsNullOrEmpty(field)) return string.Empty; if (field.Contains(";") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r")) return $"\"{field.Replace("\"", "\"\"")}\""; return field; }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportFromCsv(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0) { TempData["ErrorMessage"] = "Nessun file."; return RedirectToAction(nameof(Index)); }
            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) { TempData["ErrorMessage"] = "Solo file .csv"; return RedirectToAction(nameof(Index)); }
            var added = new List<Macchina>(); int read = 0, imported = 0, duplicates = 0, formatErrors = 0;
            try
            {
                using (var reader = new StreamReader(csvFile.OpenReadStream()))
                {
                    string header = await reader.ReadLineAsync(); read++;
                    if (header == null || !header.Trim().Equals("NomeMacchina,IP_Address", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["ErrorMessage"] = "Intestazione CSV errata."; return RedirectToAction(nameof(Index));
                    }
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        read++; if (string.IsNullOrWhiteSpace(line)) continue;
                        var v = line.Split(',');
                        if (v.Length == 2)
                        {
                            string nome = v[0].Trim().Trim('"'); string ip = v[1].Trim().Trim('"');
                            if (string.IsNullOrWhiteSpace(nome) || nome.Length > 512 || string.IsNullOrWhiteSpace(ip) || ip.Length > 100)
                            {
                                formatErrors++; continue;
                            }
                            if (await _context.Macchine.AnyAsync(m => m.NomeMacchina == nome && m.IP_Address == ip) || added.Any(m => m.NomeMacchina == nome && m.IP_Address == ip))
                            {
                                duplicates++; continue;
                            }
                            added.Add(new Macchina { NomeMacchina = nome, IP_Address = ip });
                        }
                        else { formatErrors++; }
                    }
                }
                if (added.Any())
                {
                    _context.Macchine.AddRange(added); await _context.SaveChangesAsync(); imported = added.Count;
                    foreach (var m in added) lock (_statusLock) _lastKnownMachineServerStatus.Remove(m.IP_Address);
                }
                TempData["SuccessMessage"] = $"Import: {imported} aggiunte, {duplicates} duplicati, {formatErrors} errori formato.";
            }
            catch (Exception ex) { TempData["ErrorMessage"] = "Errore import: " + ex.Message; _logger.LogError(ex, "Errore ImportFromCsv"); }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult OpcUa()
        {
            _logger.LogInformation("Caricamento pagina OpcUa.");
            return View();
        }

        public async Task<IActionResult> Storico(string nomeMacchina, string searchString, DateTime? dataInizio, DateTime? dataFine)
        {
            _logger.LogInformation("Caricamento pagina Storico con filtri: Macchina={NomeMacchina}, Ricerca={SearchString}, Da={DataInizio}, A={DataFine}", nomeMacchina, searchString, dataInizio, dataFine);

            ViewData["CurrentFilter"] = searchString;
            ViewData["SelectedMacchina"] = nomeMacchina;
            ViewData["DataInizio"] = dataInizio.HasValue ? dataInizio.Value.ToString("yyyy-MM-ddTHH:mm") : "";
            ViewData["DataFine"] = dataFine.HasValue ? dataFine.Value.ToString("yyyy-MM-ddTHH:mm") : "";

            var macchineList = await _context.Connessioni
                                            .Select(c => c.NomeMacchina)
                                            .Distinct()
                                            .OrderBy(nome => nome)
                                            .ToListAsync();
            ViewData["Macchine"] = macchineList;

            var logsQuery = _context.MacchineOpcUaLog.AsQueryable();

            if (!string.IsNullOrEmpty(nomeMacchina))
            {
                logsQuery = logsQuery.Where(l => l.NomeMacchina == nomeMacchina);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                logsQuery = logsQuery.Where(l => l.Nome.Contains(searchString));
            }

            if (dataInizio.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.Timestamp >= dataInizio.Value);
            }

            if (dataFine.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.Timestamp <= dataFine.Value);
            }

            var filteredLogs = await logsQuery.OrderByDescending(l => l.Timestamp).ToListAsync();

            return View(filteredLogs);
        }


        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string nomeMacchina, string searchString, DateTime? dataInizio, DateTime? dataFine)
        {
            var logsQuery = _context.MacchineOpcUaLog.AsQueryable();
            if (!string.IsNullOrEmpty(nomeMacchina)) { logsQuery = logsQuery.Where(l => l.NomeMacchina == nomeMacchina); }
            if (!string.IsNullOrEmpty(searchString)) { logsQuery = logsQuery.Where(l => l.Nome.Contains(searchString)); }
            if (dataInizio.HasValue) { logsQuery = logsQuery.Where(l => l.Timestamp >= dataInizio.Value); }
            if (dataFine.HasValue) { logsQuery = logsQuery.Where(l => l.Timestamp <= dataFine.Value); }
            var data = await logsQuery.OrderByDescending(l => l.Timestamp).ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Nome Tag;Nodo;Valore;Qualita;Timestamp");

            foreach (var item in data)
            {
                builder.AppendLine($"{EscapeCsvField(item.Nome)};{EscapeCsvField(item.Nodo)};{EscapeCsvField(item.Valore)};{EscapeCsvField(item.Qualita)};{item.Timestamp:dd/MM/yyyy HH:mm:ss}");
            }

            string fileName = $"Storico_{DateTime.Now:yyyyMMddHHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ExportToPdf(string nomeMacchina, string searchString, DateTime? dataInizio, DateTime? dataFine)
        {
            var logsQuery = _context.MacchineOpcUaLog.AsQueryable();
            if (!string.IsNullOrEmpty(nomeMacchina)) { logsQuery = logsQuery.Where(l => l.NomeMacchina == nomeMacchina); }
            if (!string.IsNullOrEmpty(searchString)) { logsQuery = logsQuery.Where(l => l.Nome.Contains(searchString)); }
            if (dataInizio.HasValue) { logsQuery = logsQuery.Where(l => l.Timestamp >= dataInizio.Value); }
            if (dataFine.HasValue) { logsQuery = logsQuery.Where(l => l.Timestamp <= dataFine.Value); }
            var data = await logsQuery.OrderByDescending(l => l.Timestamp).ToListAsync();

            string fileName = $"Storico_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            return new Rotativa.AspNetCore.ViewAsPdf("_StoricoPdf", data)
            {
                FileName = fileName,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        [HttpPost]
        public async Task<IActionResult> SaveChartData([FromBody] ChartDataViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ChartName) || model.Data == null || !model.Data.Any())
            {
                return Json(new { success = false, message = "Dati non validi." });
            }

            try
            {
                var fileName = string.Concat(model.ChartName.Split(Path.GetInvalidFileNameChars()));

                var directoryPath = Path.Combine(_hostingEnvironment.WebRootPath, "Grafici");

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var filePath = Path.Combine(directoryPath, $"{fileName}.txt");

                var fileContent = new StringBuilder();
                fileContent.AppendLine($"Nome Grafico: {model.ChartName}");
                fileContent.AppendLine("--- Dati ---");
                foreach (var point in model.Data)
                {
                    // Utilizza CultureInfo.InvariantCulture per garantire che il separatore decimale sia sempre un punto '.'
                    fileContent.AppendLine($"Timestamp: {point.Timestamp}, Valore: {point.Value.ToString(CultureInfo.InvariantCulture)}");
                }

                await System.IO.File.WriteAllTextAsync(filePath, fileContent.ToString());

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il salvataggio del file del grafico.");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetSavedCharts()
        {
            try
            {
                var directoryPath = Path.Combine(_hostingEnvironment.WebRootPath, "Grafici");
                if (!Directory.Exists(directoryPath))
                {
                    return Json(new { success = true, files = new List<string>() });
                }

                var fileNames = Directory.GetFiles(directoryPath, "*.txt")
                                         .Select(Path.GetFileName)
                                         .ToList();

                return Json(new { success = true, files = fileNames });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recuperare la lista dei grafici salvati.");
                return Json(new { success = false, message = "Errore nel leggere la lista dei grafici." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetChartDataFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("Nome file non fornito.");
            }

            try
            {
                var safeFileName = Path.GetFileName(fileName);
                var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "Grafici", safeFileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("File non trovato.");
                }

                var lines = await System.IO.File.ReadAllLinesAsync(filePath);
                var dataPoints = new List<ChartDataPoint>();

                foreach (var line in lines.Skip(2))
                {
                    var parts = line.Split(',');
                    if (parts.Length == 2)
                    {
                        var timestampStr = parts[0].Replace("Timestamp:", "").Trim();
                        var valueStr = parts[1].Replace("Valore:", "").Trim();

                        // Utilizza CultureInfo.InvariantCulture per interpretare correttamente il punto '.' come separatore decimale
                        if (double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                        {
                            dataPoints.Add(new ChartDataPoint { Timestamp = timestampStr, Value = value });
                        }
                    }
                }
                return Json(new { success = true, data = dataPoints });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel leggere il file del grafico {FileName}", fileName);
                return Json(new { success = false, message = "Errore nella lettura del file." });
            }
        }

        [HttpPost]
        public IActionResult DeleteChartFile([FromBody] ChartFileDeleteModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.FileName))
            {
                return BadRequest(new { success = false, message = "Nome del file non fornito." });
            }

            try
            {
                var fileName = Path.GetFileName(model.FileName);
                var directoryPath = Path.Combine(_hostingEnvironment.WebRootPath, "Grafici");
                var filePath = Path.Combine(directoryPath, fileName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation("File grafico eliminato: {FileName}", fileName);
                    return Json(new { success = true });
                }
                else
                {
                    _logger.LogWarning("Tentativo di eliminare un file non esistente: {FileName}", fileName);
                    return NotFound(new { success = false, message = "File non trovato." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'eliminazione del file grafico: {FileName}", model.FileName);
                return StatusCode(500, new { success = false, message = "Si è verificato un errore interno durante l'eliminazione." });
            }
        }


        private bool MacchinaExists(string nomeMacchina, string ipAddress) { return _context.Macchine.Any(e => e.NomeMacchina == nomeMacchina && e.IP_Address == ipAddress); }
        public IActionResult Privacy() { return View(); }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() { return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier }); }
    }

    public class StatusChangeNotificationModel
    {
        public string NomeMacchina { get; set; }
        public string IpAddress { get; set; }
        public bool IsNowOnline { get; set; }
    }

    public static class ModelStateExtensions
    {
        public static Dictionary<string, string[]> ToSerializableDictionary(this ModelStateDictionary modelState)
        {
            return modelState
                .Where(x => x.Value.Errors.Any())
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
        }
    }
}
