using System.Collections.Generic;

namespace Online.Models
{
    /// <summary>
    /// ViewModel for the Ricette page. It holds all the necessary data 
    /// required by the view, such as the list of recipes and available machines.
    /// </summary>
    public class RicetteViewModel
    {
        /// <summary>
        /// Gets or sets the list of all recipes to be displayed.
        /// </summary>
        public List<Ricetta> Ricette { get; set; }

        /// <summary>
        /// Gets or sets the list of available machine names (from the 'Connessioni' table).
        /// This is used to populate the dropdowns in the recipe parameters table.
        /// </summary>
        public List<string> MacchineDisponibili { get; set; }
    }
}
