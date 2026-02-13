using System;
using System.Collections.Generic;

namespace CrystalGroupHome.SharedRCL.Models
{
    /// <summary>
    /// Represents a single entry from an MRP log.
    /// </summary>
    public class MrpLogEntry
    {
        /// <summary>
        /// The line number in the original log file.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The original log line text.
        /// </summary>
        public string RawLine { get; set; } = string.Empty;

        /// <summary>
        /// The part number referenced in this entry (if any).
        /// </summary>
        public string? PartNumber { get; set; }

        /// <summary>
        /// The job number referenced in this entry (if any).
        /// </summary>
        public string? JobNumber { get; set; }

        /// <summary>
        /// The date associated with this entry (if any).
        /// </summary>
        public DateTime? Date { get; set; }

        /// <summary>
        /// The quantity associated with this entry (if any).
        /// </summary>
        public decimal? Quantity { get; set; }

        /// <summary>
        /// Whether this entry represents an error.
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// The type of entry (e.g., "Supply", "Demand", "Error", "Part", "Job").
        /// </summary>
        public string EntryType { get; set; } = string.Empty;

        /// <summary>
        /// Additional metadata extracted from the entry.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
