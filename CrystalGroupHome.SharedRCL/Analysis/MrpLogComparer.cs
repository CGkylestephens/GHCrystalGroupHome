using System;
using System.Collections.Generic;
using System.Linq;
using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.SharedRCL.Analysis
{
    /// <summary>
    /// Engine for comparing two MRP log documents and detecting differences.
    /// </summary>
    public class MrpLogComparer
    {
        /// <summary>
        /// Compares two MRP log documents and returns a detailed comparison.
        /// </summary>
        /// <param name="runA">The first MRP run (baseline).</param>
        /// <param name="runB">The second MRP run (comparison).</param>
        /// <returns>A comparison object containing all detected differences.</returns>
        public MrpLogComparison Compare(MrpLogDocument runA, MrpLogDocument runB)
        {
            var comparison = new MrpLogComparison
            {
                RunA = runA,
                RunB = runB
            };

            // Detect all difference types
            comparison.Differences.AddRange(DetectJobDifferences(runA, runB));
            comparison.Differences.AddRange(DetectDateShifts(runA, runB));
            comparison.Differences.AddRange(DetectQuantityChanges(runA, runB));
            comparison.Differences.AddRange(DetectErrorDifferences(runA, runB));
            comparison.Differences.AddRange(DetectPartDifferences(runA, runB));

            // Generate summary
            comparison.Summary = GenerateSummary(comparison.Differences);

            return comparison;
        }

        /// <summary>
        /// Detects jobs that were added or removed between runs.
        /// </summary>
        private List<MrpDifference> DetectJobDifferences(MrpLogDocument runA, MrpLogDocument runB)
        {
            var differences = new List<MrpDifference>();

            // Extract all job numbers from both runs (case-insensitive)
            var jobsA = new HashSet<string>(
                runA.Entries
                    .Where(e => !string.IsNullOrEmpty(e.JobNumber))
                    .Select(e => e.JobNumber!.ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase
            );

            var jobsB = new HashSet<string>(
                runB.Entries
                    .Where(e => !string.IsNullOrEmpty(e.JobNumber))
                    .Select(e => e.JobNumber!.ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase
            );

            // Jobs removed (in A but not in B)
            foreach (var job in jobsA.Where(j => !jobsB.Contains(j)))
            {
                var entry = runA.Entries.FirstOrDefault(e => 
                    e.JobNumber?.Equals(job, StringComparison.OrdinalIgnoreCase) == true);

                differences.Add(new MrpDifference
                {
                    Type = DifferenceType.JobRemoved,
                    JobNumber = job,
                    PartNumber = entry?.PartNumber ?? string.Empty,
                    Description = $"Job {job} present in Run A but missing in Run B",
                    RunAEntry = entry,
                    RunBEntry = null,
                    Severity = DifferenceSeverity.Critical
                });
            }

            // Jobs added (in B but not in A)
            foreach (var job in jobsB.Where(j => !jobsA.Contains(j)))
            {
                var entry = runB.Entries.FirstOrDefault(e => 
                    e.JobNumber?.Equals(job, StringComparison.OrdinalIgnoreCase) == true);

                differences.Add(new MrpDifference
                {
                    Type = DifferenceType.JobAdded,
                    JobNumber = job,
                    PartNumber = entry?.PartNumber ?? string.Empty,
                    Description = $"Job {job} added in Run B (not present in Run A)",
                    RunAEntry = null,
                    RunBEntry = entry,
                    Severity = DifferenceSeverity.Warning
                });
            }

            return differences;
        }

        /// <summary>
        /// Detects date shifts for entries present in both runs.
        /// </summary>
        private List<MrpDifference> DetectDateShifts(MrpLogDocument runA, MrpLogDocument runB)
        {
            var differences = new List<MrpDifference>();

            // Group entries by job number (for matching)
            var entriesAByJob = runA.Entries
                .Where(e => !string.IsNullOrEmpty(e.JobNumber) && e.Date.HasValue)
                .GroupBy(e => e.JobNumber!.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var entriesBByJob = runB.Entries
                .Where(e => !string.IsNullOrEmpty(e.JobNumber) && e.Date.HasValue)
                .GroupBy(e => e.JobNumber!.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Compare jobs present in both runs
            foreach (var jobNumber in entriesAByJob.Keys.Where(k => entriesBByJob.ContainsKey(k)))
            {
                var entriesA = entriesAByJob[jobNumber];
                var entriesB = entriesBByJob[jobNumber];

                // Compare dates for matching entries
                foreach (var entryA in entriesA)
                {
                    foreach (var entryB in entriesB)
                    {
                        if (entryA.Date.HasValue && entryB.Date.HasValue)
                        {
                            var daysDifference = Math.Abs((entryB.Date.Value - entryA.Date.Value).TotalDays);

                            // Flag if difference > 1 day
                            if (daysDifference > 1)
                            {
                                var severity = DetermineDateShiftSeverity(daysDifference);

                                differences.Add(new MrpDifference
                                {
                                    Type = DifferenceType.DateShifted,
                                    JobNumber = jobNumber,
                                    PartNumber = entryA.PartNumber ?? entryB.PartNumber ?? string.Empty,
                                    Description = $"Date shifted for job {jobNumber}: {entryA.Date.Value:MM/dd/yyyy} → {entryB.Date.Value:MM/dd/yyyy} ({daysDifference:F0} days)",
                                    RunAEntry = entryA,
                                    RunBEntry = entryB,
                                    Severity = severity,
                                    Details = new Dictionary<string, object>
                                    {
                                        ["OriginalDate"] = entryA.Date.Value.ToString("M/d/yyyy"),
                                        ["NewDate"] = entryB.Date.Value.ToString("M/d/yyyy"),
                                        ["DaysDifference"] = (int)daysDifference
                                    }
                                });
                                
                                // Only report once per job to avoid duplicates
                                break;
                            }
                        }
                    }
                }
            }

            return differences;
        }

        /// <summary>
        /// Detects quantity changes for entries present in both runs.
        /// </summary>
        private List<MrpDifference> DetectQuantityChanges(MrpLogDocument runA, MrpLogDocument runB)
        {
            var differences = new List<MrpDifference>();

            // Group entries by job/part combination
            var entriesAByKey = runA.Entries
                .Where(e => !string.IsNullOrEmpty(e.JobNumber) && e.Quantity.HasValue)
                .GroupBy(e => $"{e.JobNumber?.ToUpperInvariant()}_{e.PartNumber?.ToUpperInvariant()}")
                .ToDictionary(g => g.Key, g => g.ToList());

            var entriesBByKey = runB.Entries
                .Where(e => !string.IsNullOrEmpty(e.JobNumber) && e.Quantity.HasValue)
                .GroupBy(e => $"{e.JobNumber?.ToUpperInvariant()}_{e.PartNumber?.ToUpperInvariant()}")
                .ToDictionary(g => g.Key, g => g.ToList());

            // Compare quantities for matching entries
            foreach (var key in entriesAByKey.Keys.Where(k => entriesBByKey.ContainsKey(k)))
            {
                var entryA = entriesAByKey[key].FirstOrDefault();
                var entryB = entriesBByKey[key].FirstOrDefault();

                if (entryA != null && entryB != null && entryA.Quantity.HasValue && entryB.Quantity.HasValue)
                {
                    var originalQty = entryA.Quantity.Value;
                    var newQty = entryB.Quantity.Value;

                    // Calculate percentage change
                    var percentChange = originalQty != 0 
                        ? Math.Abs((newQty - originalQty) / originalQty) * 100
                        : (newQty != 0 ? 100 : 0);

                    // Flag if > 5% difference
                    if (percentChange > 5)
                    {
                        var severity = DetermineQuantityChangeSeverity(percentChange);

                        differences.Add(new MrpDifference
                        {
                            Type = DifferenceType.QuantityChanged,
                            JobNumber = entryA.JobNumber,
                            PartNumber = entryA.PartNumber ?? string.Empty,
                            Description = $"Quantity changed for job {entryA.JobNumber}, part {entryA.PartNumber}: {originalQty} → {newQty} ({percentChange:F1}% change)",
                            RunAEntry = entryA,
                            RunBEntry = entryB,
                            Severity = severity,
                            Details = new Dictionary<string, object>
                            {
                                ["OriginalQuantity"] = originalQty,
                                ["NewQuantity"] = newQty,
                                ["PercentChange"] = percentChange
                            }
                        });
                    }
                }
            }

            return differences;
        }

        /// <summary>
        /// Detects errors that appeared or were resolved between runs.
        /// </summary>
        private List<MrpDifference> DetectErrorDifferences(MrpLogDocument runA, MrpLogDocument runB)
        {
            var differences = new List<MrpDifference>();

            // Extract error entries
            var errorsA = runA.Entries.Where(e => e.IsError).ToList();
            var errorsB = runB.Entries.Where(e => e.IsError).ToList();

            // Create unique identifiers for errors (based on job/part/line content)
            var errorKeysA = new HashSet<string>(
                errorsA.Select(e => GetErrorKey(e))
            );

            var errorKeysB = new HashSet<string>(
                errorsB.Select(e => GetErrorKey(e))
            );

            // New errors (in B not in A)
            foreach (var error in errorsB.Where(e => !errorKeysA.Contains(GetErrorKey(e))))
            {
                differences.Add(new MrpDifference
                {
                    Type = DifferenceType.ErrorAppeared,
                    JobNumber = error.JobNumber,
                    PartNumber = error.PartNumber ?? string.Empty,
                    Description = $"New error appeared in Run B: {error.RawLine}",
                    RunAEntry = null,
                    RunBEntry = error,
                    Severity = DifferenceSeverity.Critical
                });
            }

            // Resolved errors (in A not in B)
            foreach (var error in errorsA.Where(e => !errorKeysB.Contains(GetErrorKey(e))))
            {
                differences.Add(new MrpDifference
                {
                    Type = DifferenceType.ErrorResolved,
                    JobNumber = error.JobNumber,
                    PartNumber = error.PartNumber ?? string.Empty,
                    Description = $"Error resolved in Run B: {error.RawLine}",
                    RunAEntry = error,
                    RunBEntry = null,
                    Severity = DifferenceSeverity.Info
                });
            }

            return differences;
        }

        /// <summary>
        /// Detects parts that appeared or were removed between runs.
        /// </summary>
        private List<MrpDifference> DetectPartDifferences(MrpLogDocument runA, MrpLogDocument runB)
        {
            var differences = new List<MrpDifference>();

            // Extract all unique parts from both runs
            var partsA = new HashSet<string>(
                runA.Entries
                    .Where(e => !string.IsNullOrEmpty(e.PartNumber))
                    .Select(e => e.PartNumber!.ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase
            );

            var partsB = new HashSet<string>(
                runB.Entries
                    .Where(e => !string.IsNullOrEmpty(e.PartNumber))
                    .Select(e => e.PartNumber!.ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase
            );

            // Parts removed (in A not in B)
            foreach (var part in partsA.Where(p => !partsB.Contains(p)))
            {
                var entry = runA.Entries.FirstOrDefault(e => 
                    e.PartNumber?.Equals(part, StringComparison.OrdinalIgnoreCase) == true);

                differences.Add(new MrpDifference
                {
                    Type = DifferenceType.PartRemoved,
                    PartNumber = part,
                    Description = $"Part {part} removed from planning in Run B",
                    RunAEntry = entry,
                    RunBEntry = null,
                    Severity = DifferenceSeverity.Warning
                });
            }

            // Parts appeared (in B not in A)
            foreach (var part in partsB.Where(p => !partsA.Contains(p)))
            {
                var entry = runB.Entries.FirstOrDefault(e => 
                    e.PartNumber?.Equals(part, StringComparison.OrdinalIgnoreCase) == true);

                differences.Add(new MrpDifference
                {
                    Type = DifferenceType.PartAppeared,
                    PartNumber = part,
                    Description = $"Part {part} reappeared in Run B after being removed in Run A",
                    RunAEntry = null,
                    RunBEntry = entry,
                    Severity = DifferenceSeverity.Warning
                });
            }

            return differences;
        }

        /// <summary>
        /// Generates a summary from the list of differences.
        /// </summary>
        private ComparisonSummary GenerateSummary(List<MrpDifference> differences)
        {
            var summary = new ComparisonSummary
            {
                TotalDifferences = differences.Count,
                CriticalCount = differences.Count(d => d.Severity == DifferenceSeverity.Critical),
                WarningCount = differences.Count(d => d.Severity == DifferenceSeverity.Warning),
                InfoCount = differences.Count(d => d.Severity == DifferenceSeverity.Info),
                JobsAdded = differences.Count(d => d.Type == DifferenceType.JobAdded),
                JobsRemoved = differences.Count(d => d.Type == DifferenceType.JobRemoved),
                DateShifts = differences.Count(d => d.Type == DifferenceType.DateShifted),
                QuantityChanges = differences.Count(d => d.Type == DifferenceType.QuantityChanged),
                NewErrors = differences.Count(d => d.Type == DifferenceType.ErrorAppeared)
            };

            return summary;
        }

        /// <summary>
        /// Determines severity based on number of days difference.
        /// </summary>
        private DifferenceSeverity DetermineDateShiftSeverity(double daysDifference)
        {
            if (daysDifference > 7)
                return DifferenceSeverity.Critical;
            if (daysDifference > 3)
                return DifferenceSeverity.Warning;
            return DifferenceSeverity.Info;
        }

        /// <summary>
        /// Determines severity based on percentage change.
        /// </summary>
        private DifferenceSeverity DetermineQuantityChangeSeverity(decimal percentChange)
        {
            if (percentChange > 50)
                return DifferenceSeverity.Critical;
            if (percentChange > 20)
                return DifferenceSeverity.Warning;
            return DifferenceSeverity.Info;
        }

        /// <summary>
        /// Creates a unique key for an error entry.
        /// </summary>
        private string GetErrorKey(MrpLogEntry entry)
        {
            // Use job number, part number, and a portion of the raw line to identify unique errors
            var jobPart = $"{entry.JobNumber?.ToUpperInvariant() ?? ""}_{entry.PartNumber?.ToUpperInvariant() ?? ""}";
            var lineHash = entry.RawLine.GetHashCode();
            return $"{jobPart}_{lineHash}";
        }
    }
}
