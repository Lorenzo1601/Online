using Microsoft.AspNetCore.Mvc;
using Online.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Online.Controllers
{
    public class SettingsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(IConfiguration configuration, IWebHostEnvironment env, ILogger<SettingsController> logger)
        {
            _configuration = configuration;
            _env = env;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveSettings(SettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["Error"] = "Dati non validi: " + string.Join(" ", errorMessages);
            }
            else
            {
                try
                {
                    string appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                    var jsonString = System.IO.File.ReadAllText(appSettingsPath);
                    var jsonObj = JsonNode.Parse(jsonString)!.AsObject();

                    var cleanupSettingsNode = jsonObj["CleanupSettings"]?.AsObject() ?? new JsonObject();
                    jsonObj["CleanupSettings"] = cleanupSettingsNode;

                    // Salva le impostazioni di conservazione
                    cleanupSettingsNode["RetentionDays"] = model.RetentionDays;
                    cleanupSettingsNode["RetentionHours"] = model.RetentionHours;
                    cleanupSettingsNode["RetentionMinutes"] = model.RetentionMinutes;

                    // Salva le impostazioni dell'intervallo
                    cleanupSettingsNode["CleanupIntervalHours"] = model.CleanupIntervalHours;
                    cleanupSettingsNode["CleanupIntervalMinutes"] = model.CleanupIntervalMinutes;

                    // Rimuove la vecchia chiave se presente
                    if (cleanupSettingsNode.ContainsKey("CleanupTime"))
                    {
                        cleanupSettingsNode.Remove("CleanupTime");
                    }

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var updatedJsonString = jsonObj.ToJsonString(options);
                    System.IO.File.WriteAllText(appSettingsPath, updatedJsonString);

                    TempData["Success"] = "Impostazioni salvate. Riavviare l'applicazione per renderle effettive.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante il salvataggio di appsettings.json");
                    TempData["Error"] = $"Si è verificato un errore: {ex.Message}";
                }
            }

            return RedirectToAction("OpcUa", "Home");
        }
    }
}
