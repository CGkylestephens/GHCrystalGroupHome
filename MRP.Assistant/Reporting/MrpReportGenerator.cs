using MRP.Assistant.Core;
using MRP.Assistant.Analysis;
using System.Text;

namespace MRP.Assistant.Reporting;

public class MrpReportGenerator
{
    public string GenerateReport(MrpLogComparison comparison, List<Explanation> explanations, ReportOptions options)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# MRP Log Comparison Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        
        // Section A: Run Summary
        sb.AppendLine("## A) RUN SUMMARY");
        sb.AppendLine();
        sb.AppendLine($"**Run A**: {comparison.RunA.SourceFile}");
        sb.AppendLine($"- Type: {comparison.RunA.RunType}");
        sb.AppendLine($"- Start: {comparison.RunA.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
        sb.AppendLine($"- End: {comparison.RunA.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
        sb.AppendLine($"- Site: {comparison.RunA.Site ?? "Unknown"}");
        sb.AppendLine();
        sb.AppendLine($"**Run B**: {comparison.RunB.SourceFile}");
        sb.AppendLine($"- Type: {comparison.RunB.RunType}");
        sb.AppendLine($"- Start: {comparison.RunB.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
        sb.AppendLine($"- End: {comparison.RunB.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
        sb.AppendLine($"- Site: {comparison.RunB.Site ?? "Unknown"}");
        sb.AppendLine();
        
        // Section B: What Changed
        sb.AppendLine("## B) WHAT CHANGED");
        sb.AppendLine();
        sb.AppendLine($"**Total Differences**: {comparison.Summary.TotalDifferences}");
        sb.AppendLine($"- Critical: {comparison.Summary.CriticalDifferences}");
        sb.AppendLine($"- Warning: {comparison.Summary.WarningDifferences}");
        sb.AppendLine($"- Info: {comparison.Summary.InfoDifferences}");
        sb.AppendLine();
        
        foreach (var diff in comparison.Differences.OrderByDescending(d => d.Severity))
        {
            var icon = diff.Severity switch
            {
                DifferenceSeverity.Critical => "🔴",
                DifferenceSeverity.Warning => "⚠️",
                _ => "ℹ️"
            };
            sb.AppendLine($"{icon} **{diff.Type}**: {diff.Description}");
        }
        sb.AppendLine();
        
        // Section C: Most Likely Why
        sb.AppendLine("## C) MOST LIKELY WHY");
        sb.AppendLine();
        
        var facts = explanations.Where(e => e.Type == ExplanationType.Fact).ToList();
        var inferences = explanations.Where(e => e.Type == ExplanationType.Inference).ToList();
        
        if (facts.Any())
        {
            sb.AppendLine("### ✅ FACT (Log-Supported)");
            foreach (var fact in facts)
            {
                sb.AppendLine($"- {fact.Text}");
            }
            sb.AppendLine();
        }
        
        if (inferences.Any() && options.IncludeInferences)
        {
            sb.AppendLine("### 🔍 INFERENCE (Likely Explanation)");
            foreach (var inference in inferences)
            {
                sb.AppendLine($"- {inference.Text} (Confidence: {inference.Confidence:P0})");
            }
            sb.AppendLine();
        }
        
        // Section D: Log Evidence
        sb.AppendLine("## D) LOG EVIDENCE");
        sb.AppendLine();
        sb.AppendLine("*Key excerpts from log files supporting the facts above*");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("See individual log files for full details");
        sb.AppendLine("```");
        sb.AppendLine();
        
        // Section E: Next Checks
        sb.AppendLine("## E) NEXT CHECKS IN EPICOR");
        sb.AppendLine();
        sb.AppendLine("1. Review job status for removed jobs");
        sb.AppendLine("2. Check part master data for part changes");
        sb.AppendLine("3. Verify MRP parameters and settings");
        sb.AppendLine("4. Investigate error messages in logs");
        sb.AppendLine();
        
        return sb.ToString();
    }
}
