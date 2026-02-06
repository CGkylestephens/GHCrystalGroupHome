---
name: Add Integration Tests with Sample Logs
about: Create end-to-end integration tests validating full pipeline with real logs
title: "[Agent Task] Add Integration Tests with Sample Logs"
labels: [testing, agent]
assignees: [copilot]
---

## 🧠 Task Intent
Create end-to-end integration tests that validate the complete pipeline (parse → compare → explain → report) using actual sample log files. These tests ensure all components work together correctly and meet performance requirements.

## 🔍 Scope / Input
**Dependencies**: Issues #6 and #7 must be complete (CLI and unit tests exist)

**Test Type**: Integration tests (full pipeline validation)

**Test Data**: Use real files from `/testdata/`

## ✅ Expected Output

### 1. Integration Test Project Structure

```
MRP.Assistant.Tests/
├── Integration/
│   ├── FullPipelineTests.cs
│   ├── CLIIntegrationTests.cs
│   ├── PerformanceTests.cs
│   └── RealLogComparisonTests.cs
└── TestOutput/
    └── .gitignore (ignore generated reports)
```

### 2. Full Pipeline Tests

**Integration/FullPipelineTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using MRP.Assistant.Reporting;
using Xunit;

namespace MRP.Assistant.Tests.Integration;

[Trait("Category", "Integration")]
public class FullPipelineTests
{
    private readonly string _testDataPath;
    
    public FullPipelineTests()
    {
        // Find testdata directory relative to test assembly
        var currentDir = Directory.GetCurrentDirectory();
        _testDataPath = FindTestDataDirectory(currentDir);
    }
    
    [Fact]
    public void CompareRealMrpLogs_SampleA_vs_SampleB_GeneratesValidReport()
    {
        // Arrange
        var fileA = Path.Combine(_testDataPath, "mrp_log_sample_A.txt");
        var fileB = Path.Combine(_testDataPath, "mrp_log_sample_B.txt");
        var outputFile = Path.Combine("TestOutput", "integration_report_AB.md");
        
        Directory.CreateDirectory("TestOutput");
        
        // Act - Full Pipeline
        var parser = new MrpLogParser();
        var runA = parser.Parse(fileA);
        var runB = parser.Parse(fileB);
        
        var comparer = new MrpLogComparer();
        var comparison = comparer.Compare(runA, runB);
        
        var explainer = new ExplanationEngine();
        var explanations = explainer.GenerateExplanations(comparison);
        
        var generator = new MrpReportGenerator();
        var report = generator.GenerateReport(comparison, explanations, new ReportOptions());
        
        File.WriteAllText(outputFile, report);
        
        // Assert - Report Content
        report.Should().NotBeNullOrEmpty();
        report.Should().Contain("# MRP Log Comparison Report");
        report.Should().Contain("## A) RUN SUMMARY");
        report.Should().Contain("## B) WHAT CHANGED");
        report.Should().Contain("## C) MOST LIKELY WHY");
        report.Should().Contain("## D) LOG EVIDENCE");
        report.Should().Contain("## E) NEXT CHECKS IN EPICOR");
        
        // Assert - Specific Content
        report.Should().Contain("Job 14567", "Sample A has job 14567 with timeout error");
        report.Should().Contain("FACT", "Report must distinguish facts");
        report.Should().Contain("INFERENCE", "Report must include inferences");
        
        // Assert - File Output
        File.Exists(outputFile).Should().BeTrue();
    }
    
