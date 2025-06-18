using System.ComponentModel.DataAnnotations;

namespace Online.Models
{
    /// <summary>
    /// Represents a recipe in the database.
    /// </summary>
    public class Ricetta
    {
        /// <summary>
        /// The name of the recipe, which also serves as the primary key.
        /// </summary>
        [Key]
        [StringLength(512)]
        public string NomeRicetta { get; set; }
    }
}
