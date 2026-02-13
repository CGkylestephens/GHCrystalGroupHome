# Epicor MRP Log Investigation Assistant - Project Plan

## 🎯 Project Overview

The Epicor MRP Log Investigation Assistant is a .NET class library and CLI tool designed to help planners and IT teams understand changes in Epicor Kinetic MRP results by comparing log files between MRP runs.

### Key Capabilities
- Extract clean signals from noisy MRP log files
- Anchor investigation around Part → Job → Demand → Supply chain
- Detect surprises: job disappears, date shifts, revision changes, errors
- Compare Run A vs Run B (Regen vs Net Change)
- Distinguish between FACT (log evidence) and INFERENCE (plausible explanation)
- Generate planner-friendly, structured reports

### Technical Stack
- .NET 9 / C# class library
- Console application (CLI entry point)
- Optional: Blazor backend for future web UI
- Testing: xUnit or NUnit
- Build: MSBuild / dotnet CLI

---

## 📋 Task Breakdown (Agent-Executable Issues)

The project is broken into **8 sequential issues** that follow the WRAP pattern (Well-written, Atomic, Paired with agent).

### Execution Order and Dependencies

```
Issue 1 (Bootstrap)
    ↓
Issue 2 (Parser) ← Foundation for all other components
    ↓
    ├─→ Issue 3 (Log Comparison)
    │       ↓
    ├─→ Issue 4 (Explanation Engine)
    │       ↓
    └─→ Issue 5 (Report Generator) ← Depends on 2, 3, 4
            ↓
        Issue 6 (CLI Interface) ← Depends on all core components
            ↓
        Issue 7 (Unit Tests) ← Tests parser, diff, explanation
            ↓
        Issue 8 (Integration Tests) ← End-to-end validation
```

---

## 🏗️ Detailed Issue Specifications

### Issue 1: Bootstrap MRP.Assistant Class Library Project
**Label**: `infra`, `agent`  
**Estimated Effort**: 30 minutes  
**Dependencies**: None

**Scope**:
- Create new .NET 9 class library project: `MRP.Assistant`
- Add to existing solution (`CrystalGroupHome.sln`)
- Create console application project: `MRP.Assistant.CLI`
- Set up basic project structure with folders:
  - `/Core` - core domain models
  - `/Parsers` - log parsing logic
  - `/Analysis` - comparison and explanation
  - `/Reporting` - output generation
- Add standard .gitignore entries for MRP projects
- Create initial README.md for the MRP.Assistant project

**Acceptance Criteria**:
- Solution builds successfully with `dotnet build`
- Projects reference .NET 9
- Folder structure matches specification
- README includes project purpose and basic structure

---

### Issue 2: Implement MRP Log Parser
**Label**: `parser`, `agent`  
**Estimated Effort**: 2-3 hours  
**Dependencies**: Issue 1

**Scope**:
Implement a robust parser that extracts structured data from unstructured MRP log files.

**Input Files**:
- `testdata/mrp_log_sample_A.txt`
- `testdata/mrp_log_sample_B.txt`
- `testdata/MRPRegenSample.txt`
- `testdata/MRPNETChangeSample.txt`

**Core Classes to Create**:
```csharp
// Core/MrpLogEntry.cs
public class MrpLogEntry
{
    public DateTime Timestamp { get; set; }
    public string RawLine { get; set; }
    public MrpLogEntryType EntryType { get; set; }
    public string? PartNumber { get; set; }
    public string? JobNumber { get; set; }
    public string? DemandInfo { get; set; }
    public string? SupplyInfo { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}

// Core/MrpLogDocument.cs
public class MrpLogDocument
{
    public string SourceFile { get; set; }
    public MrpRunType RunType { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Site { get; set; }
    public List<MrpLogEntry> Entries { get; set; }
    public List<string> Errors { get; set; }
}

// Parsers/MrpLogParser.cs
public class MrpLogParser
{
    public MrpLogDocument Parse(string filePath);
    private MrpLogEntry ParseLine(string line, int lineNumber);
    private MrpLogEntryType DetectEntryType(string line);
    private string? ExtractPartNumber(string line);
    private string? ExtractJobNumber(string line);
    private DateTime? ExtractTimestamp(string line);
}
```

