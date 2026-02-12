namespace CrystalGroupHome.SharedRCL.Core;

/// <summary>
/// Represents a single entry in an MRP log
/// </summary>
public class MrpLogEntry
{
    public int LineNumber { get; set; }
    public string RawLine { get; set; } = string.Empty;
    public MrpLogEntryType EntryType { get; set; }
    public string? JobNumber { get; set; }
    public string? PartNumber { get; set; }
    public DateTime? DueDate { get; set; }
    public int? Quantity { get; set; }
    public string? ErrorMessage { get; set; }
}