    [Fact]
    public void CompareMRPRegenSample_vs_MRPNETChangeSample_DetectsDifferences()
    {
        // Arrange
        var fileA = Path.Combine(_testDataPath, "MRPRegenSample.txt");
        var fileB = Path.Combine(_testDataPath, "MRPNETChangeSample.txt");
        
        // Act
        var parser = new MrpLogParser();
        var runA = parser.Parse(fileA);
        var runB = parser.Parse(fileB);
        
        var comparer = new MrpLogComparer();
        var comparison = comparer.Compare(runA, runB);
        
        // Assert
        runA.RunType.Should().Be(MrpRunType.Regeneration, "File should be detected as Regen");
        runB.RunType.Should().Be(MrpRunType.NetChange, "File should be detected as Net Change");
        
        comparison.Differences.Should().NotBeEmpty("Regen vs Net Change should have differences");
        comparison.Summary.TotalDifferences.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void ParseAllSampleLogs_NoExceptions()
    {
        // Arrange
        var sampleFiles = new[]
        {
            "mrp_log_sample_A.txt",
            "mrp_log_sample_B.txt",
            "MRPRegenSample.txt",
            "MRPNETChangeSample.txt"
        };
        
        var parser = new MrpLogParser();
        
        // Act & Assert
        foreach (var file in sampleFiles)
        {
            var fullPath = Path.Combine(_testDataPath, file);
            if (File.Exists(fullPath))
            {
                var result = parser.Parse(fullPath);
                result.Should().NotBeNull($"Parsing {file} should succeed");
                result.Entries.Should().NotBeEmpty($"{file} should have entries");
            }
        }
    }
    
    private string FindTestDataDirectory(string startPath)
    {
        // Search up directory tree for testdata folder
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            var testDataPath = Path.Combine(current.FullName, "testdata");
            if (Directory.Exists(testDataPath))
                return testDataPath;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("testdata directory not found");
    }
}
```

### 3. CLI Integration Tests

**Integration/CLIIntegrationTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.CLI.Commands;
using Xunit;

namespace MRP.Assistant.Tests.Integration;

[Trait("Category", "Integration")]
public class CLIIntegrationTests
{
    private readonly string _testDataPath;
    
    public CLIIntegrationTests()
    {
        var currentDir = Directory.GetCurrentDirectory();
        _testDataPath = FindTestDataDirectory(currentDir);
    }
    
    [Fact]
    public void CLI_CompareCommand_ProducesValidReport()
    {
        // Arrange
        var outputFile = Path.Combine("TestOutput", "cli_test_report.md");
        Directory.CreateDirectory("TestOutput");
        
        var command = new CompareCommand
        {
            RunAFile = Path.Combine(_testDataPath, "mrp_log_sample_A.txt"),
            RunBFile = Path.Combine(_testDataPath, "mrp_log_sample_B.txt"),
            OutputFile = outputFile,
            Format = "markdown"
        };
        
        // Act
        var exitCode = command.OnExecute();
        
        // Assert
        exitCode.Should().Be(0, "Command should succeed");
        File.Exists(outputFile).Should().BeTrue("Report file should be created");
        
        var reportContent = File.ReadAllText(outputFile);
        reportContent.Should().Contain("# MRP Log Comparison Report");
    }
    
    [Fact]
    public void CLI_ParseCommand_ShowsStatistics()
    {
        // Arrange
        var command = new ParseCommand
        {
            LogFile = Path.Combine(_testDataPath, "MRPRegenSample.txt"),
            Format = "summary"
        };
        
        // Capture console output
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        
        // Act
        var exitCode = command.OnExecute();
        
        // Restore console
        Console.SetOut(originalOut);
        var output = writer.ToString();
        
        // Assert
        exitCode.Should().Be(0, "Command should succeed");
        output.Should().Contain("MRP Log Summary");
        output.Should().Contain("Total Entries:");
        output.Should().Contain("Parts:");
        output.Should().Contain("Jobs:");
    }
    
    [Fact]
    public void CLI_DemoCommand_GeneratesReport()
    {
        // Arrange
        var outputFile = Path.Combine("TestOutput", "demo_test_report.md");
        var command = new DemoCommand
        {
            OutputFile = outputFile
        };
        
        // Act
        var exitCode = command.OnExecute();
        
        // Assert
        exitCode.Should().Be(0, "Demo command should succeed");
        File.Exists(outputFile).Should().BeTrue("Demo report should be created");
    }
    
    [Fact]
    public void CLI_CompareCommand_WithInvalidFile_ReturnsError()
    {
        // Arrange
        var command = new CompareCommand
        {
            RunAFile = "non_existent_file.txt",
            RunBFile = Path.Combine(_testDataPath, "mrp_log_sample_B.txt"),
            OutputFile = "output.md"
        };
        
        // Act
        var exitCode = command.OnExecute();
        
        // Assert
        exitCode.Should().Be(1, "Command should fail with invalid file");
    }
    
    private string FindTestDataDirectory(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            var testDataPath = Path.Combine(current.FullName, "testdata");
            if (Directory.Exists(testDataPath))
                return testDataPath;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("testdata directory not found");
    }
}
```

### 4. Performance Tests

**Integration/PerformanceTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using System.Diagnostics;
using Xunit;

namespace MRP.Assistant.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Category", "Performance")]
public class PerformanceTests
{
    [Fact]
    public void Parser_HandlesLargeLog_UnderFiveSeconds()
    {
        // Arrange
        var testDataPath = FindTestDataDirectory(Directory.GetCurrentDirectory());
        var largeLogFile = Path.Combine(testDataPath, "AUTOMRPREGEN2026_20260205_2359.log");
        
        // Skip if large log not available
        if (!File.Exists(largeLogFile))
        {
            // Generate synthetic large log for testing
            largeLogFile = GenerateLargeTestLog(10000);
        }
        
        var parser = new MrpLogParser();
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        var result = parser.Parse(largeLogFile);
        stopwatch.Stop();
        
        // Assert
        result.Should().NotBeNull();
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(5, 
            "Parser should handle large logs in <5 seconds");
    }
    