**Parsing Rules** (from copilot-instructions.md):
- Extract timestamps (format: `HH:mm:ss` at line start)
- Detect keywords: `Part:`, `Job`, `Demand:`, `Supply:`, `Pegged`
- Flag error indicators: `ERROR`, `cannot`, `error`, `exception`, `defunct`, `abandoned`, `timeout`, `cancel`
- Parse demand format: `S: {order}/{line}/{rel} Date: {date} Quantity: {qty}`
- Parse supply format: `J: {job}/{asm}/{mtl} Date: {date} Quantity: {qty}`
- Extract run metadata: Start/End time, Site, Run type

**Acceptance Criteria**:
- Parser successfully reads all 4 sample files
- Extracts at least 90% of Part, Job, Demand, Supply references
- Identifies all error/warning lines
- Returns structured `MrpLogDocument` object
- Handles malformed lines gracefully (logs warnings, continues parsing)

**Test Command**:
```bash
dotnet test MRP.Assistant.Tests --filter Category=Parser
```

---

### Issue 3: Build Log Comparison Engine
**Label**: `diff`, `agent`  
**Estimated Effort**: 2-3 hours  
**Dependencies**: Issue 2

**Scope**:
Implement a comparison engine that detects differences between two MRP log runs.

**Core Classes to Create**:
```csharp
// Analysis/MrpLogComparison.cs
public class MrpLogComparison
{
    public MrpLogDocument RunA { get; set; }
    public MrpLogDocument RunB { get; set; }
    public List<MrpDifference> Differences { get; set; }
    public ComparisonSummary Summary { get; set; }
}

// Analysis/MrpDifference.cs
public class MrpDifference
{
    public DifferenceType Type { get; set; } // JobAdded, JobRemoved, DateShift, QtyChange, ErrorAppeared
    public string PartNumber { get; set; }
    public string? JobNumber { get; set; }
    public string Description { get; set; }
    public MrpLogEntry? RunAEntry { get; set; }
    public MrpLogEntry? RunBEntry { get; set; }
    public DifferenceSeverity Severity { get; set; } // Critical, Warning, Info
}

// Analysis/MrpLogComparer.cs
public class MrpLogComparer
{
    public MrpLogComparison Compare(MrpLogDocument runA, MrpLogDocument runB);
    private List<MrpDifference> DetectJobDifferences();
    private List<MrpDifference> DetectDateShifts();
    private List<MrpDifference> DetectQuantityChanges();
    private List<MrpDifference> DetectNewErrors();
}
```

**Detection Logic**:
1. **Job Differences**:
   - Jobs present in A but not B (disappeared)
   - Jobs present in B but not A (appeared)
   
2. **Date/Quantity Shifts**:
   - Same job, different due date (>1 day difference)
   - Same job, different quantity (>5% variance)

3. **Error Differences**:
   - Errors in B not present in A
   - Resolved errors (in A but not B)

4. **Part-Level Changes**:
   - Parts with processing changes
   - Parts that stopped/started being processed

**Acceptance Criteria**:
- Compares two `MrpLogDocument` objects
- Detects all 5 difference types
- Assigns appropriate severity levels
- Returns structured comparison with summary stats
- Performance: Compares logs with 10,000+ entries in <5 seconds

**Test Scenarios**:
- Compare `mrp_log_sample_A.txt` vs `mrp_log_sample_B.txt`
- Should detect: job disappearance (14567), part reappearance (ABC123)

---

### Issue 4: Create Explanation Engine (FACT vs INFERENCE)
**Label**: `analysis`, `agent`  
**Estimated Effort**: 2-3 hours  
**Dependencies**: Issue 2, Issue 3

