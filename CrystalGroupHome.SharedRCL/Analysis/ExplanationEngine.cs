using CrystalGroupHome.SharedRCL.Core;

namespace CrystalGroupHome.SharedRCL.Analysis;

/// <summary>
/// Engine that generates planner-friendly explanations for MRP log differences,
/// clearly distinguishing between FACT (log-supported evidence) and INFERENCE (plausible causes)
/// </summary>
public class ExplanationEngine
{
    /// <summary>
    /// Generates explanations for all differences in a comparison
    /// </summary>
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
    
    private Explanation ExplainJobAppearance(MrpDifference diff, MrpLogComparison comparison)
    {
        var explanation = new Explanation
        {
            RelatedDifference = diff,
            Summary = $"Job {diff.JobNumber} appeared in Run B"
        };
        
        // FACTS
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Job {diff.JobNumber} not present in Run A",
            LogEvidence = "(absence in log)",
            LineNumber = 0
        });
        
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Job {diff.JobNumber} found in Run B at line {diff.RunBEntry?.LineNumber}",
            LogEvidence = diff.RunBEntry?.RawLine ?? "",
            LineNumber = diff.RunBEntry?.LineNumber ?? 0
        });
        
        // INFERENCES
        var runType = comparison.RunB.Metadata.RunType?.ToLower() ?? "";
        
        if (runType.Contains("regen"))
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Job likely created by regeneration process recalculating all requirements",
                ConfidenceLevel = 0.80,
                SupportingReasons = new() { "Regen runs recalculate all demands from scratch", "New demand source may have been added" }
            });
        }
        else if (runType.Contains("net"))
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Job created due to new demand added since last run",
                ConfidenceLevel = 0.85,
                SupportingReasons = new() { "Net Change processes only changed demands", "New sales order or forecast entry likely" }
            });
        }
        else
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Job may have been created due to new or reactivated demand",
                ConfidenceLevel = 0.70,
                SupportingReasons = new() { "Job appeared in second run", "Suggests demand change or part reactivation" }
            });
        }
        
        // Next steps
        explanation.NextStepsInEpicor.Add("Check Time Phase Inquiry for new demand sources");
        explanation.NextStepsInEpicor.Add("Review Sales Order Entry for recent orders");
        explanation.NextStepsInEpicor.Add("Check Forecast Entry for demand changes");
        explanation.NextStepsInEpicor.Add("Verify part is active in Part Master");
        
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
    
    private Explanation ExplainQuantityChange(MrpDifference diff, MrpLogComparison comparison)
    {
        var explanation = new Explanation
        {
            RelatedDifference = diff,
            Summary = $"Job {diff.JobNumber} quantity changed"
        };
        
        // Extract quantity details from difference
        var originalQty = diff.Details.GetValueOrDefault("OriginalQuantity");
        var newQty = diff.Details.GetValueOrDefault("NewQuantity");
        
        // FACTS
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Job {diff.JobNumber} quantity was {originalQty} in Run A",
            LogEvidence = diff.RunAEntry?.RawLine ?? "",
            LineNumber = diff.RunAEntry?.LineNumber ?? 0
        });
        
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Job {diff.JobNumber} quantity is {newQty} in Run B",
            LogEvidence = diff.RunBEntry?.RawLine ?? "",
            LineNumber = diff.RunBEntry?.LineNumber ?? 0
        });
        
        // INFERENCES: Determine if increase or decrease
        var isIncrease = false;
        if (int.TryParse(originalQty, out var origQtyInt) && int.TryParse(newQty, out var newQtyInt))
        {
            isIncrease = newQtyInt > origQtyInt;
        }
        
        if (isIncrease)
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Quantity increase likely due to additional demand from sales orders or forecast",
                ConfidenceLevel = 0.80,
                SupportingReasons = new() { 
                    "Quantity increased between runs",
                    "Suggests new or increased customer demand"
                }
            });
        }
        else
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Quantity decrease may be due to demand reduction or order cancellation",
                ConfidenceLevel = 0.75,
                SupportingReasons = new() { 
                    "Quantity decreased between runs",
                    "Suggests reduced forecast or cancelled orders"
                }
            });
        }
        
        // Next steps
        explanation.NextStepsInEpicor.Add("Check Time Phase Inquiry for demand source changes");
        explanation.NextStepsInEpicor.Add("Review Sales Order Entry for order modifications");
        explanation.NextStepsInEpicor.Add("Check Forecast Entry for forecast adjustments");
        explanation.NextStepsInEpicor.Add("Verify inventory levels and on-hand quantities");
        
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
        else
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Error may be due to system state or data changes between runs",
                ConfidenceLevel = 0.60,
                SupportingReasons = new() { 
                    "Error appeared in second run only",
                    "Suggests environmental or data change"
                }
            });
        }
        
        // Next steps
        explanation.NextStepsInEpicor.Add("Check System Monitor for performance issues");
        explanation.NextStepsInEpicor.Add("Review Part Master for recent changes");
        explanation.NextStepsInEpicor.Add("Check database logs for errors at same timestamp");
        explanation.NextStepsInEpicor.Add("Verify BOM and routing data integrity");
        
        return explanation;
    }
    
    private Explanation ExplainErrorResolution(MrpDifference diff, MrpLogComparison comparison)
    {
        var explanation = new Explanation
        {
            RelatedDifference = diff,
            Summary = $"Error resolved in Run B for Part {diff.PartNumber}"
        };
        
        // FACTS
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Run A logged error: {diff.RunAEntry?.ErrorMessage}",
            LogEvidence = diff.RunAEntry?.RawLine ?? "",
            LineNumber = diff.RunAEntry?.LineNumber ?? 0
        });
        
        explanation.Facts.Add(new ExplanationFact
        {
            Statement = $"Part {diff.PartNumber} processed successfully in Run B",
            LogEvidence = diff.RunBEntry?.RawLine ?? "(no error in Run B)",
            LineNumber = diff.RunBEntry?.LineNumber ?? 0
        });
        
        // INFERENCES based on error type
        var errorMsg = diff.RunAEntry?.ErrorMessage?.ToLower() ?? "";
        
        if (errorMsg.Contains("timeout"))
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Timeout resolved, likely due to improved system performance or reduced load",
                ConfidenceLevel = 0.70,
                SupportingReasons = new() { 
                    "Timeout errors often resolve with system improvements",
                    "May be transient infrastructure issue"
                }
            });
        }
        else if (errorMsg.Contains("cannot") || errorMsg.Contains("exception"))
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Error fixed by data correction or configuration change",
                ConfidenceLevel = 0.80,
                SupportingReasons = new() { 
                    "Data validation errors typically require manual fixes",
                    "Resolution suggests deliberate correction"
                }
            });
        }
        else
        {
            explanation.Inferences.Add(new ExplanationInference
            {
                Statement = "Error may have been resolved by system recovery or data correction",
                ConfidenceLevel = 0.65,
                SupportingReasons = new() { 
                    "Error cleared in second run",
                    "Suggests intervention or automatic recovery"
                }
            });
        }
        
        // Next steps
        explanation.NextStepsInEpicor.Add("Verify fix is permanent by monitoring future runs");
        explanation.NextStepsInEpicor.Add("Document what was changed to resolve the error");
        explanation.NextStepsInEpicor.Add("Check if similar errors exist for other parts");
        
        return explanation;
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
        var explanation = new Explanation
        {
            RelatedDifference = diff,
            Summary = $"Difference detected: {diff.Type}"
        };
        
        // Generic FACTS
        if (diff.RunAEntry != null)
        {
            explanation.Facts.Add(new ExplanationFact
            {
                Statement = $"Entry found in Run A at line {diff.RunAEntry.LineNumber}",
                LogEvidence = diff.RunAEntry.RawLine,
                LineNumber = diff.RunAEntry.LineNumber
            });
        }
        
        if (diff.RunBEntry != null)
        {
            explanation.Facts.Add(new ExplanationFact
            {
                Statement = $"Entry found in Run B at line {diff.RunBEntry.LineNumber}",
                LogEvidence = diff.RunBEntry.RawLine,
                LineNumber = diff.RunBEntry.LineNumber
            });
        }
        
        // Generic INFERENCE
        explanation.Inferences.Add(new ExplanationInference
        {
            Statement = "Change detected between runs - manual review recommended",
            ConfidenceLevel = 0.50,
            SupportingReasons = new() { "Difference type requires additional context for detailed explanation" }
        });
        
        // Generic next steps
        explanation.NextStepsInEpicor.Add("Review the log entries for this difference");
        explanation.NextStepsInEpicor.Add("Compare MRP settings between runs");
        
        return explanation;
    }
}
