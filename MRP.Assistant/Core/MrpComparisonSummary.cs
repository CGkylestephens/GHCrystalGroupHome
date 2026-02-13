namespace MRP.Assistant.Core;

public class MrpComparisonSummary
{
    public int TotalDifferences { get; set; }
    public int CriticalDifferences { get; set; }
    public int WarningDifferences { get; set; }
    public int InfoDifferences { get; set; }
}