    [Fact]
    public void Comparer_Handles10000EntriesEach_UnderFiveSeconds()
    {
        // Arrange
        var docA = GenerateSyntheticDocument("A", 10000);
        var docB = GenerateSyntheticDocument("B", 10000);
        
        var comparer = new MrpLogComparer();
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        var result = comparer.Compare(docA, docB);
        stopwatch.Stop();
        
        // Assert
        result.Should().NotBeNull();
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(5,
            "Comparer should handle 10k+ entries in <5 seconds");
    }
    
    [Fact]
    public void FullPipeline_WithRealLogs_CompletesInReasonableTime()
    {
        // Arrange
        var testDataPath = FindTestDataDirectory(Directory.GetCurrentDirectory());
        var fileA = Path.Combine(testDataPath, "mrp_log_sample_A.txt");
        var fileB = Path.Combine(testDataPath, "mrp_log_sample_B.txt");
        
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        
        var parser = new MrpLogParser();
        var runA = parser.Parse(fileA);
        var runB = parser.Parse(fileB);
        
        var comparer = new MrpLogComparer();
        var comparison = comparer.Compare(runA, runB);
        
        var explainer = new ExplanationEngine();
        var explanations = explainer.GenerateExplanations(comparison);
        
        var generator = new MrpReportGenerator();
        var report = generator.GenerateReport(comparison, explanations, new ReportOptions());
        
        stopwatch.Stop();
        
        // Assert
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(10,
            "Full pipeline should complete in <10 seconds");
    }
    
    private MrpLogDocument GenerateSyntheticDocument(string source, int entryCount)
    {
        var doc = new MrpLogDocument
        {
            SourceFile = $"{source}_synthetic.txt",
            RunType = MrpRunType.Regeneration,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1)
        };
        
        for (int i = 0; i < entryCount; i++)
        {
            doc.Entries.Add(new MrpLogEntry
            {
                RawLine = $"01:00:00 Processing Part:PART{i:D6}",
                PartNumber = $"PART{i:D6}",
                JobNumber = $"JOB{i:D6}",
                EntryType = MrpLogEntryType.ProcessingPart,
                LineNumber = i + 1
            });
        }
        
        return doc;
    }
    
    private string GenerateLargeTestLog(int lineCount)
    {
        var tempFile = Path.Combine("TestOutput", "large_synthetic_log.txt");
        Directory.CreateDirectory("TestOutput");
        
        using var writer = new StreamWriter(tempFile);
        for (int i = 0; i < lineCount; i++)
        {
            writer.WriteLine($"01:00:00 Processing Part:PART{i:D6}, Attribute Set:''");
            writer.WriteLine($"01:00:01 Demand: S: {i}/1/1 Date: 6/15/2026 Quantity: {i}.00000000");
            writer.WriteLine($"01:00:02 Supply: J: JOB{i:D6}/0/0 Date: 6/15/2026 Quantity: {i}.00000000");
        }
        
        return tempFile;
    }
    
