using System.ComponentModel.DataAnnotations;

namespace Online.Models
{
    public class ConnessioneEditModel
    {
        [Required]
        public string OriginalNomeMacchina { get; set; }

        [Required(ErrorMessage = "Il nome della macchina è obbligatorio.")]
        [StringLength(512)]
        public string NomeMacchina { get; set; }

        [Required(ErrorMessage = "L'indirizzo IP è obbligatorio.")]
        [StringLength(512)]
        public string IP_Address { get; set; }

        [Required(ErrorMessage = "La porta è obbligatoria.")]
        [Range(1, 65535, ErrorMessage = "La porta deve essere un numero valido.")]
        public int Porta { get; set; }
    }
}
