using System.Collections.Generic;
using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.SharedRCL.Analysis
{
    /// <summary>
    /// Represents a difference detected between two MRP log runs.
    /// </summary>
    public class MrpDifference
    {
        /// <summary>
        /// The type of difference detected.
        /// </summary>
        public DifferenceType Type { get; set; }

        /// <summary>
        /// The part number associated with this difference.
        /// </summary>
        public string PartNumber { get; set; } = string.Empty;

        /// <summary>
        /// The job number associated with this difference (if applicable).
        /// </summary>
        public string? JobNumber { get; set; }

        /// <summary>
        /// Human-readable description of the difference.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The entry from Run A (null if not applicable).
        /// </summary>
        public MrpLogEntry? RunAEntry { get; set; }

        /// <summary>
        /// The entry from Run B (null if not applicable).
        /// </summary>
        public MrpLogEntry? RunBEntry { get; set; }

        /// <summary>
        /// The severity level of this difference.
        /// </summary>
        public DifferenceSeverity Severity { get; set; }

        /// <summary>
        /// Additional details about the difference.
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// The type of difference detected in an MRP log comparison.
    /// </summary>
    public enum DifferenceType
    {
        /// <summary>
        /// A job was added in Run B that wasn't in Run A.
        /// </summary>
        JobAdded,

        /// <summary>
        /// A job was present in Run A but missing in Run B.
        /// </summary>
        JobRemoved,

        /// <summary>
        /// A date was shifted between runs.
        /// </summary>
        DateShifted,

        /// <summary>
        /// A quantity changed between runs.
        /// </summary>
        QuantityChanged,

        /// <summary>
        /// An error appeared in Run B that wasn't in Run A.
        /// </summary>
        ErrorAppeared,

        /// <summary>
        /// An error was resolved (present in Run A but not in Run B).
        /// </summary>
        ErrorResolved,

        /// <summary>
        /// A part appeared in Run B that wasn't in Run A.
        /// </summary>
        PartAppeared,

        /// <summary>
        /// A part was removed (present in Run A but not in Run B).
        /// </summary>
        PartRemoved
    }

    /// <summary>
    /// The severity level of a difference.
    /// </summary>
    public enum DifferenceSeverity
    {
        /// <summary>
        /// Informational difference, low priority.
        /// </summary>
        Info,

        /// <summary>
        /// Warning level difference, moderate priority.
        /// </summary>
        Warning,

        /// <summary>
        /// Critical difference, high priority.
        /// </summary>
        Critical
    }
}
