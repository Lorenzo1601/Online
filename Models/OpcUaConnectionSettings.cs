namespace Online.Models
{
    /// <summary>
    /// Mantiene le ultime impostazioni di connessione usate per l'OPC UA.
    /// </summary>
    public class OpcUaConnectionSettings
    {
        public string? LastIpAddress { get; set; }
        public string? LastPort { get; set; }
    }
}
