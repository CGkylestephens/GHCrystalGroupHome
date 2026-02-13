---
name: Implement Planner-Friendly Report Generator
about: Create report generator that outputs structured, readable reports in multiple formats
title: "[Agent Task] Implement Planner-Friendly Report Generator"
labels: [reporter, agent]
assignees: [copilot]
---

## 🧠 Task Intent
Create a report generator that transforms comparisons and explanations into structured, planner-friendly reports following the 5-section format (Run Summary, What Changed, Most Likely Why, Log Evidence, Next Checks).

## 🔍 Scope / Input
**Dependencies**: Issues #2, #3, #4 must be complete (parser, comparer, explainer exist)

**Input**: 
- `MrpLogComparison` object
- `List<Explanation>` from ExplanationEngine

**Output Formats**: Markdown, PlainText (HTML/JSON optional)

**Reference**: `/copilot-instructions.md` - 5-section report structure

## ✅ Expected Output

### 1. Report Models (in `Reporting/` folder)

**ReportFormat.cs**:
```csharp
namespace MRP.Assistant.Reporting;

public enum ReportFormat
{
    Markdown,
    PlainText,
    Html,
    Json
}

public class ReportOptions
{
    public ReportFormat Format { get; set; } = ReportFormat.Markdown;
    public int MaxDifferencesToShow { get; set; } = 10;
    public bool IncludeRawLogExcerpts { get; set; } = true;
    public bool GroupByPart { get; set; } = false;
}
```

### 2. Report Generator (in `Reporting/` folder)

**MrpReportGenerator.cs**:
```csharp
namespace MRP.Assistant.Reporting;

public class MrpReportGenerator
{
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
        
        var topExplanations = explanations
            .OrderByDescending(e => e.RelatedDifference.Severity)
            .Take(options.MaxDifferencesToShow);
            
        foreach (var explanation in topExplanations)
        {
            sb.AppendLine($"### Evidence for: {explanation.Summary}");
            sb.AppendLine();
            
            foreach (var fact in explanation.Facts.Where(f => !string.IsNullOrEmpty(f.LogEvidence)))
            {
                sb.AppendLine($"**Line {fact.LineNumber}**:");
                sb.AppendLine("```");
                sb.AppendLine(fact.LogEvidence);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }
    }
    
    private void AppendNextSteps(StringBuilder sb, List<Explanation> explanations)
    {
        // Collect and deduplicate next steps
        var allNextSteps = explanations
            .SelectMany(e => e.NextStepsInEpicor)
            .Distinct()
            .ToList();
            
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
    
    private string GeneratePlainTextReport(/* ... */)
    {
        // Similar to markdown but without formatting symbols
        // Use --- for separators, plain bullet points
    }
    
    private string GenerateHtmlReport(/* ... */)
    {
        // Optional: Convert markdown to HTML or build HTML directly
    }
    
    private string GenerateJsonReport(/* ... */)
    {
        // Optional: Serialize comparison + explanations to JSON
    }
}
```

## 🧪 Acceptance Criteria
- [ ] MrpReportGenerator created in Reporting folder
- [ ] Generates reports in Markdown format
- [ ] Generates reports in PlainText format
- [ ] Follows 5-section structure exactly (A-E)
- [ ] Run Summary includes all metadata (type, time, duration, counts)
- [ ] What Changed limited to top 10 by severity
- [ ] Most Likely Why clearly labels FACT vs INFERENCE
- [ ] Log Evidence includes line numbers and code blocks
- [ ] Next Steps grouped by priority (Must/Should/Optional)
- [ ] Report is readable by non-technical users
- [ ] Handles empty/incomplete logs gracefully
- [ ] Deduplicates next steps across explanations

## 🧪 Sample Output
Generate sample report and save to `testdata/sample_comparison_report.md`:
```bash
dotnet run --project MRP.Assistant.CLI -- compare \
  --run-a testdata/mrp_log_sample_A.txt \
  --run-b testdata/mrp_log_sample_B.txt \
  --output testdata/sample_comparison_report.md
```

**Expected Report Structure**:
```markdown
# MRP Log Comparison Report

## A) RUN SUMMARY

### Run A
- **Type**: Regeneration
- **Source**: `mrp_log_sample_A.txt`
- **Site**: PLANT01
- **Start Time**: 2024-02-01 02:00:00
- ...

## B) WHAT CHANGED
**Total Differences**: 3
- 🔴 Critical: 1
- ⚠️  Warning: 1
- ℹ️  Info: 1
...

## C) MOST LIKELY WHY
### Job 14567 disappeared between runs

**FACTS** (log-supported evidence):
- ✅ Job 14567 present in Run A at line 42
- ✅ Run A logged error: Job 14567 abandoned due to timeout

**INFERENCES** (plausible explanations):
- 🔍 Job may have been automatically removed due to timeout (Confidence: High)
  - Timeout errors often trigger automatic cleanup
  - Common in Net Change runs
...

## D) LOG EVIDENCE
...

## E) NEXT CHECKS IN EPICOR
### 🔴 Must Check:
- Check Job Tracker for deletion history
...
```

## 📝 Notes
- Use clear visual hierarchy (headers, bullets, icons)
- Keep language simple and non-technical
- Prioritize actionability over completeness
- Limit to top 10 differences to avoid information overload
- Include generation timestamp in report
- Next issue will add CLI commands to invoke this generator
