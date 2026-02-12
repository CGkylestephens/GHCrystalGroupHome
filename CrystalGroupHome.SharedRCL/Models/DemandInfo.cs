using System;

namespace CrystalGroupHome.SharedRCL.Models
{
    /// <summary>
    /// Represents demand information extracted from an MRP log entry.
    /// </summary>
    public class DemandInfo
    {
        /// <summary>
        /// The type of demand (e.g., "S" for Sales, "T" for Transfer).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The order number associated with this demand.
        /// </summary>
        public string Order { get; set; } = string.Empty;

        /// <summary>
        /// The line number within the order.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// The release number for the order line.
        /// </summary>
        public int Release { get; set; }

        /// <summary>
        /// The due date for this demand.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// The quantity required.
        /// </summary>
        public decimal Quantity { get; set; }
    }
}
