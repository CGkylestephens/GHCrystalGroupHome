# MRP Log Comparison Engine

## Overview

The MRP Log Comparison Engine is a service for detecting and analyzing differences between two MRP (Material Requirements Planning) log runs. It identifies changes in jobs, dates, quantities, parts, and errors, providing actionable insights for production planners.

## Components

### Models

#### MrpLogDocument (`Models/MrpLogDocument.cs`)
Container for a parsed MRP log document containing:
- **Metadata**: Run-level information (site, times, status, type)
- **Entries**: List of detailed log entries
- **RawLines**: Original log file content

#### MrpLogEntry (`Models/MrpLogEntry.cs`)
Represents a single log entry with:
- **PartNumber**: The part being processed
- **JobNumber**: Associated job number
- **Date**: Scheduled or processing date
- **Quantity**: Item quantity
- **IsError**: Whether this is an error entry
- **EntryType**: Classification (Job, Part, Supply, Demand, Error, Other)

### Analysis Models

#### MrpLogComparison (`Analysis/MrpLogComparison.cs`)
Result of comparing two log runs:
- **RunA**: First log document (baseline)
- **RunB**: Second log document (comparison)
- **Differences**: List of detected differences
- **Summary**: Aggregated statistics

#### MrpDifference (`Analysis/MrpDifference.cs`)
Represents a single difference:
- **Type**: JobAdded, JobRemoved, DateShifted, QuantityChanged, ErrorAppeared, ErrorResolved, PartAppeared, PartRemoved
- **Severity**: Info, Warning, Critical
- **Description**: Human-readable explanation
- **Details**: Additional metadata (original/new values, percentages, etc.)

#### ComparisonSummary (`Analysis/MrpLogComparison.cs`)
Statistics about the comparison:
- **TotalDifferences**: Count of all differences
- **CriticalCount/WarningCount/InfoCount**: Counts by severity
- **JobsAdded/JobsRemoved**: Job change counts
- **DateShifts**: Number of date changes detected
- **QuantityChanges**: Number of quantity changes detected
- **NewErrors**: Number of new errors appearing

### Services

#### MrpLogDocumentParser (`Services/MrpLogDocumentParser.cs`)
Parses raw MRP log files into structured `MrpLogDocument` objects. Extracts:
- Job numbers and part numbers
- Dates and quantities
- Error indicators
- Entry classifications

#### MrpLogComparer (`Analysis/MrpLogComparer.cs`)
Core comparison engine that detects all difference types between two log runs.

## Usage

### Basic Comparison

```csharp
using CrystalGroupHome.SharedRCL.Analysis;
using CrystalGroupHome.SharedRCL.Services;

// Parse both log files
var parser = new MrpLogDocumentParser();
var runA = await parser.ParseLogFileAsync("mrp_log_run_A.txt");
var runB = await parser.ParseLogFileAsync("mrp_log_run_B.txt");

// Compare the runs
var comparer = new MrpLogComparer();
var comparison = comparer.Compare(runA, runB);

// Access summary
Console.WriteLine($"Total differences: {comparison.Summary.TotalDifferences}");
Console.WriteLine($"Critical issues: {comparison.Summary.CriticalCount}");
Console.WriteLine($"Jobs removed: {comparison.Summary.JobsRemoved}");

// Iterate through differences
foreach (var diff in comparison.Differences)
{
    Console.WriteLine($"[{diff.Severity}] {diff.Type}: {diff.Description}");
}
```

### Filtering by Severity

```csharp
var criticalDiffs = comparison.Differences
    .Where(d => d.Severity == DifferenceSeverity.Critical)
    .ToList();

foreach (var diff in criticalDiffs)
{
    Console.WriteLine($"CRITICAL: {diff.Description}");
}
```

### Filtering by Type

```csharp
var jobChanges = comparison.Differences
    .Where(d => d.Type == DifferenceType.JobRemoved || d.Type == DifferenceType.JobAdded)
    .ToList();

var newErrors = comparison.Differences
    .Where(d => d.Type == DifferenceType.ErrorAppeared)
    .ToList();
```

### Accessing Details

```csharp
foreach (var diff in comparison.Differences)
{
    if (diff.Type == DifferenceType.DateShifted)
    {
        var originalDate = diff.Details["OriginalDate"];
        var newDate = diff.Details["NewDate"];
        var daysDiff = diff.Details["DaysDifference"];
        Console.WriteLine($"Date changed: {originalDate} → {newDate} ({daysDiff} days)");
    }
    
    if (diff.Type == DifferenceType.QuantityChanged)
    {
        var originalQty = diff.Details["OriginalQuantity"];
        var newQty = diff.Details["NewQuantity"];
        var percentChange = diff.Details["PercentChange"];
        Console.WriteLine($"Quantity changed: {originalQty} → {newQty} ({percentChange}% change)");
    }
}
```

## Detection Rules

### Job Differences
- **JobRemoved** (Critical): Job present in Run A but missing in Run B
- **JobAdded** (Warning): Job in Run B that wasn't in Run A

### Date Shifts
Detected when dates differ by more than 1 day:
- **Critical**: >7 days difference
- **Warning**: >3 days difference
- **Info**: 1-3 days difference

### Quantity Changes
Detected when quantities change by more than 5%:
- **Critical**: >50% change
- **Warning**: >20% change
- **Info**: 5-20% change

### Error Differences
- **ErrorAppeared** (Critical): New error in Run B
- **ErrorResolved** (Info): Error from Run A no longer present in Run B

### Part Differences
- **PartRemoved** (Warning): Part in Run A but not in Run B
- **PartAppeared** (Warning): Part in Run B but not in Run A

## Performance

The comparison engine is highly optimized:
- Processes 4,000+ line logs in under 100ms
- Far exceeds the <5 second requirement for 10,000 line logs
- Uses efficient data structures (HashSet, Dictionary) for lookups
- Optimized O(n) algorithms for most comparisons
- Static compiled regex for error identification

## Integration

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddScoped<MrpLogDocumentParser>();
services.AddScoped<MrpLogComparer>();

// In your service
public class MrpAnalysisService
{
    private readonly MrpLogDocumentParser _parser;
    private readonly MrpLogComparer _comparer;

    public MrpAnalysisService(MrpLogDocumentParser parser, MrpLogComparer comparer)
    {
        _parser = parser;
        _comparer = comparer;
    }

    public async Task<MrpLogComparison> CompareLogsAsync(string fileA, string fileB)
    {
        var runA = await _parser.ParseLogFileAsync(fileA);
        var runB = await _parser.ParseLogFileAsync(fileB);
        return _comparer.Compare(runA, runB);
    }
}
```

## Testing

Test files are located in `/testdata/`:
- `mrp_log_sample_A.txt` - Example baseline run
- `mrp_log_sample_B.txt` - Example comparison run

The comparison engine has been validated against these test scenarios:
- Job disappearance detection (Job 14567)
- Part reappearance detection (Part ABC123)
- Date shift detection with appropriate severity
- Quantity change detection with percentage-based severity
- Error appearance and resolution tracking

## Notes

- Job numbers are compared case-insensitively
- Part numbers are compared case-insensitively
- Error identification uses normalized string comparison for reliability
- First entry with a date is used for each job to avoid duplicate reporting
- Original entries are preserved in `MrpDifference` for evidence trails
