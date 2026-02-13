---
name: Build Log Comparison Engine
about: Implement engine to detect differences between two MRP log runs
title: "[Agent Task] Build Log Comparison Engine"
labels: [diff, agent]
assignees: [copilot]
---

## 🧠 Task Intent
Implement a comparison engine that analyzes two parsed MRP log documents and detects meaningful differences: job appearance/disappearance, date shifts, quantity changes, and new errors.

## 🔍 Scope / Input
**Dependencies**: Issue #2 must be complete (parser exists)

**Input**: Two `MrpLogDocument` objects (from parser)

**Test Data**:
- Compare `mrp_log_sample_A.txt` vs `mrp_log_sample_B.txt`
- Expected: Detect job 14567 disappearance, Part ABC123 changes

**Reference**: `/docs/MRP_ASSISTANT_PROJECT_PLAN.md` (Issue 3 section)

## ✅ Expected Output

### 1. Comparison Models (in `Analysis/` folder)

**MrpLogComparison.cs**:
```csharp
namespace MRP.Assistant.Analysis;

public class MrpLogComparison
{
    public MrpLogDocument RunA { get; set; } = null!;
    public MrpLogDocument RunB { get; set; } = null!;
    public List<MrpDifference> Differences { get; set; } = new();
    public ComparisonSummary Summary { get; set; } = new();
}

public class ComparisonSummary
{
    public int TotalDifferences { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int JobsAdded { get; set; }
    public int JobsRemoved { get; set; }
    public int DateShifts { get; set; }
    public int QuantityChanges { get; set; }
    public int NewErrors { get; set; }
}
```

**MrpDifference.cs**:
```csharp
namespace MRP.Assistant.Analysis;

public class MrpDifference
{
    public DifferenceType Type { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? JobNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public MrpLogEntry? RunAEntry { get; set; }
    public MrpLogEntry? RunBEntry { get; set; }
    public DifferenceSeverity Severity { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

public enum DifferenceType
{
    JobAdded,
    JobRemoved,
    DateShifted,
    QuantityChanged,
    ErrorAppeared,
    ErrorResolved,
    PartAppeared,
    PartRemoved
}

public enum DifferenceSeverity
{
    Info,
    Warning,
    Critical
}
```

### 2. Comparison Engine (in `Analysis/` folder)

**MrpLogComparer.cs**:
```csharp
namespace MRP.Assistant.Analysis;

public class MrpLogComparer
{
    public MrpLogComparison Compare(MrpLogDocument runA, MrpLogDocument runB)
    {
        var comparison = new MrpLogComparison
        {
            RunA = runA,
            RunB = runB
        };
        
        // Detect all difference types
        comparison.Differences.AddRange(DetectJobDifferences(runA, runB));
        comparison.Differences.AddRange(DetectDateShifts(runA, runB));
        comparison.Differences.AddRange(DetectQuantityChanges(runA, runB));
        comparison.Differences.AddRange(DetectErrorDifferences(runA, runB));
        comparison.Differences.AddRange(DetectPartDifferences(runA, runB));
        
        // Generate summary
        comparison.Summary = GenerateSummary(comparison.Differences);
        
        return comparison;
    }
    
    private List<MrpDifference> DetectJobDifferences(MrpLogDocument runA, MrpLogDocument runB)
    {
        // Extract all job numbers from both runs
        // Identify jobs in A but not B (removed)
        // Identify jobs in B but not A (added)
        // Create MrpDifference for each
    }
    
    private List<MrpDifference> DetectDateShifts(MrpLogDocument runA, MrpLogDocument runB)
    {
        // For jobs present in both runs
        // Compare supply/demand dates
        // Flag if difference > 1 day
        // Severity: >7 days = Critical, >3 days = Warning, else Info
    }
    
    private List<MrpDifference> DetectQuantityChanges(MrpLogDocument runA, MrpLogDocument runB)
    {
        // For supply/demand entries in both runs
        // Calculate percentage change
        // Flag if > 5% difference
        // Severity: >50% = Critical, >20% = Warning, else Info
    }
    
    private List<MrpDifference> DetectErrorDifferences(MrpLogDocument runA, MrpLogDocument runB)
    {
        // Extract error entries from both
        // New errors (in B not in A) → ErrorAppeared
        // Resolved errors (in A not in B) → ErrorResolved
    }
    
    private List<MrpDifference> DetectPartDifferences(MrpLogDocument runA, MrpLogDocument runB)
    {
        // Extract parts processed in each run
        // Identify parts in B not in A (appeared)
        // Identify parts in A not in B (removed)
    }
    
    private ComparisonSummary GenerateSummary(List<MrpDifference> differences)
    {
        // Count differences by severity and type
        // Return aggregated summary
    }
}
```

## 🧪 Acceptance Criteria
- [ ] MrpLogComparison, MrpDifference models created
- [ ] MrpLogComparer implemented with all detection methods
- [ ] Detects job additions (jobs in B not in A)
- [ ] Detects job removals (jobs in A not in B)
- [ ] Detects date shifts (>1 day difference)
- [ ] Detects quantity changes (>5% variance)
- [ ] Detects new errors (errors in B not in A)
- [ ] Detects resolved errors (errors in A not in B)
- [ ] Assigns appropriate severity levels
- [ ] Generates summary statistics
- [ ] Comparison completes in <5 seconds for 10,000 line logs

## 🧪 Test Scenarios

### Scenario 1: Job Disappearance
**Input**: 
- Run A has job 14567 with error "abandoned due to timeout"
- Run B does not have job 14567

**Expected Output**:
```
Type: JobRemoved
JobNumber: "14567"
Severity: Critical
Description: "Job 14567 present in Run A but missing in Run B"
RunAEntry: (entry with job 14567)
RunBEntry: null
```

### Scenario 2: Part Reappearance
**Input**:
- Run A: "Part: ABC123 removed from planning"
- Run B: "Part: ABC123 reappears with new demand"

**Expected Output**:
```
Type: PartAppeared
PartNumber: "ABC123"
Severity: Warning
Description: "Part ABC123 reappeared in Run B after being removed in Run A"
```

### Scenario 3: Date Shift
**Input**:
- Run A: Job X date 2/15/2026
- Run B: Job X date 2/20/2026

**Expected Output**:
```
Type: DateShifted
JobNumber: "X"
Severity: Warning (5 days)
Details: { "OriginalDate": "2/15/2026", "NewDate": "2/20/2026", "DaysDifference": 5 }
```

## 🧪 Validation Commands
```bash
# Build
dotnet build MRP.Assistant/MRP.Assistant.csproj

# Test comparison with sample files (create temporary test in CLI)
dotnet run --project MRP.Assistant.CLI -- compare \
  --run-a testdata/mrp_log_sample_A.txt \
  --run-b testdata/mrp_log_sample_B.txt
```

## 📝 Notes
- Focus on accuracy over performance initially
- Use HashSet for efficient job/part lookups
- Consider job numbers case-insensitive for matching
- Date comparison should account for time zones (if present)
- Store original entries in MrpDifference for evidence trail
- Next issue will add explanation logic to differences
