using MRP.Assistant.Core;
using MRP.Assistant.Analysis;
using System.Text;

namespace MRP.Assistant.Reporting;

public enum ReportFormat
{
    Markdown,
    Plaintext,
    Html,
    Json
}

public class ReportOptions
{
    public ReportFormat Format { get; set; } = ReportFormat.Markdown;
    public int MaxDifferencesToShow { get; set; } = 10;
}

public class MrpReportGenerator
{
    public string GenerateReport(MrpLogComparison comparison, List<Explanation> explanations, ReportOptions options)
    {
        return options.Format switch
        {
            ReportFormat.Markdown => GenerateMarkdownReport(comparison, explanations, options),
            ReportFormat.Plaintext => GeneratePlaintextReport(comparison, explanations, options),
            ReportFormat.Html => GenerateHtmlReport(comparison, explanations, options),
            ReportFormat.Json => GenerateJsonReport(comparison, explanations, options),
            _ => GenerateMarkdownReport(comparison, explanations, options)
        };
    }

    private string GenerateMarkdownReport(MrpLogComparison comparison, List<Explanation> explanations, ReportOptions options)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# MRP Log Comparison Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Section A: Run Summary
        sb.AppendLine("## A) Run Summary");
        sb.AppendLine();
        sb.AppendLine("### Run A");
        sb.AppendLine($"- **Source:** {Path.GetFileName(comparison.RunA.SourceFile)}");
        sb.AppendLine($"- **Run Type:** {comparison.RunA.RunType}");
        sb.AppendLine($"- **Site:** {comparison.RunA.Site ?? "Unknown"}");
        sb.AppendLine($"- **Entries:** {comparison.RunA.Entries.Count:N0}");
        sb.AppendLine();

        sb.AppendLine("### Run B");
        sb.AppendLine($"- **Source:** {Path.GetFileName(comparison.RunB.SourceFile)}");
        sb.AppendLine($"- **Run Type:** {comparison.RunB.RunType}");
        sb.AppendLine($"- **Site:** {comparison.RunB.Site ?? "Unknown"}");
        sb.AppendLine($"- **Entries:** {comparison.RunB.Entries.Count:N0}");
        sb.AppendLine();

        // Section B: What Changed
        sb.AppendLine("## B) What Changed");
        sb.AppendLine();
        sb.AppendLine($"**Total Differences:** {comparison.Summary.TotalDifferences}");
        sb.AppendLine($"- 🔴 Critical: {comparison.Summary.CriticalCount}");
        sb.AppendLine($"- ⚠️ Warning: {comparison.Summary.WarningCount}");
        sb.AppendLine($"- ℹ️ Info: {comparison.Summary.InfoCount}");
        sb.AppendLine();

        var differencesToShow = comparison.Differences.Take(options.MaxDifferencesToShow).ToList();
        foreach (var diff in differencesToShow)
        {
            var icon = diff.Severity switch
            {
                Severity.Critical => "🔴",
                Severity.Warning => "⚠️",
                _ => "ℹ️"
            };
            sb.AppendLine($"### {icon} {diff.Type}");
            sb.AppendLine($"{diff.Description}");
            if (!string.IsNullOrEmpty(diff.JobNumber))
                sb.AppendLine($"- **Job:** {diff.JobNumber}");
            if (!string.IsNullOrEmpty(diff.PartNumber))
                sb.AppendLine($"- **Part:** {diff.PartNumber}");
            sb.AppendLine();
        }

        if (comparison.Differences.Count > options.MaxDifferencesToShow)
        {
            sb.AppendLine($"*...and {comparison.Differences.Count - options.MaxDifferencesToShow} more differences*");
            sb.AppendLine();
        }

        // Section C: Most Likely Why
        sb.AppendLine("## C) Most Likely Why");
        sb.AppendLine();
        foreach (var explanation in explanations)
        {
            sb.AppendLine($"### {explanation.Title}");
            sb.AppendLine($"{explanation.Description}");
            sb.AppendLine();

            if (explanation.Facts.Any())
            {
                sb.AppendLine("**Facts (Log-Supported):**");
                foreach (var fact in explanation.Facts)
                {
                    sb.AppendLine($"- {fact}");
                }
                sb.AppendLine();
            }

            if (explanation.Inferences.Any())
            {
                sb.AppendLine($"**Inferences (Confidence: {explanation.Confidence:P0}):**");
                foreach (var inference in explanation.Inferences)
                {
                    sb.AppendLine($"- {inference}");
                }
                sb.AppendLine();
            }
        }

        // Section D: Log Evidence
        sb.AppendLine("## D) Log Evidence");
        sb.AppendLine();
        sb.AppendLine("Detailed line-by-line evidence available in source files:");
        sb.AppendLine($"- Run A: {comparison.RunA.SourceFile}");
        sb.AppendLine($"- Run B: {comparison.RunB.SourceFile}");
        sb.AppendLine();

        // Section E: Next Checks in Epicor
        sb.AppendLine("## E) Next Checks in Epicor");
        sb.AppendLine();
        sb.AppendLine("Recommended verification steps:");
        sb.AppendLine("1. Review affected job records in Job Tracker");
        sb.AppendLine("2. Check part master data for any recent changes");
        sb.AppendLine("3. Verify BOM and routing accuracy");
        sb.AppendLine("4. Review recent sales order or demand changes");
        sb.AppendLine("5. Check MRP parameters and scheduling rules");
        sb.AppendLine();

        return sb.ToString();
    }

    private string GeneratePlaintextReport(MrpLogComparison comparison, List<Explanation> explanations, ReportOptions options)
    {
        // Simple conversion from markdown - remove markdown syntax
        var markdown = GenerateMarkdownReport(comparison, explanations, options);
        return markdown
            .Replace("**", "")
            .Replace("*", "")
            .Replace("#", "")
            .Replace("🔴", "[CRITICAL]")
            .Replace("⚠️", "[WARNING]")
            .Replace("ℹ️", "[INFO]")
            .Replace("✅", "[FACT]")
            .Replace("🔍", "[INFERENCE]");
    }

    private string GenerateHtmlReport(MrpLogComparison comparison, List<Explanation> explanations, ReportOptions options)
    {
        var markdown = GenerateMarkdownReport(comparison, explanations, options);
        // Basic markdown to HTML conversion
        var html = markdown
            .Replace("# ", "<h1>").Replace("\n\n", "</h1>\n\n")
            .Replace("## ", "<h2>").Replace("\n\n", "</h2>\n\n")
            .Replace("### ", "<h3>").Replace("\n\n", "</h3>\n\n")
            .Replace("**", "<strong>").Replace("**", "</strong>")
            .Replace("- ", "<li>").Replace("\n", "</li>\n");

        return $"<!DOCTYPE html><html><head><title>MRP Report</title></head><body>{html}</body></html>";
    }

    private string GenerateJsonReport(MrpLogComparison comparison, List<Explanation> explanations, ReportOptions options)
    {
        var report = new
        {
            generated = DateTime.Now,
            runA = new { comparison.RunA.SourceFile, comparison.RunA.RunType, comparison.RunA.Site, EntryCount = comparison.RunA.Entries.Count },
            runB = new { comparison.RunB.SourceFile, comparison.RunB.RunType, comparison.RunB.Site, EntryCount = comparison.RunB.Entries.Count },
            summary = comparison.Summary,
            differences = comparison.Differences.Take(options.MaxDifferencesToShow),
            explanations
        };

        return System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
