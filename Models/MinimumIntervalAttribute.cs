using System.ComponentModel.DataAnnotations;

namespace Online.Models
{
    public class MinimumIntervalAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext.ObjectInstance is not SettingsViewModel model)
            {
                return new ValidationResult("ViewModel non valido.");
            }

            if (model.CleanupIntervalHours <= 0 && model.CleanupIntervalMinutes <= 0)
            {
                return new ValidationResult("L'intervallo di esecuzione totale deve essere di almeno un minuto.");
            }

            return ValidationResult.Success;
        }
    }
}
