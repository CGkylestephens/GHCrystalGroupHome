---
name: Create Explanation Engine (FACT vs INFERENCE)
about: Build logic that explains why differences occurred with FACT/INFERENCE distinction
title: "[Agent Task] Create Explanation Engine (FACT vs INFERENCE)"
labels: [analysis, agent]
assignees: [copilot]
---

## 🧠 Task Intent
Implement an explanation engine that takes detected differences and generates planner-friendly explanations, clearly distinguishing between FACT (log-supported evidence) and INFERENCE (plausible causes). This helps planners understand not just *what* changed, but *why* it might have changed.

## 🔍 Scope / Input
**Dependencies**: Issues #2 and #3 must be complete (parser and comparer exist)

**Input**: `MrpLogComparison` object with detected differences

**Reference**: 
- `/copilot-instructions.md` - FACT vs INFERENCE guidelines
- `/docs/MRP_ASSISTANT_PROJECT_PLAN.md` (Issue 4 section)

## ✅ Expected Output

### 1. Explanation Models (in `Analysis/` folder)

**Explanation.cs**:
```csharp
namespace MRP.Assistant.Analysis;

public class Explanation
{
    public MrpDifference RelatedDifference { get; set; } = null!;
    public string Summary { get; set; } = string.Empty; // One-line what happened
    public List<ExplanationFact> Facts { get; set; } = new();
    public List<ExplanationInference> Inferences { get; set; } = new();
    public List<string> NextStepsInEpicor { get; set; } = new();
}

public class ExplanationFact
{
    public string Statement { get; set; } = string.Empty;
    public string LogEvidence { get; set; } = string.Empty; // Actual log line
    public int LineNumber { get; set; }
}

public class ExplanationInference
{
    public string Statement { get; set; } = string.Empty;
    public double ConfidenceLevel { get; set; } // 0.0 - 1.0
    public List<string> SupportingReasons { get; set; } = new();
}
```

### 2. Explanation Engine (in `Analysis/` folder)

