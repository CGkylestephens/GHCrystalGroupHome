using System.Collections.Generic;
using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.SharedRCL.Analysis
{
    /// <summary>
    /// Represents a comparison between two MRP log runs.
    /// </summary>
    public class MrpLogComparison
    {
        /// <summary>
        /// The first MRP run (typically the baseline or earlier run).
        /// </summary>
        public MrpLogDocument RunA { get; set; } = null!;

        /// <summary>
        /// The second MRP run (typically the comparison or later run).
        /// </summary>
        public MrpLogDocument RunB { get; set; } = null!;

        /// <summary>
        /// List of detected differences between the two runs.
        /// </summary>
        public List<MrpDifference> Differences { get; set; } = new();

        /// <summary>
        /// Summary statistics of the comparison.
        /// </summary>
        public ComparisonSummary Summary { get; set; } = new();
    }

    /// <summary>
    /// Summary statistics for an MRP log comparison.
    /// </summary>
    public class ComparisonSummary
    {
        /// <summary>
        /// Total number of differences detected.
        /// </summary>
        public int TotalDifferences { get; set; }

        /// <summary>
        /// Number of critical severity differences.
        /// </summary>
        public int CriticalCount { get; set; }

        /// <summary>
        /// Number of warning severity differences.
        /// </summary>
        public int WarningCount { get; set; }

        /// <summary>
        /// Number of info severity differences.
        /// </summary>
        public int InfoCount { get; set; }

        /// <summary>
        /// Number of jobs added in Run B.
        /// </summary>
        public int JobsAdded { get; set; }

        /// <summary>
        /// Number of jobs removed (present in Run A but not in Run B).
        /// </summary>
        public int JobsRemoved { get; set; }

        /// <summary>
        /// Number of date shifts detected.
        /// </summary>
        public int DateShifts { get; set; }

        /// <summary>
        /// Number of quantity changes detected.
        /// </summary>
        public int QuantityChanges { get; set; }

        /// <summary>
        /// Number of new errors that appeared in Run B.
        /// </summary>
        public int NewErrors { get; set; }
    }
}
