using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Online.Data;
using Online.Models;

namespace Online
{
    public class DataCleanUpService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DataCleanUpService> _logger;
        private readonly IOptionsMonitor<CleanupSettings> _settingsMonitor;

        public DataCleanUpService(IServiceScopeFactory scopeFactory, ILogger<DataCleanUpService> logger, IOptionsMonitor<CleanupSettings> settingsMonitor)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _settingsMonitor = settingsMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servizio di Pulizia Dati avviato.");
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var currentSettings = _settingsMonitor.CurrentValue;

                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // --- MODIFICA RICHIESTA ---
                        // La data e ora corrente viene presa dal fuso orario del PC dove gira il servizio.
                        var cutoffDate = DateTime.Now
                            .AddDays(-currentSettings.RetentionDays)
                            .AddHours(-currentSettings.RetentionHours)
                            .AddMinutes(-currentSettings.RetentionMinutes);

                        _logger.LogInformation("Esecuzione pulizia: eliminazione dei record antecedenti al {CutoffDate} (Ora Locale del Server).", cutoffDate);

                        var deletedCount = await dbContext.MacchineOpcUaLog
                            .Where(log => log.Timestamp < cutoffDate)
                            .ExecuteDeleteAsync(stoppingToken);

                        if (deletedCount > 0)
                        {
                            _logger.LogInformation("{count} record obsoleti sono stati eliminati.", deletedCount);
                        }
                        else
                        {
                            _logger.LogInformation("Nessun record obsoleto da eliminare trovato.");
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Errore durante il processo di pulizia.");
                }

                try
                {
                    TimeSpan interval = new TimeSpan(currentSettings.CleanupIntervalHours, currentSettings.CleanupIntervalMinutes, 0);

                    if (interval.TotalMinutes < 1)
                    {
                        _logger.LogWarning("L'intervallo di pulizia è inferiore a 1 minuto. Verrà impostato a 1 minuto per sicurezza.");
                        interval = TimeSpan.FromMinutes(1);
                    }

                    _logger.LogInformation("Pulizia completata. Prossima esecuzione tra {interval}.", interval);
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            _logger.LogInformation("Servizio di Pulizia Dati arrestato.");
        }
    }
}
