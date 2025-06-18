using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online.Models
{
    /// <summary>
    /// Represents a single parameter (tag) associated with a recipe.
    /// Maps to the 'parametricetta' table in the database.
    /// </summary>
    [Table("parametricetta")]
    public class ParametroRicetta
    {
        [Key, Column(Order = 0)]
        [StringLength(512)]
        public string NomeRicetta { get; set; }

        [Key, Column(Order = 1)]
        [StringLength(512)]
        public string NomeTag { get; set; }

        [Required]
        [StringLength(512)]
        public string NomeMacchina { get; set; }

        [Required]
        [StringLength(2048)]
        public string Connessione { get; set; } // This will store the NodeId from the OPC UA server

        [Required]
        [StringLength(1024)]
        public string Valore { get; set; }
    }
}