    private string FindTestDataDirectory(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            var testDataPath = Path.Combine(current.FullName, "testdata");
            if (Directory.Exists(testDataPath))
                return testDataPath;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("testdata directory not found");
    }
}
```

### 5. Real Log Comparison Tests

**Integration/RealLogComparisonTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using Xunit;

namespace MRP.Assistant.Tests.Integration;

[Trait("Category", "Integration")]
public class RealLogComparisonTests
{
    private readonly string _testDataPath;
    
    public RealLogComparisonTests()
    {
        _testDataPath = FindTestDataDirectory(Directory.GetCurrentDirectory());
    }
    
    [Fact]
    public void Compare_SampleA_vs_SampleB_DetectsKnownDifferences()
    {
        // Arrange
        var fileA = Path.Combine(_testDataPath, "mrp_log_sample_A.txt");
        var fileB = Path.Combine(_testDataPath, "mrp_log_sample_B.txt");
        
        var parser = new MrpLogParser();
        var comparer = new MrpLogComparer();
        
        // Act
        var runA = parser.Parse(fileA);
        var runB = parser.Parse(fileB);
        var comparison = comparer.Compare(runA, runB);
        
        // Assert - Based on known sample content
        comparison.Differences.Should().NotBeEmpty("Sample files have known differences");
        
        // Job 14567 should be detected as removed (present in A with error, not in B)
        var job14567Diff = comparison.Differences.FirstOrDefault(d => 
            d.JobNumber == "14567" && d.Type == DifferenceType.JobRemoved);
            
        job14567Diff.Should().NotBeNull("Job 14567 removal should be detected");
        
        // Part ABC123 changes should be detected
        var abc123Diffs = comparison.Differences.Where(d => d.PartNumber == "ABC123");
        abc123Diffs.Should().NotBeEmpty("Part ABC123 changes should be detected");
    }
    
    [Theory]
    [InlineData("AUTOMRPREGEN2026_20260205_2359.log")]
    [InlineData("AUTOMRPNET2026.log")]
    public void Parse_RealProductionLog_IfExists_NoExceptions(string filename)
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, filename);
        
        // Skip if file doesn't exist
        if (!File.Exists(filePath))
        {
            return; // Skip test
        }
        
        var parser = new MrpLogParser();
        
        // Act
        var result = parser.Parse(filePath);
        
        // Assert
        result.Should().NotBeNull();
        result.Entries.Should().NotBeEmpty();
        result.ParsingErrors.Count.Should().BeLessThan(result.Entries.Count / 10, 
            "Parsing errors should be < 10% of total entries");
    }
    
    private string FindTestDataDirectory(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            var testDataPath = Path.Combine(current.FullName, "testdata");
            if (Directory.Exists(testDataPath))
                return testDataPath;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("testdata directory not found");
    }
}
```

### 6. Test Output Ignore

**TestOutput/.gitignore**:
```
# Ignore all test-generated files
*
!.gitignore
```

## 🧪 Acceptance Criteria
- [ ] Integration test classes created (4 test files)
- [ ] Full pipeline test validates end-to-end flow
- [ ] CLI integration tests validate all commands
- [ ] Performance test ensures <5s for large logs
- [ ] Real log comparison tests validate known differences
- [ ] All integration tests pass: `dotnet test --filter Category=Integration`
- [ ] TestOutput directory ignored in git
- [ ] Tests find testdata directory automatically
- [ ] Tests skip gracefully if optional files missing
- [ ] Sample report generated in TestOutput folder

## 🧪 Validation Commands
```bash
# Run all integration tests
dotnet test --filter Category=Integration

# Run performance tests
dotnet test --filter Category=Performance

# Run all tests (unit + integration)
dotnet test MRP.Assistant.Tests/MRP.Assistant.Tests.csproj

# Run tests with detailed output
dotnet test --filter Category=Integration --logger "console;verbosity=detailed"

# Check test output
ls -la MRP.Assistant.Tests/TestOutput/
cat MRP.Assistant.Tests/TestOutput/integration_report_AB.md
```

**Expected Results**:
- All integration tests pass
- Reports generated in TestOutput folder
- Performance tests complete within time limits
- Known differences detected in sample files

## 📝 Notes
- Integration tests may take longer than unit tests (that's expected)
- Use real files from testdata folder
- Generate synthetic data for performance tests if needed
- Skip tests gracefully if optional files missing
- Output reports to TestOutput folder (ignored by git)
- Validate both functionality and performance
- This completes the full test suite!

## 🎯 Project Complete!
After this issue is complete:
- All 8 issues will be done
- Full MRP.Assistant implementation ready
- Comprehensive test coverage
- CLI ready for use
- Documentation complete
