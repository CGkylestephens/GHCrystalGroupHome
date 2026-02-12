using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CrystalGroupHome.SharedRCL.Analysis;
using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.SharedRCL.Reporting
{
    /// <summary>
    /// Generates planner-friendly reports from MRP log comparisons.
    /// </summary>
    public class MrpReportGenerator
    {
        /// <summary>
        /// Generates a report from the comparison and explanations.
        /// </summary>
        /// <param name="comparison">The MRP log comparison.</param>
        /// <param name="explanations">List of explanations for differences.</param>
        /// <param name="options">Report generation options.</param>
        /// <returns>The generated report as a string.</returns>
        public string GenerateReport(
            MrpLogComparison comparison,
            List<Explanation> explanations,
            ReportOptions options)
        {
            return options.Format switch
            {
                ReportFormat.Markdown => GenerateMarkdownReport(comparison, explanations, options),
                ReportFormat.PlainText => GeneratePlainTextReport(comparison, explanations, options),
                ReportFormat.Html => GenerateHtmlReport(comparison, explanations, options),
                ReportFormat.Json => GenerateJsonReport(comparison, explanations),
                _ => throw new ArgumentException($"Unsupported format: {options.Format}")
            };
        }

        private string GenerateMarkdownReport(
            MrpLogComparison comparison,
            List<Explanation> explanations,
            ReportOptions options)
        {
            var sb = new StringBuilder();

            // Section A: Run Summary
            sb.AppendLine("# MRP Log Comparison Report");
            sb.AppendLine();
            sb.AppendLine($"*Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
            sb.AppendLine();
            sb.AppendLine("## A) RUN SUMMARY");
            sb.AppendLine();
            AppendRunSummary(sb, "Run A", comparison.RunA);
            sb.AppendLine();
            AppendRunSummary(sb, "Run B", comparison.RunB);
            sb.AppendLine();

            // Section B: What Changed
            sb.AppendLine("## B) WHAT CHANGED");
            sb.AppendLine();
            AppendDifferencesSummary(sb, comparison, options);
            sb.AppendLine();

            // Section C: Most Likely Why
            sb.AppendLine("## C) MOST LIKELY WHY");
            sb.AppendLine();
            AppendExplanations(sb, explanations, options);
            sb.AppendLine();

            // Section D: Log Evidence
            sb.AppendLine("## D) LOG EVIDENCE");
            sb.AppendLine();
            AppendLogEvidence(sb, explanations, options);
            sb.AppendLine();

            // Section E: Next Checks in Epicor
            sb.AppendLine("## E) NEXT CHECKS IN EPICOR");
            sb.AppendLine();
            AppendNextSteps(sb, explanations);
            sb.AppendLine();

            return sb.ToString();
        }

        private void AppendRunSummary(StringBuilder sb, string runName, MrpLogDocument document)
        {
            sb.AppendLine($"### {runName}");
            sb.AppendLine($"- **Type**: {document.RunType}");
            sb.AppendLine($"- **Source**: `{Path.GetFileName(document.SourceFile)}`");
            sb.AppendLine($"- **Site**: {document.Site ?? "Unknown"}");
            sb.AppendLine($"- **Start Time**: {document.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
            sb.AppendLine($"- **End Time**: {document.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
            sb.AppendLine($"- **Duration**: {document.Duration?.ToString(@"hh\:mm\:ss") ?? "Unknown"}");
            sb.AppendLine($"- **Entries Parsed**: {document.Entries.Count:N0}");
            sb.AppendLine($"- **Errors Logged**: {document.Entries.Count(e => e.EntryType == MrpLogEntryType.Error)}");

            // Count unique parts and jobs
            var parts = document.Entries.Where(e => !string.IsNullOrEmpty(e.PartNumber))
                .Select(e => e.PartNumber).Distinct().Count();
            var jobs = document.Entries.Where(e => !string.IsNullOrEmpty(e.JobNumber))
                .Select(e => e.JobNumber).Distinct().Count();

            sb.AppendLine($"- **Parts Processed**: {parts}");
            sb.AppendLine($"- **Jobs Referenced**: {jobs}");
        }

        private void AppendDifferencesSummary(
            StringBuilder sb,
            MrpLogComparison comparison,
            ReportOptions options)
        {
            var summary = comparison.Summary;

            // Handle empty comparison gracefully
            if (summary.TotalDifferences == 0)
            {
                sb.AppendLine("**No significant differences found between the two runs.**");
                return;
            }

            // Overview stats
            sb.AppendLine($"**Total Differences**: {summary.TotalDifferences}");
            sb.AppendLine($"- 🔴 Critical: {summary.CriticalCount}");
            sb.AppendLine($"- ⚠️  Warning: {summary.WarningCount}");
            sb.AppendLine($"- ℹ️  Info: {summary.InfoCount}");
            sb.AppendLine();

            // Top differences by severity
            var topDifferences = comparison.Differences
                .OrderByDescending(d => d.Severity)
                .ThenBy(d => d.Type)
                .Take(options.MaxDifferencesToShow)
                .ToList();

            sb.AppendLine("### Top Changes:");
            sb.AppendLine();

            foreach (var diff in topDifferences)
            {
                var icon = diff.Severity switch
                {
                    DifferenceSeverity.Critical => "🔴",
                    DifferenceSeverity.Warning => "⚠️",
                    DifferenceSeverity.Info => "ℹ️",
                    _ => "•"
                };

                sb.AppendLine($"{icon} **{diff.Type}**: {diff.Description}");

                if (!string.IsNullOrEmpty(diff.PartNumber))
                {
                    sb.AppendLine($"  - Part: {diff.PartNumber}");
                }
                if (!string.IsNullOrEmpty(diff.JobNumber))
                {
                    sb.AppendLine($"  - Job: {diff.JobNumber}");
                }

                sb.AppendLine();
            }

            // Summary by type
            sb.AppendLine("### Summary by Type:");
            sb.AppendLine($"- Jobs Added: {summary.JobsAdded}");
            sb.AppendLine($"- Jobs Removed: {summary.JobsRemoved}");
            sb.AppendLine($"- Date Shifts: {summary.DateShifts}");
            sb.AppendLine($"- Quantity Changes: {summary.QuantityChanges}");
            sb.AppendLine($"- New Errors: {summary.NewErrors}");
        }

        private void AppendExplanations(
            StringBuilder sb,
            List<Explanation> explanations,
            ReportOptions options)
        {
            if (explanations == null || explanations.Count == 0)
            {
                sb.AppendLine("*No detailed explanations available.*");
                return;
            }

            var topExplanations = explanations
                .OrderByDescending(e => e.RelatedDifference.Severity)
                .Take(options.MaxDifferencesToShow)
                .ToList();

            foreach (var explanation in topExplanations)
            {
                sb.AppendLine($"### {explanation.Summary}");
                sb.AppendLine();

                // Facts
                if (explanation.Facts.Any())
                {
                    sb.AppendLine("**FACTS** (log-supported evidence):");
                    foreach (var fact in explanation.Facts)
                    {
                        sb.AppendLine($"- ✅ {fact.Statement}");
                    }
                    sb.AppendLine();
                }

                // Inferences
                if (explanation.Inferences.Any())
                {
                    sb.AppendLine("**INFERENCES** (plausible explanations):");
                    foreach (var inference in explanation.Inferences)
                    {
                        var confidence = inference.ConfidenceLevel >= 0.8 ? "High" :
                                         inference.ConfidenceLevel >= 0.6 ? "Medium" : "Low";
                        sb.AppendLine($"- 🔍 {inference.Statement} (Confidence: {confidence})");

                        if (inference.SupportingReasons.Any())
                        {
                            foreach (var reason in inference.SupportingReasons)
                            {
                                sb.AppendLine($"  - {reason}");
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        private void AppendLogEvidence(
            StringBuilder sb,
            List<Explanation> explanations,
            ReportOptions options)
        {
            if (!options.IncludeRawLogExcerpts)
            {
                sb.AppendLine("_(Raw log excerpts omitted)_");
                return;
            }

            if (explanations == null || explanations.Count == 0)
            {
                sb.AppendLine("*No log evidence available.*");
                return;
            }

            var topExplanations = explanations
                .OrderByDescending(e => e.RelatedDifference.Severity)
                .Take(options.MaxDifferencesToShow);

            bool hasEvidence = false;
            foreach (var explanation in topExplanations)
            {
                var factsWithEvidence = explanation.Facts.Where(f => !string.IsNullOrEmpty(f.LogEvidence)).ToList();
                if (factsWithEvidence.Any())
                {
                    hasEvidence = true;
                    sb.AppendLine($"### Evidence for: {explanation.Summary}");
                    sb.AppendLine();

                    foreach (var fact in factsWithEvidence)
                    {
                        sb.AppendLine($"**Line {fact.LineNumber}**:");
                        sb.AppendLine("```");
                        sb.AppendLine(fact.LogEvidence);
                        sb.AppendLine("```");
                        sb.AppendLine();
                    }
                }
            }

            if (!hasEvidence)
            {
                sb.AppendLine("*No detailed log evidence available.*");
            }
        }

        private void AppendNextSteps(StringBuilder sb, List<Explanation> explanations)
        {
            if (explanations == null || explanations.Count == 0)
            {
                sb.AppendLine("*No specific next steps identified.*");
                return;
            }

            // Collect and deduplicate next steps
            var allNextSteps = explanations
                .SelectMany(e => e.NextStepsInEpicor)
                .Distinct()
                .ToList();

            if (!allNextSteps.Any())
            {
                sb.AppendLine("*No specific next steps identified.*");
                return;
            }

            // Group by priority (heuristic based on keywords)
            var mustCheck = new List<string>();
            var shouldCheck = new List<string>();
            var optional = new List<string>();

            foreach (var step in allNextSteps)
            {
                if (step.Contains("Job Tracker") || step.Contains("System Monitor"))
                    mustCheck.Add(step);
                else if (step.Contains("Review") || step.Contains("Check"))
                    shouldCheck.Add(step);
                else
                    optional.Add(step);
            }

            if (mustCheck.Any())
            {
                sb.AppendLine("### 🔴 Must Check:");
                foreach (var step in mustCheck)
                    sb.AppendLine($"- {step}");
                sb.AppendLine();
            }

            if (shouldCheck.Any())
            {
                sb.AppendLine("### ⚠️ Should Check:");
                foreach (var step in shouldCheck)
                    sb.AppendLine($"- {step}");
                sb.AppendLine();
            }

            if (optional.Any())
            {
                sb.AppendLine("### ℹ️ Optional:");
                foreach (var step in optional)
                    sb.AppendLine($"- {step}");
            }
        }

        private string GeneratePlainTextReport(
            MrpLogComparison comparison,
            List<Explanation> explanations,
            ReportOptions options)
        {
            var sb = new StringBuilder();

            // Section A: Run Summary
            sb.AppendLine("MRP LOG COMPARISON REPORT");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("A) RUN SUMMARY");
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine();
            AppendRunSummaryPlainText(sb, "Run A", comparison.RunA);
            sb.AppendLine();
            AppendRunSummaryPlainText(sb, "Run B", comparison.RunB);
            sb.AppendLine();

            // Section B: What Changed
            sb.AppendLine("B) WHAT CHANGED");
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine();
            AppendDifferencesSummaryPlainText(sb, comparison, options);
            sb.AppendLine();

            // Section C: Most Likely Why
            sb.AppendLine("C) MOST LIKELY WHY");
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine();
            AppendExplanationsPlainText(sb, explanations, options);
            sb.AppendLine();

            // Section D: Log Evidence
            sb.AppendLine("D) LOG EVIDENCE");
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine();
            AppendLogEvidencePlainText(sb, explanations, options);
            sb.AppendLine();

            // Section E: Next Checks in Epicor
            sb.AppendLine("E) NEXT CHECKS IN EPICOR");
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine();
            AppendNextStepsPlainText(sb, explanations);
            sb.AppendLine();

            return sb.ToString();
        }

        private void AppendRunSummaryPlainText(StringBuilder sb, string runName, MrpLogDocument document)
        {
            sb.AppendLine(runName);
            sb.AppendLine($"  Type: {document.RunType}");
            sb.AppendLine($"  Source: {Path.GetFileName(document.SourceFile)}");
            sb.AppendLine($"  Site: {document.Site ?? "Unknown"}");
            sb.AppendLine($"  Start Time: {document.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
            sb.AppendLine($"  End Time: {document.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
            sb.AppendLine($"  Duration: {document.Duration?.ToString(@"hh\:mm\:ss") ?? "Unknown"}");
            sb.AppendLine($"  Entries Parsed: {document.Entries.Count:N0}");
            sb.AppendLine($"  Errors Logged: {document.Entries.Count(e => e.EntryType == MrpLogEntryType.Error)}");

            var parts = document.Entries.Where(e => !string.IsNullOrEmpty(e.PartNumber))
                .Select(e => e.PartNumber).Distinct().Count();
            var jobs = document.Entries.Where(e => !string.IsNullOrEmpty(e.JobNumber))
                .Select(e => e.JobNumber).Distinct().Count();

            sb.AppendLine($"  Parts Processed: {parts}");
            sb.AppendLine($"  Jobs Referenced: {jobs}");
        }

        private void AppendDifferencesSummaryPlainText(
            StringBuilder sb,
            MrpLogComparison comparison,
            ReportOptions options)
        {
            var summary = comparison.Summary;

            if (summary.TotalDifferences == 0)
            {
                sb.AppendLine("No significant differences found between the two runs.");
                return;
            }

            sb.AppendLine($"Total Differences: {summary.TotalDifferences}");
            sb.AppendLine($"  Critical: {summary.CriticalCount}");
            sb.AppendLine($"  Warning: {summary.WarningCount}");
            sb.AppendLine($"  Info: {summary.InfoCount}");
            sb.AppendLine();

            var topDifferences = comparison.Differences
                .OrderByDescending(d => d.Severity)
                .ThenBy(d => d.Type)
                .Take(options.MaxDifferencesToShow)
                .ToList();

            sb.AppendLine("Top Changes:");
            sb.AppendLine();

            foreach (var diff in topDifferences)
            {
                var severityLabel = diff.Severity switch
                {
                    DifferenceSeverity.Critical => "[CRITICAL]",
                    DifferenceSeverity.Warning => "[WARNING]",
                    DifferenceSeverity.Info => "[INFO]",
                    _ => ""
                };

                sb.AppendLine($"  {severityLabel} {diff.Type}: {diff.Description}");

                if (!string.IsNullOrEmpty(diff.PartNumber))
                {
                    sb.AppendLine($"    Part: {diff.PartNumber}");
                }
                if (!string.IsNullOrEmpty(diff.JobNumber))
                {
                    sb.AppendLine($"    Job: {diff.JobNumber}");
                }

                sb.AppendLine();
            }

            sb.AppendLine("Summary by Type:");
            sb.AppendLine($"  Jobs Added: {summary.JobsAdded}");
            sb.AppendLine($"  Jobs Removed: {summary.JobsRemoved}");
            sb.AppendLine($"  Date Shifts: {summary.DateShifts}");
            sb.AppendLine($"  Quantity Changes: {summary.QuantityChanges}");
            sb.AppendLine($"  New Errors: {summary.NewErrors}");
        }

        private void AppendExplanationsPlainText(
            StringBuilder sb,
            List<Explanation> explanations,
            ReportOptions options)
        {
            if (explanations == null || explanations.Count == 0)
            {
                sb.AppendLine("No detailed explanations available.");
                return;
            }

            var topExplanations = explanations
                .OrderByDescending(e => e.RelatedDifference.Severity)
                .Take(options.MaxDifferencesToShow)
                .ToList();

            foreach (var explanation in topExplanations)
            {
                sb.AppendLine(explanation.Summary);
                sb.AppendLine();

                if (explanation.Facts.Any())
                {
                    sb.AppendLine("  FACTS (log-supported evidence):");
                    foreach (var fact in explanation.Facts)
                    {
                        sb.AppendLine($"    * {fact.Statement}");
                    }
                    sb.AppendLine();
                }

                if (explanation.Inferences.Any())
                {
                    sb.AppendLine("  INFERENCES (plausible explanations):");
                    foreach (var inference in explanation.Inferences)
                    {
                        var confidence = inference.ConfidenceLevel >= 0.8 ? "High" :
                                         inference.ConfidenceLevel >= 0.6 ? "Medium" : "Low";
                        sb.AppendLine($"    * {inference.Statement} (Confidence: {confidence})");

                        if (inference.SupportingReasons.Any())
                        {
                            foreach (var reason in inference.SupportingReasons)
                            {
                                sb.AppendLine($"      - {reason}");
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        private void AppendLogEvidencePlainText(
            StringBuilder sb,
            List<Explanation> explanations,
            ReportOptions options)
        {
            if (!options.IncludeRawLogExcerpts)
            {
                sb.AppendLine("(Raw log excerpts omitted)");
                return;
            }

            if (explanations == null || explanations.Count == 0)
            {
                sb.AppendLine("No log evidence available.");
                return;
            }

            var topExplanations = explanations
                .OrderByDescending(e => e.RelatedDifference.Severity)
                .Take(options.MaxDifferencesToShow);

            bool hasEvidence = false;
            foreach (var explanation in topExplanations)
            {
                var factsWithEvidence = explanation.Facts.Where(f => !string.IsNullOrEmpty(f.LogEvidence)).ToList();
                if (factsWithEvidence.Any())
                {
                    hasEvidence = true;
                    sb.AppendLine($"Evidence for: {explanation.Summary}");
                    sb.AppendLine();

                    foreach (var fact in factsWithEvidence)
                    {
                        sb.AppendLine($"  Line {fact.LineNumber}:");
                        sb.AppendLine($"  {fact.LogEvidence}");
                        sb.AppendLine();
                    }
                }
            }

            if (!hasEvidence)
            {
                sb.AppendLine("No detailed log evidence available.");
            }
        }

        private void AppendNextStepsPlainText(StringBuilder sb, List<Explanation> explanations)
        {
            if (explanations == null || explanations.Count == 0)
            {
                sb.AppendLine("No specific next steps identified.");
                return;
            }

            var allNextSteps = explanations
                .SelectMany(e => e.NextStepsInEpicor)
                .Distinct()
                .ToList();

            if (!allNextSteps.Any())
            {
                sb.AppendLine("No specific next steps identified.");
                return;
            }

            var mustCheck = new List<string>();
            var shouldCheck = new List<string>();
            var optional = new List<string>();

            foreach (var step in allNextSteps)
            {
                if (step.Contains("Job Tracker") || step.Contains("System Monitor"))
                    mustCheck.Add(step);
                else if (step.Contains("Review") || step.Contains("Check"))
                    shouldCheck.Add(step);
                else
                    optional.Add(step);
            }

            if (mustCheck.Any())
            {
                sb.AppendLine("Must Check:");
                foreach (var step in mustCheck)
                    sb.AppendLine($"  - {step}");
                sb.AppendLine();
            }

            if (shouldCheck.Any())
            {
                sb.AppendLine("Should Check:");
                foreach (var step in shouldCheck)
                    sb.AppendLine($"  - {step}");
                sb.AppendLine();
            }

            if (optional.Any())
            {
                sb.AppendLine("Optional:");
                foreach (var step in optional)
                    sb.AppendLine($"  - {step}");
            }
        }

        private string GenerateHtmlReport(
            MrpLogComparison comparison,
            List<Explanation> explanations,
            ReportOptions options)
        {
            // For now, convert markdown to basic HTML
            var markdown = GenerateMarkdownReport(comparison, explanations, options);
            
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\">");
            html.AppendLine("<title>MRP Log Comparison Report</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            html.AppendLine("h1 { color: #333; }");
            html.AppendLine("h2 { color: #555; border-bottom: 2px solid #ddd; padding-bottom: 5px; }");
            html.AppendLine("h3 { color: #777; }");
            html.AppendLine("code { background-color: #f4f4f4; padding: 2px 6px; border-radius: 3px; }");
            html.AppendLine("pre { background-color: #f4f4f4; padding: 15px; border-radius: 5px; overflow-x: auto; }");
            html.AppendLine("ul { line-height: 1.6; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            // Basic markdown to HTML conversion
            var lines = markdown.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("# "))
                    html.AppendLine($"<h1>{line.Substring(2)}</h1>");
                else if (line.StartsWith("## "))
                    html.AppendLine($"<h2>{line.Substring(3)}</h2>");
                else if (line.StartsWith("### "))
                    html.AppendLine($"<h3>{line.Substring(4)}</h3>");
                else if (line.StartsWith("```"))
                    html.AppendLine(line.Contains("```") && line != "```" ? "<pre>" : line == "```" ? "</pre>" : "<pre>");
                else if (line.StartsWith("- "))
                    html.AppendLine($"<li>{line.Substring(2)}</li>");
                else if (!string.IsNullOrWhiteSpace(line))
                    html.AppendLine($"<p>{line}</p>");
            }
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }

        private string GenerateJsonReport(
            MrpLogComparison comparison,
            List<Explanation> explanations)
        {
            var report = new
            {
                GeneratedAt = DateTime.Now,
                Comparison = new
                {
                    RunA = new
                    {
                        comparison.RunA.SourceFile,
                        comparison.RunA.RunType,
                        comparison.RunA.Site,
                        comparison.RunA.StartTime,
                        comparison.RunA.EndTime,
                        comparison.RunA.Duration,
                        EntryCount = comparison.RunA.Entries.Count,
                        ErrorCount = comparison.RunA.Entries.Count(e => e.EntryType == MrpLogEntryType.Error)
                    },
                    RunB = new
                    {
                        comparison.RunB.SourceFile,
                        comparison.RunB.RunType,
                        comparison.RunB.Site,
                        comparison.RunB.StartTime,
                        comparison.RunB.EndTime,
                        comparison.RunB.Duration,
                        EntryCount = comparison.RunB.Entries.Count,
                        ErrorCount = comparison.RunB.Entries.Count(e => e.EntryType == MrpLogEntryType.Error)
                    },
                    comparison.Summary,
                    comparison.Differences
                },
                Explanations = explanations
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            return JsonSerializer.Serialize(report, jsonOptions);
        }
    }
}