**ExplanationEngine.cs**:
```csharp
namespace MRP.Assistant.Analysis;

public class ExplanationEngine
{
    public List<Explanation> GenerateExplanations(MrpLogComparison comparison)
    {
        var explanations = new List<Explanation>();
        
        foreach (var diff in comparison.Differences)
        {
            var explanation = diff.Type switch
            {
                DifferenceType.JobRemoved => ExplainJobDisappearance(diff, comparison),
                DifferenceType.JobAdded => ExplainJobAppearance(diff, comparison),
                DifferenceType.DateShifted => ExplainDateShift(diff, comparison),
                DifferenceType.QuantityChanged => ExplainQuantityChange(diff, comparison),
                DifferenceType.ErrorAppeared => ExplainNewError(diff, comparison),
                DifferenceType.ErrorResolved => ExplainErrorResolution(diff, comparison),
                _ => CreateGenericExplanation(diff)
            };
            
            explanations.Add(explanation);
        }
        
        return explanations;
    }
    
    private Explanation ExplainJobDisappearance(MrpDifference diff, MrpLogComparison comparison)
    {
        var explanation = new Explanation
        {
            RelatedDifference = diff,
            Summary = $"Job {diff.JobNumber} disappeared between runs"
        };
        
        // FACTS: Extract from log evidence
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Job {diff.JobNumber} present in Run A at line {diff.RunAEntry?.LineNumber}",
            LogEvidence = diff.RunAEntry?.RawLine ?? "",
            LineNumber = diff.RunAEntry?.LineNumber ?? 0
        });
        
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Job {diff.JobNumber} not found in Run B",
            LogEvidence = "(absence in log)",
            LineNumber = 0
        });
        
        // Check for related errors in Run A
        var relatedError = FindRelatedError(diff.JobNumber, comparison.RunA);
        if (relatedError != null)
        {
            explanation.Facts.Add(new ExplanationFact
            {
                Statement = $"Run A logged error: {relatedError.ErrorMessage}",
                LogEvidence = relatedError.RawLine,
                LineNumber = relatedError.LineNumber
            });
            
            // INFERENCES based on error type
            if (relatedError.ErrorMessage?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true)
            {
                explanation.Inferences.Add(new ExplanationInference
                {
                    Statement = "Job may have been automatically removed due to timeout",
                    ConfidenceLevel = 0.85,
                    SupportingReasons = new() { "Timeout errors often trigger automatic cleanup", "Common in Net Change runs" }
                });
            }
            else if (relatedError.ErrorMessage?.Contains("abandoned", StringComparison.OrdinalIgnoreCase) == true)
            {
                explanation.Inferences.Add(new ExplanationInference
                {
                    Statement = "Job may have been manually deleted after abandonment",
                    ConfidenceLevel = 0.75,
                    SupportingReasons = new() { "Abandoned jobs typically require manual intervention" }
                });
            }
        }
        else
        {
            // No error found - generic inferences
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Job may have been manually deleted between runs",
                ConfidenceLevel = 0.60,
                SupportingReasons = new() { "No error logged in Run A", "Clean deletion suggests manual action" }
            });
        }
        
        // Next steps
        explanation.NextStepsInEpicor.Add("Check Job Tracker for deletion history");
        explanation.NextStepsInEpicor.Add("Review Job Entry screen for manual changes");
        explanation.NextStepsInEpicor.Add("Check MRP processing logs for cleanup actions");
        
        return explanation;
    }
    
    private Explanation ExplainDateShift(MrpDifference diff, MrpLogComparison comparison)
    {
        var explanation = new Explanation
        {
            RelatedDifference = diff,
            Summary = $"Job {diff.JobNumber} due date shifted"
        };
        
        // Extract date details from difference
        var originalDate = diff.Details.GetValueOrDefault("OriginalDate");
        var newDate = diff.Details.GetValueOrDefault("NewDate");
        var daysDiff = diff.Details.GetValueOrDefault("DaysDifference");
        
        // FACTS
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Job {diff.JobNumber} due date was {originalDate} in Run A",
            LogEvidence = diff.RunAEntry?.RawLine ?? "",
            LineNumber = diff.RunAEntry?.LineNumber ?? 0
        });
        
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Job {diff.JobNumber} due date is {newDate} in Run B ({daysDiff} days difference)",
            LogEvidence = diff.RunBEntry?.RawLine ?? "",
            LineNumber = diff.RunBEntry?.LineNumber ?? 0
        });
        
        // INFERENCES: Check for related demand/quantity changes
        var quantityChanged = comparison.Differences.Any(d => 
            d.Type == DifferenceType.QuantityChanged && 
            d.JobNumber == diff.JobNumber);
            
        if (quantityChanged)
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Date shift likely caused by quantity increase and capacity constraints",
                ConfidenceLevel = 0.80,
                SupportingReasons = new() { 
                    "Quantity change detected for same job",
                    "Additional capacity needed to meet higher demand"
                }
            });
        }
        else
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Date shift may be due to resource availability or scheduling changes",
                ConfidenceLevel = 0.65,
                SupportingReasons = new() { 
                    "No quantity change detected",
                    "Suggests resource or calendar constraint"
                }
            });
        }
        
        // Next steps
        explanation.NextStepsInEpicor.Add("Check Resource Scheduling for capacity conflicts");
        explanation.NextStepsInEpicor.Add("Review Load Leveling settings");
        explanation.NextStepsInEpicor.Add("Check job routing for operation duration changes");
        
        return explanation;
    }
    
    private Explanation ExplainNewError(MrpDifference diff, MrpLogComparison comparison)
    {
        var explanation = new Explanation
        {
            RelatedDifference = diff,
            Summary = $"New error appeared in Run B for Part {diff.PartNumber}"
        };
        
        // FACTS
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Part {diff.PartNumber} processed successfully in Run A",
            LogEvidence = diff.RunAEntry?.RawLine ?? "(no error in Run A)",
            LineNumber = diff.RunAEntry?.LineNumber ?? 0
        });
        
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Run B logged error: {diff.RunBEntry?.ErrorMessage}",
            LogEvidence = diff.RunBEntry?.RawLine ?? "",
            LineNumber = diff.RunBEntry?.LineNumber ?? 0
        });
        
        // INFERENCES based on error type
        var errorMsg = diff.RunBEntry?.ErrorMessage?.ToLower() ?? "";
        
        if (errorMsg.Contains("timeout"))
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Timeout likely caused by network latency or database performance",
                ConfidenceLevel = 0.75,
                SupportingReasons = new() { 
                    "Timeout errors are often infrastructure-related",
                    "Part processed successfully in previous run"
                }
            });
        }
        else if (errorMsg.Contains("cannot") || errorMsg.Contains("exception"))
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Data or configuration change may have caused the error",
                ConfidenceLevel = 0.70,
                SupportingReasons = new() { 
                    "Exception suggests data validation issue",
                    "Recent changes to part master or BOM possible"
                }
            });
        }
        
        // Next steps
        explanation.NextStepsInEpicor.Add("Check System Monitor for performance issues");
        explanation.NextStepsInEpicor.Add("Review Part Master for recent changes");
        explanation.NextStepsInEpicor.Add("Check database logs for errors at same timestamp");
        
        return explanation;
    }
    
    private Explanation ExplainQuantityChange(MrpDifference diff, MrpLogComparison comparison)
    {
        // Similar structure to above methods
        // FACTS: Original quantity vs new quantity
        // INFERENCES: Demand change, forecast adjustment, manual override
        // Next steps: Check demand sources, forecast sheets
    }
    
    private Explanation ExplainJobAppearance(MrpDifference diff, MrpLogComparison comparison)
    {
        // FACTS: Job present in B, not in A
        // INFERENCES: New demand, regen processing, part reactivation
        // Next steps: Check demand sources, sales orders
    }
    
    private Explanation ExplainErrorResolution(MrpDifference diff, MrpLogComparison comparison)
    {
        // FACTS: Error in A, resolved in B
        // INFERENCES: Manual fix, data correction, system recovery
        // Next steps: Verify fix is permanent
    }
    
    private MrpLogEntry? FindRelatedError(string? jobNumber, MrpLogDocument document)
    {
        if (string.IsNullOrEmpty(jobNumber)) return null;
        
        return document.Entries.FirstOrDefault(e => 
            e.EntryType == MrpLogEntryType.Error && 
            e.JobNumber == jobNumber);
    }
    
    private Explanation CreateGenericExplanation(MrpDifference diff)
    {
        // Fallback for unhandled difference types
    }
}
```

