using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online.Models
{
    // Ho aggiornato questo modello per corrispondere alla nuova struttura della tabella che mi hai mostrato.
    [Table("connessioni")]
    public class Connessione
    {
        // La chiave primaria è ora NomeMacchina, come da tua immagine.
        [Key]
        [Column("NomeMacchina")]
        [StringLength(512)]
        public string NomeMacchina { get; set; }

        // Ho rinominato la proprietà per corrispondere alla colonna "IP_Address".
        [Required]
        [Column("IP_Address")]
        [StringLength(512)]
        public string IP_Address { get; set; }

        [Required]
        [Column("Porta")]
        public int Porta { get; set; }

        // Ho reso il campo "Tipo" obbligatorio (Not Null) come nello schema.
        [Required]
        [Column("Tipo")]
        [StringLength(45)]
        public string Tipo { get; set; }

        // La colonna "Attivo" non è presente nel nuovo schema, quindi l'ho rimossa.
        // La colonna "ID" è stata sostituita da "NomeMacchina" come chiave primaria.
    }
}
