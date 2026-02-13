namespace MRP.Assistant.Core;

public enum MrpLogEntryType
{
    Info,
    Warning,
    Error,
    ProcessingStart,
    ProcessingEnd
}

public class MrpLogEntry
{
    public int LineNumber { get; set; }
    public string? Timestamp { get; set; }
    public MrpLogEntryType EntryType { get; set; }
    public string? Message { get; set; }
    public string? PartNumber { get; set; }
    public string? JobNumber { get; set; }
    public string? DemandInfo { get; set; }
    public string? SupplyInfo { get; set; }
}

public class MrpLogDocument
{
    public string SourceFile { get; set; } = string.Empty;
    public string? Site { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue 
        ? EndTime.Value - StartTime.Value 
        : null;
    public string RunType { get; set; } = "unknown";
    public List<MrpLogEntry> Entries { get; set; } = new();
    public List<string> ParsingErrors { get; set; } = new();
}

public enum DifferenceType
{
    JobAdded,
    JobRemoved,
    DateShifted,
    QuantityChanged,
    ErrorAppeared,
    ErrorResolved
}

public enum Severity
{
    Info,
    Warning,
    Critical
}

public class MrpDifference
{
    public DifferenceType Type { get; set; }
    public Severity Severity { get; set; }
    public string? PartNumber { get; set; }
    public string? JobNumber { get; set; }
    public string? Description { get; set; }
    public string? RunAValue { get; set; }
    public string? RunBValue { get; set; }
    public int? RunALineNumber { get; set; }
    public int? RunBLineNumber { get; set; }
}

public class ComparisonSummary
{
    public int TotalDifferences { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
}

public class MrpLogComparison
{
    public MrpLogDocument RunA { get; set; } = new();
    public MrpLogDocument RunB { get; set; } = new();
    public List<MrpDifference> Differences { get; set; } = new();
    public ComparisonSummary Summary { get; set; } = new();
}