## 🧪 Acceptance Criteria
- [ ] Explanation models created (Explanation, ExplanationFact, ExplanationInference)
- [ ] ExplanationEngine implemented with all explanation methods
- [ ] Generates explanations for all 5+ difference types
- [ ] Clearly separates FACT from INFERENCE
- [ ] Assigns confidence levels (0.0-1.0) to inferences
- [ ] Provides actionable "Next Steps in Epicor"
- [ ] Language is planner-friendly (no jargon like "NRE" or "mutex")
- [ ] Facts reference actual log lines with line numbers
- [ ] Inferences include supporting reasons
- [ ] Confidence levels are reasonable (0.6-0.9 range)

## 🧪 Test Scenario

**Input Difference**:
- Type: JobRemoved
- JobNumber: "14567"
- RunAEntry: "ERROR: Job 14567 abandoned due to timeout" (line 42)

**Expected Explanation**:
```
Summary: "Job 14567 disappeared between runs"

Facts:
- "Job 14567 present in Run A at line 42"
  Evidence: "ERROR: Job 14567 abandoned due to timeout"
- "Job 14567 not found in Run B"
  Evidence: "(absence in log)"
- "Run A logged error: Job 14567 abandoned due to timeout"
  Evidence: [full log line]

Inferences:
- "Job may have been automatically removed due to timeout"
  Confidence: 0.85
  Reasons: ["Timeout errors often trigger automatic cleanup", "Common in Net Change runs"]

Next Steps:
- "Check Job Tracker for deletion history"
- "Review Job Entry screen for manual changes"
- "Check MRP processing logs for cleanup actions"
```

## 📝 Notes
- Use simple language planners can understand
- Confidence levels should reflect certainty: 0.9+ = very likely, 0.7-0.9 = likely, 0.5-0.7 = possible
- Always provide at least 1 FACT and 1 INFERENCE
- Next steps should be specific Epicor screens/reports
- Consider correlating multiple differences (e.g., error + job disappearance)
- Next issue will format these explanations into readable reports
