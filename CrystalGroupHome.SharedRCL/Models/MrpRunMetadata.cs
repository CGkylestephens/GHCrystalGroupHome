using System;
using System.Collections.Generic;

namespace CrystalGroupHome.SharedRCL.Models
{
    /// <summary>
    /// Represents metadata extracted from an MRP (Material Requirements Planning) log file.
    /// </summary>
    public class MrpRunMetadata
    {
        /// <summary>
        /// The site name where the MRP run occurred (e.g., "PLANT01", "MfgSys").
        /// </summary>
        public string? Site { get; set; }

        /// <summary>
        /// The timestamp when the MRP run started.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// The timestamp when the MRP run ended. May be null if the run did not complete.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// The type of MRP run: "regen" (regeneration), "net change", or "unknown" if not detectable.
        /// </summary>
        public string RunType { get; set; } = "unknown";

        /// <summary>
        /// The status of the MRP run: "success", "failed", "incomplete", or "uncertain".
        /// </summary>
        public string Status { get; set; } = "uncertain";

        /// <summary>
        /// Health flags indicating issues with the run (e.g., "timeout", "error", "abandoned", "defunct").
        /// </summary>
        public List<string> HealthFlags { get; set; } = new List<string>();
    }
}
