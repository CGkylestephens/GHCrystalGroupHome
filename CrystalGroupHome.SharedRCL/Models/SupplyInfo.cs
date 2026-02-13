using System;

namespace CrystalGroupHome.SharedRCL.Models
{
    /// <summary>
    /// Represents supply information extracted from an MRP log entry.
    /// </summary>
    public class SupplyInfo
    {
        /// <summary>
        /// The type of supply (e.g., "J" for Job, "P" for Purchase Order).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The job number associated with this supply.
        /// </summary>
        public string JobNumber { get; set; } = string.Empty;

        /// <summary>
        /// The assembly number (typically 0 for top-level).
        /// </summary>
        public int Assembly { get; set; }

        /// <summary>
        /// The material sequence number.
        /// </summary>
        public int Material { get; set; }

        /// <summary>
        /// The due date for this supply.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// The quantity available.
        /// </summary>
        public decimal Quantity { get; set; }
    }
}
