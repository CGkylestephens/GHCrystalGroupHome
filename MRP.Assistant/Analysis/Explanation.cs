namespace MRP.Assistant.Analysis;

public enum ExplanationType
{
    Fact,
    Inference
}

public class Explanation
{
    public ExplanationType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; } = 1.0;  // 0.0 to 1.0
    public List<int>? EvidenceLines { get; set; }
}
