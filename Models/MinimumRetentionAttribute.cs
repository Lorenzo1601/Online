using System.ComponentModel.DataAnnotations;

namespace Online.Models
{
    public class MinimumRetentionAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (validationContext.ObjectInstance is not SettingsViewModel model)
            {
                return new ValidationResult("ViewModel non valido.");
            }

            if (model.RetentionDays <= 0 && model.RetentionHours <= 0 && model.RetentionMinutes <= 0)
            {
                return new ValidationResult("Il periodo di conservazione totale deve essere di almeno un minuto.");
            }

            return ValidationResult.Success;
        }
    }
}
