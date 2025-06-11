using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online.Models
{
    [Table("macchine")] // Mappa esplicitamente alla tabella 'macchine'
    public class Macchina
    {
        [Key] // Parte della chiave composita
        [Column("NomeMacchina")] // Mappa al nome della colonna DB
        [StringLength(512)]
        [Display(Name = "Nome Macchina")]
        public string NomeMacchina { get; set; }

        [Key] // Parte della chiave composita
        [Column("IP_Address")] // Mappa al nome della colonna DB
        [StringLength(100)]
        [Display(Name = "Indirizzo IP")]
        public string IP_Address { get; set; }
    }
}
