using System;
using System.Collections.Generic;

namespace CrystalGroupHome.SharedRCL.Models
{
    /// <summary>
    /// Represents a single parsed line from an MRP log file.
    /// </summary>
    public class MrpLogEntry
    {
        /// <summary>
        /// The timestamp extracted from the log line (HH:mm:ss format).
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// The raw, unparsed line from the log file.
        /// </summary>
        public string RawLine { get; set; } = string.Empty;

        /// <summary>
        /// The type of entry this line represents.
        /// </summary>
        public MrpLogEntryType EntryType { get; set; }

        /// <summary>
        /// The part number extracted from the line, if any.
        /// </summary>
        public string? PartNumber { get; set; }

        /// <summary>
        /// The job number extracted from the line, if any.
        /// </summary>
        public string? JobNumber { get; set; }

        /// <summary>
        /// Demand information extracted from the line, if this is a Demand entry.
        /// </summary>
        public DemandInfo? Demand { get; set; }

        /// <summary>
        /// Supply information extracted from the line, if this is a Supply entry.
        /// </summary>
        public SupplyInfo? Supply { get; set; }

        /// <summary>
        /// Error message extracted from the line, if this is an Error or Warning entry.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The line number in the source file (1-based).
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Additional metadata extracted from the line.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Defines the types of entries that can be found in an MRP log.
    /// </summary>
    public enum MrpLogEntryType
    {
        /// <summary>
        /// Unknown or unrecognized entry type.
        /// </summary>
        Unknown,

        /// <summary>
        /// Line indicating a part is being processed (e.g., "Processing Part:").
        /// </summary>
        ProcessingPart,

        /// <summary>
        /// Line containing demand information (e.g., "Demand: S: ...").
        /// </summary>
        Demand,

        /// <summary>
        /// Line containing supply information (e.g., "Supply: J: ...").
        /// </summary>
        Supply,

        /// <summary>
        /// Line containing pegging information (e.g., "Pegged Qty: ...").
        /// </summary>
        Pegging,

        /// <summary>
        /// Line containing an error message.
        /// </summary>
        Error,

        /// <summary>
        /// Line containing a warning message.
        /// </summary>
        Warning,

        /// <summary>
        /// Line containing system information (e.g., "Building Pegging Demand Master...").
        /// </summary>
        SystemInfo,

        /// <summary>
        /// Line containing only a timestamp (no other significant data).
        /// </summary>
        Timestamp
    }
}