**Scope**:
Implement logic that explains *why* differences occurred, distinguishing between FACT (log evidence) and INFERENCE (plausible cause).

**Core Classes to Create**:
```csharp
// Analysis/ExplanationEngine.cs
public class ExplanationEngine
{
    public List<Explanation> GenerateExplanations(MrpLogComparison comparison);
    private Explanation ExplainJobDisappearance(MrpDifference diff);
    private Explanation ExplainDateShift(MrpDifference diff);
    private Explanation ExplainNewError(MrpDifference diff);
}

// Analysis/Explanation.cs
public class Explanation
{
    public MrpDifference RelatedDifference { get; set; }
    public string Summary { get; set; } // One-line what happened
    public List<ExplanationFact> Facts { get; set; } // LOG-SUPPORTED evidence
    public List<ExplanationInference> Inferences { get; set; } // Plausible causes
    public List<string> NextStepsInEpicor { get; set; } // Screens to check
}

// Analysis/ExplanationFact.cs
public class ExplanationFact
{
    public string Statement { get; set; }
    public string LogEvidence { get; set; } // Line from log
    public int LineNumber { get; set; }
}

// Analysis/ExplanationInference.cs
public class ExplanationInference
{
    public string Statement { get; set; }
    public double ConfidenceLevel { get; set; } // 0.0 - 1.0
    public List<string> SupportingReasons { get; set; }
}
```

**Explanation Rules**:
1. **Job Disappeared**:
   - FACT: "Job 14567 present in Run A log, absent in Run B log"
   - FACT: "Run A logged 'ERROR: Job 14567 abandoned due to timeout' at 02:45 UTC"
   - INFERENCE (High): "Job may have been manually deleted after timeout error"
   - INFERENCE (Medium): "Timeout may have caused automatic cleanup in Net Change run"
   - Next Steps: Check Job Tracker, Job Entry screen

2. **Date Shift**:
   - FACT: "Job X due date was 2/15 in Run A, 2/20 in Run B"
   - FACT: "Part Y demand quantity increased from 100 to 150"
   - INFERENCE: "Date shift likely due to capacity constraints from increased demand"
   - Next Steps: Check Resource Scheduling, Load Leveling

3. **New Error**:
   - FACT: "Run B logged 'ERROR: Part ABC timeout' at 03:15"
   - FACT: "Part ABC processed successfully in Run A"
   - INFERENCE: "Network or database latency may have caused timeout"
   - Next Steps: Check System Monitor, Database logs

**Acceptance Criteria**:
- Generates explanations for all difference types
- Clearly separates FACT from INFERENCE
- Provides actionable "Next Steps"
- Assigns confidence levels to inferences
- Language is planner-friendly (no technical jargon)

---

### Issue 5: Implement Planner-Friendly Report Generator
**Label**: `reporter`, `agent`  
**Estimated Effort**: 2 hours  
**Dependencies**: Issue 2, Issue 3, Issue 4

**Scope**:
Create a report generator that outputs structured, readable reports following the 5-section format from copilot-instructions.md.

**Core Classes to Create**:
```csharp
// Reporting/MrpReportGenerator.cs
public class MrpReportGenerator
{
    public string GenerateReport(MrpLogComparison comparison, List<Explanation> explanations, ReportFormat format);
}

// Reporting/ReportFormat.cs
public enum ReportFormat
{
    Markdown,
    PlainText,
    Html,
    Json
}
```

**Report Structure** (5 sections):
```
A) RUN SUMMARY
   - Run A: [Type, Time, Site, Duration, Parts Processed, Jobs Created, Errors]
   - Run B: [Type, Time, Site, Duration, Parts Processed, Jobs Created, Errors]

B) WHAT CHANGED
   - [Bullet list of top 10 most significant differences]
   - Critical: [count] | Warnings: [count] | Info: [count]

C) MOST LIKELY WHY
   - [For each top difference, provide explanation with FACT/INFERENCE labels]
   
D) LOG EVIDENCE
   - [Relevant log excerpts with line numbers]
   
E) NEXT CHECKS IN EPICOR
   - [Recommended screens/reports to investigate further]
   - Grouped by priority (Must Check / Should Check / Optional)
```

