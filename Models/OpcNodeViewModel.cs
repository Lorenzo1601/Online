namespace Online.Models
{
    /// <summary>
    /// View model to represent an OPC UA node for the frontend.
    /// </summary>
    public class OpcNodeViewModel
    {
        /// <summary>
        /// The NodeId as a string.
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// The display name of the node.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// The value of the node, if it's a variable.
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// The status code of the value.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Indicates if the node has children that can be browsed.
        /// </summary>
        public bool HasChildren { get; set; }

        /// <summary>
        /// The class of the node (e.g., Object, Variable).
        /// </summary>
        public string? NodeClass { get; set; } // <-- PROPRIETÀ MANCANTE AGGIUNTA
    }
}
