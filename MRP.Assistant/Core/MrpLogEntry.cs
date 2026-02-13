namespace MRP.Assistant.Core;

public class MrpLogEntry
{
    public int LineNumber { get; set; }
    public string RawLine { get; set; } = string.Empty;
    public MrpLogEntryType EntryType { get; set; }
    public string? PartNumber { get; set; }
    public string? JobNumber { get; set; }
    public string? DemandSource { get; set; }  // e.g., "S: 100516/1/1"
    public string? SupplySource { get; set; }   // e.g., "J: F340394/0/0"
    public DateTime? Date { get; set; }
    public decimal? Quantity { get; set; }
    public string? ErrorMessage { get; set; }
}
