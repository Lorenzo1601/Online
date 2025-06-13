using System.ComponentModel.DataAnnotations;

namespace Online.Models
{
    // Aggiunti attributi per validare che né il periodo di conservazione né l'intervallo siano zero.
    [MinimumRetention]
    [MinimumInterval]
    public class SettingsViewModel
    {
        // --- Periodo di Conservazione ---
        [Display(Name = "Giorni")]
        [Range(0, 365, ErrorMessage = "I giorni devono essere tra 0 e 365.")]
        public int RetentionDays { get; set; }

        [Display(Name = "Ore")]
        [Range(0, 23, ErrorMessage = "Le ore devono essere tra 0 e 23.")]
        public int RetentionHours { get; set; }

        [Display(Name = "Minuti")]
        [Range(0, 59, ErrorMessage = "I minuti devono essere tra 0 e 59.")]
        public int RetentionMinutes { get; set; }

        // --- Intervallo di Esecuzione (NUOVI CAMPI) ---
        [Display(Name = "Ore")]
        [Range(0, 23, ErrorMessage = "Le ore dell'intervallo devono essere tra 0 e 23.")]
        public int CleanupIntervalHours { get; set; }

        [Display(Name = "Minuti")]
        [Range(0, 59, ErrorMessage = "I minuti dell'intervallo devono essere tra 0 e 59.")]
        public int CleanupIntervalMinutes { get; set; }
    }
}
