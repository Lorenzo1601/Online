using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Online.Services
{
    // Oggetto per memorizzare le informazioni di un singolo nodo monitorato
    public class MonitoredNodeInfo
    {
        public string NodeId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    // Oggetto per contenere la configurazione di un singolo server
    public class ServerMonitoringConfig
    {
        public ConcurrentDictionary<string, MonitoredNodeInfo> DbLoggingNodes { get; set; } = new();
        public ConcurrentDictionary<string, MonitoredNodeInfo> TelegramAlarmingNodes { get; set; } = new();
    }

    /// <summary>
    /// Servizio per gestire la persistenza della configurazione di monitoraggio.
    /// Salva e carica le impostazioni da un file JSON per renderle permanenti.
    /// Questo servizio deve essere registrato come Singleton.
    /// </summary>
    public class MonitoringConfigService
    {
        private readonly string _configFilePath;
        private readonly ConcurrentDictionary<string, ServerMonitoringConfig> _serverConfigs;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public MonitoringConfigService()
        {
            // Definisce il percorso del file di configurazione nella cartella dell'applicazione
            _configFilePath = Path.Combine(AppContext.BaseDirectory, "monitoring_config.json");
            _serverConfigs = LoadConfig();
        }

        /// <summary>
        /// Carica la configurazione dal file JSON. Se il file non esiste, ne crea una vuota.
        /// </summary>
        private ConcurrentDictionary<string, ServerMonitoringConfig> LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    return JsonSerializer.Deserialize<ConcurrentDictionary<string, ServerMonitoringConfig>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante la lettura del file di configurazione del monitoraggio: {ex.Message}");
            }
            return new();
        }

        /// <summary>
        /// Salva la configurazione corrente su file JSON in modo asincrono e thread-safe.
        /// </summary>
        private async Task SaveConfigAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(_serverConfigs, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante il salvataggio del file di configurazione del monitoraggio: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Ottiene la configurazione per un server specifico. Se non esiste, la crea.
        /// </summary>
        public ServerMonitoringConfig GetConfigForServer(string machineName)
        {
            return _serverConfigs.GetOrAdd(machineName, new ServerMonitoringConfig());
        }

        public async Task AddDbLoggingNodeAsync(string machineName, string nodeId, string displayName)
        {
            var serverConfig = GetConfigForServer(machineName);
            serverConfig.DbLoggingNodes.TryAdd(nodeId, new MonitoredNodeInfo { NodeId = nodeId, DisplayName = displayName });
            await SaveConfigAsync();
        }

        public async Task RemoveDbLoggingNodeAsync(string machineName, string nodeId)
        {
            var serverConfig = GetConfigForServer(machineName);
            if (serverConfig.DbLoggingNodes.TryRemove(nodeId, out _))
            {
                await SaveConfigAsync();
            }
        }

        public async Task AddTelegramAlarmingNodeAsync(string machineName, string nodeId, string displayName)
        {
            var serverConfig = GetConfigForServer(machineName);
            serverConfig.TelegramAlarmingNodes.TryAdd(nodeId, new MonitoredNodeInfo { NodeId = nodeId, DisplayName = displayName });
            await SaveConfigAsync();
        }

        public async Task RemoveTelegramAlarmingNodeAsync(string machineName, string nodeId)
        {
            var serverConfig = GetConfigForServer(machineName);
            if (serverConfig.TelegramAlarmingNodes.TryRemove(nodeId, out _))
            {
                await SaveConfigAsync();
            }
        }

        /// <summary>
        /// Rimuove tutta la configurazione per un server, utile quando un server viene eliminato.
        /// </summary>
        public async Task RemoveServerConfigAsync(string machineName)
        {
            if (_serverConfigs.TryRemove(machineName, out _))
            {
                await SaveConfigAsync();
            }
        }
    }
}
