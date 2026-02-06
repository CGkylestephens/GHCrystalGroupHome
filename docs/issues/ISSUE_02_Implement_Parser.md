---
name: Implement MRP Log Parser
about: Build robust parser to extract structured data from unstructured MRP logs
title: "[Agent Task] Implement MRP Log Parser"
labels: [parser, agent]
assignees: [copilot]
---

## 🧠 Task Intent
Implement a robust log parser that extracts structured data (Parts, Jobs, Demand, Supply, Errors) from unstructured Epicor MRP log files. This is the foundation for all comparison and analysis functionality.

## 🔍 Scope / Input
**Dependencies**: Issue #1 must be complete (project structure exists)

**Input Files** (in `/testdata/`):
- `mrp_log_sample_A.txt` - Simple sample with error
- `mrp_log_sample_B.txt` - Simple sample without job
- `MRPRegenSample.txt` - Real regen log with pegging data
- `MRPNETChangeSample.txt` - Real net change log

**Reference**:
- `/copilot-instructions.md` - Parsing rules and keywords
- `/testdata/ExtractLogSample.ps1` - Keywords to detect

## ✅ Expected Output

### 1. Core Domain Models (in `Core/` folder)

**MrpLogEntry.cs**:
```csharp
namespace MRP.Assistant.Core;

public class MrpLogEntry
{
    public DateTime? Timestamp { get; set; }
    public string RawLine { get; set; } = string.Empty;
    public MrpLogEntryType EntryType { get; set; }
    public string? PartNumber { get; set; }
    public string? JobNumber { get; set; }
    public DemandInfo? Demand { get; set; }
    public SupplyInfo? Supply { get; set; }
    public string? ErrorMessage { get; set; }
    public int LineNumber { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum MrpLogEntryType
{
    Unknown,
    ProcessingPart,
    Demand,
    Supply,
    Pegging,
    Error,
    Warning,
    SystemInfo,
    Timestamp
}
```

**DemandInfo.cs**:
```csharp
namespace MRP.Assistant.Core;

public class DemandInfo
{
    public string Type { get; set; } = string.Empty; // S (Sales), T (Transfer), etc.
    public string Order { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Release { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Quantity { get; set; }
}
```

**SupplyInfo.cs**:
```csharp
namespace MRP.Assistant.Core;

public class SupplyInfo
{
    public string Type { get; set; } = string.Empty; // J (Job), P (PO), etc.
    public string JobNumber { get; set; } = string.Empty;
    public int Assembly { get; set; }
    public int Material { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Quantity { get; set; }
}
```

**MrpLogDocument.cs**:
```csharp
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
    
    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue 
        ? EndTime.Value - StartTime.Value 
        : null;
}

public enum MrpRunType
{
    Unknown,
    Regeneration,
    NetChange
}
```

### 2. Parser Implementation (in `Parsers/` folder)

**MrpLogParser.cs**:
```csharp
namespace MRP.Assistant.Parsers;

public class MrpLogParser
{
    private readonly List<string> _errorKeywords = new()
    {
        "cannot", "can not", "will not", "won't", "wont",
        "error", "exception", "defunct", "abandoned",
        "timeout", "deadlock", "cancel", "cancelling"
    };
    
    public MrpLogDocument Parse(string filePath)
    {
        // Implementation:
        // 1. Read file line by line
        // 2. Detect run type from filename or content
        // 3. Parse each line with ParseLine()
        // 4. Extract metadata (start/end time, site)
        // 5. Return populated MrpLogDocument
    }
    
    private MrpLogEntry ParseLine(string line, int lineNumber)
    {
        // Implementation:
        // 1. Try to extract timestamp
        // 2. Detect entry type
        // 3. Extract relevant fields based on type
        // 4. Return MrpLogEntry
    }
    
    private MrpLogEntryType DetectEntryType(string line)
    {
        // Check for error keywords first (highest priority)
        // Then check for Part:, Job, Demand:, Supply:, Pegged
        // Return appropriate enum value
    }
    
    private DateTime? ExtractTimestamp(string line)
    {
        // Match HH:mm:ss at start of line
        // Return DateTime with time component, today's date
    }
    
    private string? ExtractPartNumber(string line)
    {
        // Look for "Part:" or "Processing Part:"
        // Extract part number (may contain - , alphanumeric)
    }
    
    private string? ExtractJobNumber(string line)
    {
        // Look for "Job", "J:", or job patterns
        // Extract job number (may have U, F prefix or be numeric)
    }
    
    private DemandInfo? ParseDemand(string line)
    {
        // Pattern: "Demand: S: 100516/1/1 Date: 6/15/2026 Quantity: 4.00000000"
        // Extract type, order, line, release, date, quantity
    }
    
    private SupplyInfo? ParseSupply(string line)
    {
        // Pattern: "Supply: J: U0000000000273/0/0 Date: 6/15/2026 Quantity: 4.00000000"
        // Extract type, job, assembly, material, date, quantity
    }
    
    private MrpRunType DetectRunType(string filename, List<string> lines)
    {
        // Check filename for "REGEN", "NET", "NetChange"
        // Or inspect log content for run type indicators
    }
}
```

## 🧪 Acceptance Criteria
- [ ] All domain models created in `Core/` folder
- [ ] MrpLogParser created in `Parsers/` folder
- [ ] Parser successfully reads all 4 sample files
- [ ] Extracts timestamps (HH:mm:ss format)
- [ ] Extracts Part numbers from "Part:", "Processing Part:" lines
- [ ] Extracts Job numbers from various formats (U-prefix, F-prefix, numeric)
- [ ] Parses Demand entries (S: order/line/rel Date: X Quantity: Y)
- [ ] Parses Supply entries (J: job/asm/mtl Date: X Quantity: Y)
- [ ] Detects error keywords (error, cannot, timeout, etc.)
- [ ] Handles malformed lines gracefully (logs to ParsingErrors, continues)
- [ ] Returns structured MrpLogDocument with metadata
- [ ] No exceptions thrown on valid log files

## 🧪 Validation Commands
```bash
# Build project
dotnet build MRP.Assistant/MRP.Assistant.csproj

# Create simple test program to validate parser
dotnet run --project MRP.Assistant.CLI -- parse testdata/mrp_log_sample_A.txt
```

## 📝 Parsing Examples

**Input line**: `01:05:07 Processing Part:CTL-00025, Attribute Set:''`
**Expected**: 
- Timestamp: 01:05:07
- EntryType: ProcessingPart
- PartNumber: "CTL-00025"

**Input line**: `01:05:07 Demand: S: 100516/1/1 Date: 6/15/2026 Quantity: 4.00000000`
**Expected**:
- Timestamp: 01:05:07
- EntryType: Demand
- Demand.Type: "S"
- Demand.Order: "100516"
- Demand.Line: 1
- Demand.Release: 1
- Demand.DueDate: 6/15/2026
- Demand.Quantity: 4.0

**Input line**: `ERROR: Job 14567 abandoned due to timeout`
**Expected**:
- EntryType: Error
- JobNumber: "14567"
- ErrorMessage: "Job 14567 abandoned due to timeout"

## 📝 Notes
- Prioritize clarity over performance at this stage
- Log parsing warnings to ParsingErrors list, don't fail
- Use regex cautiously (only where necessary)
- Test with all 4 sample files before completing
- Next issue will use this parser for log comparison
