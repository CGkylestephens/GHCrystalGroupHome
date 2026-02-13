namespace MRP.Assistant.Core;

public class MrpLogDocument
{
    public string SourceFile { get; set; } = string.Empty;
    public MrpRunType RunType { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Site { get; set; }
    public List<MrpLogEntry> Entries { get; set; } = new();
    public List<string> ParsingErrors { get; set; } = new();
}
