namespace MRP.Assistant.Core;

public class MrpDifference
{
    public DifferenceType Type { get; set; }
    public DifferenceSeverity Severity { get; set; }
    public string? PartNumber { get; set; }
    public string? JobNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
}