**Acceptance Criteria**:
- Generates reports in Markdown and PlainText formats
- Follows 5-section structure exactly
- Limits "What Changed" to top 10 by severity
- FACT/INFERENCE clearly labeled in section C
- Log excerpts in section D include line numbers and context
- Report is readable by non-technical planners
- Includes incomplete log warnings if applicable

**Sample Output Location**:
- Generate sample report: `testdata/sample_comparison_report.md`

---

### Issue 6: Add CLI Interface for File-Based Comparison
**Label**: `cli`, `agent`  
**Estimated Effort**: 1-2 hours  
**Dependencies**: Issue 2, Issue 3, Issue 4, Issue 5

**Scope**:
Create a command-line interface that ties all components together.

**CLI Commands**:
```bash
# Compare two log files
mrp-assistant compare --run-a testdata/mrp_log_sample_A.txt --run-b testdata/mrp_log_sample_B.txt --output report.md

# Parse a single log file
mrp-assistant parse --file testdata/MRPRegenSample.txt --format json

# Generate sample report from test data
mrp-assistant demo
```

**Main Program Structure**:
```csharp
// MRP.Assistant.CLI/Program.cs
public class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandLineApplication();
        app.Name = "mrp-assistant";
        app.Description = "Epicor MRP Log Investigation Assistant";
        
        // Compare command
        app.Command("compare", cmd => { /* ... */ });
        
        // Parse command
        app.Command("parse", cmd => { /* ... */ });
        
        // Demo command
        app.Command("demo", cmd => { /* ... */ });
        
        return app.Execute(args);
    }
}
```

**Acceptance Criteria**:
- All 3 commands work correctly
- Help text is clear and planner-friendly
- Error messages are helpful
- `demo` command generates sample report from included test data
- CLI returns appropriate exit codes (0 = success, 1 = error)
- Validates input files exist before processing

**Package Reference**:
- Add `McMaster.Extensions.CommandLineUtils` for CLI parsing

---

### Issue 7: Create Unit Tests for Core Components
**Label**: `testing`, `agent`  
**Estimated Effort**: 2-3 hours  
**Dependencies**: Issue 2, Issue 3, Issue 4

**Scope**:
Implement comprehensive unit tests for parser, comparer, and explanation engine.

**Test Project Structure**:
```
MRP.Assistant.Tests/
├── Parsers/
│   ├── MrpLogParserTests.cs
│   └── TimestampExtractionTests.cs
├── Analysis/
│   ├── MrpLogComparerTests.cs
│   ├── ExplanationEngineTests.cs
│   └── DifferenceDetectionTests.cs
└── TestData/
    └── test_fixtures.txt
```

**Test Coverage Areas**:

1. **Parser Tests** (30+ tests):
   - Parse valid log files
   - Extract Part numbers (various formats)
   - Extract Job numbers (U-prefix, F-prefix, numeric)
   - Parse demand/supply entries
   - Handle timestamps (HH:mm:ss format)
   - Detect error keywords
   - Handle malformed lines gracefully
   - Parse empty/minimal files

2. **Comparer Tests** (25+ tests):
   - Detect job disappearance
   - Detect job appearance
   - Detect date shifts (>1 day)
   - Detect quantity changes (>5%)
   - Detect new errors
   - Handle identical logs (no differences)
   - Handle completely different logs

3. **Explanation Engine Tests** (20+ tests):
   - Generate job disappearance explanation
   - Generate date shift explanation
   - Generate error explanation
   - Separate FACT from INFERENCE
   - Assign confidence levels
   - Generate next steps

**Acceptance Criteria**:
- Minimum 80% code coverage on core classes
- All tests pass with `dotnet test`
- Tests run in <30 seconds
- Use test fixtures from `testdata/` folder
- Tests are independent (no shared state)

