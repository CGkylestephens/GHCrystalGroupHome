namespace CrystalGroupHome.SharedRCL.Core;

/// <summary>
/// Result of comparing two MRP log documents
/// </summary>
public class MrpLogComparison
{
    public MrpLogDocument RunA { get; set; } = new();
    public MrpLogDocument RunB { get; set; } = new();
    public List<MrpDifference> Differences { get; set; } = new();
}
