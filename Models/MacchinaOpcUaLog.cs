using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online.Models
{
    [Table("macchineopcua")]
    public class MacchinaOpcUaLog
    {
        // NUOVO: Campo Id per la chiave composita
        [Column("Id")]
        public long Id { get; set; }

        [Column("Nome")]
        [StringLength(512)]
        public string Nome { get; set; }

        [Column("Nodo")]
        [StringLength(1024)]
        public string? Nodo { get; set; }

        [Column("Valore")]
        [StringLength(512)]
        public string? Valore { get; set; }

        [Column("Qualita")]
        [StringLength(45)]
        public string? Qualita { get; set; }

        [Column("Timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
