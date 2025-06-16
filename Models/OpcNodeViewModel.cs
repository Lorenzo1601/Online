namespace Online.Models
{
    /// <summary>
    /// Rappresenta un singolo nodo OPC UA da visualizzare nell'interfaccia utente.
    /// Contiene tutte le proprietà necessarie per la visualizzazione e l'interazione.
    /// </summary>
    public class OpcNodeViewModel
    {
        public string NodeId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Status { get; set; }
        public bool HasChildren { get; set; }
        public string? NodeClass { get; set; }

        /// <summary>
        /// Indica se il logging su database è attivo per questo nodo.
        /// Questa proprietà viene letta dal frontend per impostare lo stato della spunta.
        /// </summary>
        public bool IsDbLogging { get; set; }

        /// <summary>
        /// Indica se l'allarme Telegram è attivo per questo nodo.
        /// Questa proprietà viene letta dal frontend per impostare lo stato della spunta.
        /// </summary>
        public bool IsTelegramAlarming { get; set; }
    }
}
