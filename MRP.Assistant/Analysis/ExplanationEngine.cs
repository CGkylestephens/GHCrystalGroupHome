using MRP.Assistant.Core;

namespace MRP.Assistant.Analysis;

public class ExplanationEngine
{
    public List<Explanation> GenerateExplanations(MrpLogComparison comparison)
    {
        var explanations = new List<Explanation>();
        
        foreach (var diff in comparison.Differences)
        {
            if (diff.Type == DifferenceType.JobRemoved)
            {
                explanations.Add(new Explanation
                {
                    Type = ExplanationType.Fact,
                    Text = $"Job {diff.JobNumber} was present in Run A but not in Run B",
                    Confidence = 1.0
                });
                
                if (diff.Description.Contains("timeout") || diff.Description.Contains("error"))
                {
                    explanations.Add(new Explanation
                    {
                        Type = ExplanationType.Inference,
                        Text = $"Job {diff.JobNumber} may have been removed due to previous errors",
                        Confidence = 0.75
                    });
                }
            }
            else if (diff.Type == DifferenceType.DateShifted)
            {
                explanations.Add(new Explanation
                {
                    Type = ExplanationType.Fact,
                    Text = $"{diff.Description}",
                    Confidence = 1.0
                });
            }
        }
        
        return explanations;
    }
}
