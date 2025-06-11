using System.ComponentModel.DataAnnotations;

namespace Online.Models
{
    public class MacchinaInputModel
    {
        [Required(ErrorMessage = "Il Nome Macchina è obbligatorio.")]
        [StringLength(512)]
        [Display(Name = "Nome Macchina")]
        public string NomeMacchina { get; set; }

        [Required(ErrorMessage = "L'Indirizzo IP è obbligatorio.")]
        [StringLength(100)]
        [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", ErrorMessage = "Formato Indirizzo IP non valido.")]
        [Display(Name = "Indirizzo IP")]
        public string IP_Address { get; set; }
    }
}
