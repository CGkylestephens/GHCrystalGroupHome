namespace CrystalGroupHome.SharedRCL.Core;

/// <summary>
/// Represents a difference detected between two MRP runs
/// </summary>
public class MrpDifference
{
    public DifferenceType Type { get; set; }
    public string? JobNumber { get; set; }
    public string? PartNumber { get; set; }
    public MrpLogEntry? RunAEntry { get; set; }
    public MrpLogEntry? RunBEntry { get; set; }
    public Dictionary<string, string> Details { get; set; } = new();
}
