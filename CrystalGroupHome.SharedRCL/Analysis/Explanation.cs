using CrystalGroupHome.SharedRCL.Core;

namespace CrystalGroupHome.SharedRCL.Analysis;

/// <summary>
/// A fact that can be directly verified from MRP log content
/// </summary>
public class ExplanationFact
{
    public string Statement { get; set; } = string.Empty;
    public string LogEvidence { get; set; } = string.Empty; // Actual log line
    public int LineNumber { get; set; }
}

/// <summary>
/// An inference or plausible explanation based on observed facts
/// </summary>
public class ExplanationInference
{
    public string Statement { get; set; } = string.Empty;
    public double ConfidenceLevel { get; set; } // 0.0 - 1.0
    public List<string> SupportingReasons { get; set; } = new();
}

/// <summary>
/// A complete explanation for a detected difference between MRP runs
/// </summary>
public class Explanation
{
    public MrpDifference RelatedDifference { get; set; } = null!;
    public string Summary { get; set; } = string.Empty; // One-line what happened
    public List<ExplanationFact> Facts { get; set; } = new();
    public List<ExplanationInference> Inferences { get; set; } = new();
    public List<string> NextStepsInEpicor { get; set; } = new();
}
