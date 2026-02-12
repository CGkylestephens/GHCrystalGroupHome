using System;
using System.Collections.Generic;

namespace CrystalGroupHome.SharedRCL.Models
{
    /// <summary>
    /// Represents a parsed MRP log document containing detailed entries.
    /// </summary>
    public class MrpLogDocument
    {
        /// <summary>
        /// Metadata about the MRP run.
        /// </summary>
        public MrpRunMetadata Metadata { get; set; } = new();

        /// <summary>
        /// All entries extracted from the log.
        /// </summary>
        public List<MrpLogEntry> Entries { get; set; } = new();

        /// <summary>
        /// All raw log lines.
        /// </summary>
        public List<string> RawLines { get; set; } = new();
    }
}
