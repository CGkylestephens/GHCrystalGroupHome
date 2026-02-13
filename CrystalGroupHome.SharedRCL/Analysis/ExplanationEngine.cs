using MRP.Assistant.Core;

namespace MRP.Assistant.Analysis;

public class Explanation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Facts { get; set; } = new();
    public List<string> Inferences { get; set; } = new();
    public double Confidence { get; set; }
}

public class ExplanationEngine
{
    public List<Explanation> GenerateExplanations(MrpLogComparison comparison)
    {
        var explanations = new List<Explanation>();

        // Generate explanations for each type of difference
        var jobChanges = comparison.Differences.Where(d => 
            d.Type == DifferenceType.JobAdded || d.Type == DifferenceType.JobRemoved).ToList();

        if (jobChanges.Any())
        {
            explanations.Add(new Explanation
            {
                Title = "Job Changes Detected",
                Description = $"Found {jobChanges.Count} job-related changes between runs",
                Facts = new List<string>
                {
                    $"✅ {comparison.Differences.Count(d => d.Type == DifferenceType.JobAdded)} jobs added",
                    $"✅ {comparison.Differences.Count(d => d.Type == DifferenceType.JobRemoved)} jobs removed"
                },
                Inferences = new List<string>
                {
                    "🔍 Job changes may indicate schedule adjustments or demand changes",
                    "🔍 Review recent sales orders or engineering changes"
                },
                Confidence = 0.75
            });
        }

        var errorChanges = comparison.Differences.Where(d =>
            d.Type == DifferenceType.ErrorAppeared || d.Type == DifferenceType.ErrorResolved).ToList();

        if (errorChanges.Any())
        {
            var criticalErrors = errorChanges.Where(d => d.Severity == Severity.Critical);
            explanations.Add(new Explanation
            {
                Title = "Error Status Changes",
                Description = $"Error conditions changed between runs",
                Facts = new List<string>
                {
                    $"✅ {comparison.Differences.Count(d => d.Type == DifferenceType.ErrorAppeared)} new errors",
                    $"✅ {comparison.Differences.Count(d => d.Type == DifferenceType.ErrorResolved)} errors resolved"
                },
                Inferences = new List<string>
                {
                    "🔍 New errors may indicate data quality issues",
                    "🔍 Check for missing part records or invalid BOMs"
                },
                Confidence = criticalErrors.Any() ? 0.85 : 0.65
            });
        }

        // Add a default explanation if no specific patterns found
        if (!explanations.Any())
        {
            explanations.Add(new Explanation
            {
                Title = "No Significant Changes",
                Description = "Runs appear similar with minimal differences",
                Facts = new List<string>
                {
                    $"✅ Total differences: {comparison.Summary.TotalDifferences}"
                },
                Inferences = new List<string>(),
                Confidence = 0.90
            });
        }

        return explanations;
    }
}
