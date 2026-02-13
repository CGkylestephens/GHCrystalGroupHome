using System;
using System.Collections.Generic;

namespace CrystalGroupHome.SharedRCL.Models
{
    /// <summary>
    /// Represents a complete parsed MRP log file with metadata and all entries.
    /// </summary>
    public class MrpLogDocument
    {
        /// <summary>
        /// The path to the source log file.
        /// </summary>
        public string SourceFile { get; set; } = string.Empty;

        /// <summary>
        /// The type of MRP run (Regeneration or Net Change).
        /// </summary>
        public MrpRunType RunType { get; set; }

        /// <summary>
        /// The timestamp when the MRP run started.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// The timestamp when the MRP run ended.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// The site name where the MRP run occurred.
        /// </summary>
        public string? Site { get; set; }

        /// <summary>
        /// All parsed log entries from the file.
        /// </summary>
        public List<MrpLogEntry> Entries { get; set; } = new();

        /// <summary>
        /// Parsing errors or warnings encountered during parsing.
        /// </summary>
        public List<string> ParsingErrors { get; set; } = new();

        /// <summary>
        /// The duration of the MRP run, if both start and end times are available.
        /// </summary>
        public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue
            ? EndTime.Value - StartTime.Value
            : null;
    }

    /// <summary>
    /// Defines the types of MRP runs.
    /// </summary>
    public enum MrpRunType
    {
        /// <summary>
        /// Unknown or undetected run type.
        /// </summary>
        Unknown,

        /// <summary>
        /// Full regeneration run (processes all parts).
        /// </summary>
        Regeneration,

        /// <summary>
        /// Net change run (processes only changed parts).
        /// </summary>
        NetChange
    }
}
