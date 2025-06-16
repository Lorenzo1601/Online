using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace online.Models
{
    [Table("connessioni")]
    public class Connessione
    {
        [Key] // Definisce NomeMacchina come chiave primaria
        [Column("NomeMacchina")]
        [StringLength(512)]
        public string NomeMacchina { get; set; }

        [Column("IP_Address")]
        [StringLength(512)]
        public string IP_Address { get; set; }

        [Column("Porta")]
        public int Porta { get; set; }
    }
}