**Testing Framework**:
- xUnit or NUnit
- Add `FluentAssertions` for readable assertions
- Add `Moq` if mocking is needed

---

### Issue 8: Add Integration Tests with Sample Logs
**Label**: `testing`, `agent`  
**Estimated Effort**: 1-2 hours  
**Dependencies**: Issue 6, Issue 7

**Scope**:
Create end-to-end integration tests that validate the full pipeline using real sample logs.

**Test Scenarios**:

1. **Full Comparison Flow**:
   ```csharp
   [Fact]
   public void CompareRealMrpLogs_SampleA_vs_SampleB_GeneratesReport()
   {
       // Arrange
       var fileA = "testdata/mrp_log_sample_A.txt";
       var fileB = "testdata/mrp_log_sample_B.txt";
       
       // Act
       var result = FullPipeline.Compare(fileA, fileB);
       
       // Assert
       Assert.True(result.Success);
       Assert.Contains("Job 14567", result.Report);
       Assert.Contains("FACT", result.Report);
       Assert.Contains("INFERENCE", result.Report);
   }
   ```

2. **CLI Integration**:
   ```csharp
   [Fact]
   public void CLI_Compare_Command_Produces_Valid_Report()
   {
       // Arrange
       var args = new[] { "compare", "--run-a", "testdata/mrp_log_sample_A.txt", 
                          "--run-b", "testdata/mrp_log_sample_B.txt", 
                          "--output", "testoutput/report.md" };
       
       // Act
       var exitCode = Program.Main(args);
       
       // Assert
       Assert.Equal(0, exitCode);
       Assert.True(File.Exists("testoutput/report.md"));
   }
   ```

3. **Large Log Performance**:
   ```csharp
   [Fact]
   public void Parser_Handles_Large_Log_In_Reasonable_Time()
   {
       // Test with 10,000+ line log (generate or use real)
       // Should complete in < 5 seconds
   }
   ```

**Acceptance Criteria**:
- 3+ integration tests covering full pipeline
- Tests use actual files from `testdata/`
- CLI integration tests validate exit codes and file outputs
- Performance test validates <5s for large logs
- All integration tests pass with `dotnet test --filter Category=Integration`

---

## 📊 Project Metrics

**Total Estimated Effort**: 12-18 hours
- Infrastructure: 0.5 hours
- Core Development: 8-12 hours
- Testing: 3-5 hours
- Documentation: 0.5-1 hour

**Total Issues**: 8 (all agent-executable)

**Key Milestones**:
1. ✅ Bootstrap Complete (Issue 1)
2. ✅ Parsing Works (Issue 2)
3. ✅ Comparison Works (Issue 3)
4. ✅ Explanation Works (Issue 4)
5. ✅ Reporting Works (Issue 5)
6. ✅ CLI Ready (Issue 6)
7. ✅ Unit Tested (Issue 7)
8. ✅ Integration Tested (Issue 8)

---

## 🎯 Success Criteria

The project is complete when:
1. All 8 issues are closed
2. Solution builds with `dotnet build`
3. All tests pass with `dotnet test`
4. CLI demo command generates valid report
5. README documentation is complete
6. Sample report in `testdata/` demonstrates capability

---

## 📝 Notes for GitHub Copilot Agent

- Each issue should be assigned to `@copilot`
- Use label `agent` for all issues
- Reference this plan in issue descriptions
- Include test data file paths in each issue
- Provide code structure examples
- Specify acceptance criteria clearly
- Issues can be tackled one at a time (sequential dependency chain)

---

## 🔗 Related Resources

- Test Data: `/testdata/mrp_log_sample_*.txt`
- Instructions: `/copilot-instructions.md`
- Sample Extractor: `/testdata/ExtractLogSample.ps1`
- Existing Solution: `CrystalGroupHome.sln`
- Issue Template: `.github/ISSUE_TEMPLATE/agent_task.md`
