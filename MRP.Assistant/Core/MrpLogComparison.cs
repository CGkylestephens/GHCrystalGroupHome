namespace MRP.Assistant.Core;

public class MrpLogComparison
{
    public MrpLogDocument RunA { get; set; } = null!;
    public MrpLogDocument RunB { get; set; } = null!;
    public List<MrpDifference> Differences { get; set; } = new();
    public MrpComparisonSummary Summary { get; set; } = new();
}
