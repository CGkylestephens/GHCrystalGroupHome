using System;
using System.Collections.Generic;
using System.Linq;

namespace CrystalGroupHome.SharedRCL.Models
{
    /// <summary>
    /// Represents a parsed MRP log document with all entries and metadata.
    /// </summary>
    public class MrpLogDocument
    {
        /// <summary>
        /// Path to the source log file.
        /// </summary>
        public string SourceFile { get; set; } = string.Empty;

        /// <summary>
        /// Type of MRP run (e.g., "Regeneration", "Net Change").
        /// </summary>
        public string RunType { get; set; } = "Unknown";

        /// <summary>
        /// Site where the run occurred.
        /// </summary>
        public string? Site { get; set; }

        /// <summary>
        /// Start time of the MRP run.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// End time of the MRP run.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Duration of the MRP run.
        /// </summary>
        public TimeSpan? Duration
        {
            get
            {
                if (StartTime.HasValue && EndTime.HasValue)
                {
                    return EndTime.Value - StartTime.Value;
                }
                return null;
            }
        }

        /// <summary>
        /// All parsed log entries.
        /// </summary>
        public List<MrpLogEntry> Entries { get; set; } = new List<MrpLogEntry>();
    }

    /// <summary>
    /// Represents a single entry/line in an MRP log.
    /// </summary>
    public class MrpLogEntry
    {
        /// <summary>
        /// Line number in the source file.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Raw log line text.
        /// </summary>
        public string RawLine { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of the log entry.
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Type of log entry.
        /// </summary>
        public MrpLogEntryType EntryType { get; set; }

        /// <summary>
        /// Part number mentioned in this entry.
        /// </summary>
        public string? PartNumber { get; set; }

        /// <summary>
        /// Job number mentioned in this entry.
        /// </summary>
        public string? JobNumber { get; set; }

        /// <summary>
        /// Message or description from the log entry.
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Type of MRP log entry.
    /// </summary>
    public enum MrpLogEntryType
    {
        /// <summary>
        /// Normal informational entry.
        /// </summary>
        Info,

        /// <summary>
        /// Error entry.
        /// </summary>
        Error,

        /// <summary>
        /// Warning entry.
        /// </summary>
        Warning,

        /// <summary>
        /// Processing activity.
        /// </summary>
        Processing,

        /// <summary>
        /// Unknown type.
        /// </summary>
        Unknown
    }
}
