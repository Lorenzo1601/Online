namespace Online.Models
{
    public class CleanupSettings
    {
        public int RetentionDays { get; set; } = 7;
        public int RetentionHours { get; set; } = 0;
        public int RetentionMinutes { get; set; } = 0;

        // Proprietà modificate
        public int CleanupIntervalHours { get; set; } = 1;
        public int CleanupIntervalMinutes { get; set; } = 0;
    }
}
