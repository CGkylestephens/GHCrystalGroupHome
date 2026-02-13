using System.Collections.Generic;
using System.Linq;
using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.SharedRCL.Analysis
{
    /// <summary>
    /// Represents a comparison between two MRP log runs.
    /// </summary>
    public class MrpLogComparison
    {
        /// <summary>
        /// First MRP run (baseline).
        /// </summary>
        public MrpLogDocument RunA { get; set; } = new MrpLogDocument();

        /// <summary>
        /// Second MRP run (comparison target).
        /// </summary>
        public MrpLogDocument RunB { get; set; } = new MrpLogDocument();

        /// <summary>
        /// All differences found between the runs.
        /// </summary>
        public List<Difference> Differences { get; set; } = new List<Difference>();

        /// <summary>
        /// Summary statistics of the comparison.
        /// </summary>
        public ComparisonSummary Summary { get; set; } = new ComparisonSummary();
    }

    /// <summary>
    /// Summary statistics for a comparison.
    /// </summary>
    public class ComparisonSummary
    {
        /// <summary>
        /// Total number of differences found.
        /// </summary>
        public int TotalDifferences { get; set; }

        /// <summary>
        /// Number of critical differences.
        /// </summary>
        public int CriticalCount { get; set; }

        /// <summary>
        /// Number of warning-level differences.
        /// </summary>
        public int WarningCount { get; set; }

        /// <summary>
        /// Number of informational differences.
        /// </summary>
        public int InfoCount { get; set; }

        /// <summary>
        /// Number of jobs added in Run B.
        /// </summary>
        public int JobsAdded { get; set; }

        /// <summary>
        /// Number of jobs removed in Run B.
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
        /// Number of new errors in Run B.
        /// </summary>
        public int NewErrors { get; set; }
    }

    /// <summary>
    /// Represents a single difference between two MRP runs.
    /// </summary>
    public class Difference
    {
        /// <summary>
        /// Type of difference.
        /// </summary>
        public DifferenceType Type { get; set; }

        /// <summary>
        /// Severity level of the difference.
        /// </summary>
        public DifferenceSeverity Severity { get; set; }

        /// <summary>
        /// Human-readable description of the difference.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Part number associated with this difference, if any.
        /// </summary>
        public string? PartNumber { get; set; }

        /// <summary>
        /// Job number associated with this difference, if any.
        /// </summary>
        public string? JobNumber { get; set; }

        /// <summary>
        /// Value in Run A.
        /// </summary>
        public string? ValueInRunA { get; set; }

        /// <summary>
        /// Value in Run B.
        /// </summary>
        public string? ValueInRunB { get; set; }
    }

    /// <summary>
    /// Type of difference between MRP runs.
    /// </summary>
    public enum DifferenceType
    {
        /// <summary>
        /// Job was added.
        /// </summary>
        JobAdded,

        /// <summary>
        /// Job was removed.
        /// </summary>
        JobRemoved,

        /// <summary>
        /// Date changed.
        /// </summary>
        DateShift,

        /// <summary>
        /// Quantity changed.
        /// </summary>
        QuantityChange,

        /// <summary>
        /// New error appeared.
        /// </summary>
        NewError,

        /// <summary>
        /// Other type of change.
        /// </summary>
        Other
    }

    /// <summary>
    /// Severity level of a difference.
    /// </summary>
    public enum DifferenceSeverity
    {
        /// <summary>
        /// Informational only.
        /// </summary>
        Info,

        /// <summary>
        /// Warning level.
        /// </summary>
        Warning,

        /// <summary>
        /// Critical issue.
        /// </summary>
        Critical
    }
}
