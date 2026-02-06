---
name: Create Unit Tests for Core Components
about: Implement comprehensive unit tests for parser, comparer, and explanation engine
title: "[Agent Task] Create Unit Tests for Core Components"
labels: [testing, agent]
assignees: [copilot]
---

## 🧠 Task Intent
Implement comprehensive unit tests for the core components (parser, comparer, explanation engine) to ensure reliability and correctness. Tests should be independent, fast, and provide good coverage of edge cases.

## 🔍 Scope / Input
**Dependencies**: Issues #2, #3, #4 must be complete (core components exist)

**Testing Framework**: xUnit  
**Assertions**: FluentAssertions (for readable assertions)

**Target Coverage**: Minimum 80% on core classes

## ✅ Expected Output

### 1. Update Test Project

**MRP.Assistant.Tests.csproj** - Add packages:
```xml
<ItemGroup>
  <PackageReference Include="FluentAssertions" Version="6.12.2" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
</ItemGroup>
```

### 2. Test Structure

```
MRP.Assistant.Tests/
├── Parsers/
│   ├── MrpLogParserTests.cs
│   ├── TimestampExtractionTests.cs
│   ├── PartNumberExtractionTests.cs
│   └── DemandSupplyParsingTests.cs
├── Analysis/
│   ├── MrpLogComparerTests.cs
│   ├── ExplanationEngineTests.cs
│   └── DifferenceDetectionTests.cs
├── Fixtures/
│   ├── TestLogData.cs
│   └── SampleLogBuilder.cs
└── TestHelpers/
    └── AssertionExtensions.cs
```

### 3. Parser Tests

**Parsers/MrpLogParserTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.Core;
using MRP.Assistant.Parsers;
using Xunit;

namespace MRP.Assistant.Tests.Parsers;

public class MrpLogParserTests
{
    private readonly MrpLogParser _parser;
    
    public MrpLogParserTests()
    {
        _parser = new MrpLogParser();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void Parse_ValidLogFile_ReturnsPopulatedDocument()
    {
        // Arrange
        var testFile = Path.Combine("testdata", "mrp_log_sample_A.txt");
        
        // Act
        var result = _parser.Parse(testFile);
        
        // Assert
        result.Should().NotBeNull();
        result.SourceFile.Should().Be(testFile);
        result.Entries.Should().NotBeEmpty();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void Parse_FileWithErrors_ExtractsErrorEntries()
    {
        // Arrange
        var logContent = @"
01:00:00 Starting MRP
ERROR: Job 14567 abandoned due to timeout
01:05:00 Processing continues
";
        var testFile = CreateTempLogFile(logContent);
        
        // Act
        var result = _parser.Parse(testFile);
        
        // Assert
        result.Entries.Should().Contain(e => e.EntryType == MrpLogEntryType.Error);
        var errorEntry = result.Entries.First(e => e.EntryType == MrpLogEntryType.Error);
        errorEntry.JobNumber.Should().Be("14567");
        errorEntry.ErrorMessage.Should().Contain("abandoned due to timeout");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void Parse_NonExistentFile_ThrowsException()
    {
        // Arrange
        var invalidFile = "non_existent.log";
        
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _parser.Parse(invalidFile));
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void Parse_EmptyFile_ReturnsDocumentWithNoEntries()
    {
        // Arrange
        var testFile = CreateTempLogFile("");
        
        // Act
        var result = _parser.Parse(testFile);
        
        // Assert
        result.Should().NotBeNull();
        result.Entries.Should().BeEmpty();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void Parse_LogWithMultipleParts_ExtractsAllPartNumbers()
    {
        // Arrange
        var logContent = @"
01:00:00 Processing Part:ABC123, Attribute Set:''
01:01:00 Processing Part:XYZ789, Attribute Set:''
01:02:00 Processing Part:DEF456, Attribute Set:''
";
        var testFile = CreateTempLogFile(logContent);
        
        // Act
        var result = _parser.Parse(testFile);
        
        // Assert
        var partNumbers = result.Entries
            .Where(e => !string.IsNullOrEmpty(e.PartNumber))
            .Select(e => e.PartNumber)
            .ToList();
            
        partNumbers.Should().Contain("ABC123");
        partNumbers.Should().Contain("XYZ789");
        partNumbers.Should().Contain("DEF456");
    }
    
    private string CreateTempLogFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }
}
```

**Parsers/TimestampExtractionTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.Parsers;
using Xunit;

namespace MRP.Assistant.Tests.Parsers;

public class TimestampExtractionTests
{
    [Theory]
    [Trait("Category", "Parser")]
    [InlineData("01:00:46 Building Pegging Demand Master...", "01:00:46")]
    [InlineData("12:34:56 Processing Part:ABC123", "12:34:56")]
    [InlineData("00:00:00 Start", "00:00:00")]
    [InlineData("23:59:59 End", "23:59:59")]
    public void ParseLine_WithValidTimestamp_ExtractsTimestamp(string line, string expectedTime)
    {
        // Arrange
        var parser = new MrpLogParser();
        
        // Act
        var result = parser.ParseLine(line, 1);
        
        // Assert
        result.Timestamp.Should().NotBeNull();
        result.Timestamp.Value.ToString("HH:mm:ss").Should().Be(expectedTime);
    }
    
    [Theory]
    [Trait("Category", "Parser")]
    [InlineData("No timestamp here")]
    [InlineData("25:00:00 Invalid hour")]
    [InlineData("12:60:00 Invalid minute")]
    public void ParseLine_WithInvalidTimestamp_ReturnsNullTimestamp(string line)
    {
        // Arrange
        var parser = new MrpLogParser();
        
        // Act
        var result = parser.ParseLine(line, 1);
        
        // Assert
        result.Timestamp.Should().BeNull();
    }
}
```

**Parsers/DemandSupplyParsingTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.Parsers;
using Xunit;

namespace MRP.Assistant.Tests.Parsers;

public class DemandSupplyParsingTests
{
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLine_WithDemandEntry_ExtractsDemandInfo()
    {
        // Arrange
        var parser = new MrpLogParser();
        var line = "01:05:07 Demand: S: 100516/1/1 Date: 6/15/2026 Quantity: 4.00000000";
        
        // Act
        var result = parser.ParseLine(line, 1);
        
        // Assert
        result.EntryType.Should().Be(MrpLogEntryType.Demand);
        result.Demand.Should().NotBeNull();
        result.Demand!.Type.Should().Be("S");
        result.Demand.Order.Should().Be("100516");
        result.Demand.Line.Should().Be(1);
        result.Demand.Release.Should().Be(1);
        result.Demand.Quantity.Should().Be(4.0m);
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLine_WithSupplyEntry_ExtractsSupplyInfo()
    {
        // Arrange
        var parser = new MrpLogParser();
        var line = "01:05:07 Supply: J: U0000000000273/0/0 Date: 6/15/2026 Quantity: 4.00000000";
        
        // Act
        var result = parser.ParseLine(line, 1);
        
        // Assert
        result.EntryType.Should().Be(MrpLogEntryType.Supply);
        result.Supply.Should().NotBeNull();
        result.Supply!.Type.Should().Be("J");
        result.Supply.JobNumber.Should().Be("U0000000000273");
        result.Supply.Assembly.Should().Be(0);
        result.Supply.Material.Should().Be(0);
        result.Supply.Quantity.Should().Be(4.0m);
    }
}
```

### 4. Comparer Tests

**Analysis/MrpLogComparerTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.Analysis;
using MRP.Assistant.Core;
using Xunit;

namespace MRP.Assistant.Tests.Analysis;

public class MrpLogComparerTests
{
    private readonly MrpLogComparer _comparer;
    
    public MrpLogComparerTests()
    {
        _comparer = new MrpLogComparer();
    }
    
    [Fact]
    [Trait("Category", "Comparer")]
    public void Compare_IdenticalLogs_ReturnsNoDifferences()
    {
        // Arrange
        var docA = CreateSampleDocument("A", new[] { "Job1", "Job2" });
        var docB = CreateSampleDocument("B", new[] { "Job1", "Job2" });
        
        // Act
        var result = _comparer.Compare(docA, docB);
        
        // Assert
        result.Differences.Should().BeEmpty();
        result.Summary.TotalDifferences.Should().Be(0);
    }
    
    [Fact]
    [Trait("Category", "Comparer")]
    public void Compare_JobInAButNotB_DetectsJobRemoval()
    {
        // Arrange
        var docA = CreateSampleDocument("A", new[] { "Job1", "Job2", "Job3" });
        var docB = CreateSampleDocument("B", new[] { "Job1", "Job2" });
        
        // Act
        var result = _comparer.Compare(docA, docB);
        
        // Assert
        result.Differences.Should().ContainSingle(d => 
            d.Type == DifferenceType.JobRemoved && 
            d.JobNumber == "Job3");
    }
    
    [Fact]
    [Trait("Category", "Comparer")]
    public void Compare_JobInBButNotA_DetectsJobAddition()
    {
        // Arrange
        var docA = CreateSampleDocument("A", new[] { "Job1", "Job2" });
        var docB = CreateSampleDocument("B", new[] { "Job1", "Job2", "Job4" });
        
        // Act
        var result = _comparer.Compare(docA, docB);
        
        // Assert
        result.Differences.Should().ContainSingle(d => 
            d.Type == DifferenceType.JobAdded && 
            d.JobNumber == "Job4");
    }
    
    [Fact]
    [Trait("Category", "Comparer")]
    public void Compare_WithMultipleChanges_GeneratesCorrectSummary()
    {
        // Arrange
        var docA = CreateSampleDocument("A", new[] { "Job1", "Job2" });
        var docB = CreateSampleDocument("B", new[] { "Job2", "Job3" });
        
        // Act
        var result = _comparer.Compare(docA, docB);
        
        // Assert
        result.Summary.JobsRemoved.Should().Be(1); // Job1
        result.Summary.JobsAdded.Should().Be(1); // Job3
        result.Summary.TotalDifferences.Should().Be(2);
    }
    
    private MrpLogDocument CreateSampleDocument(string source, string[] jobNumbers)
    {
        var doc = new MrpLogDocument
        {
            SourceFile = $"{source}.txt",
            RunType = MrpRunType.Regeneration
        };
        
        foreach (var job in jobNumbers)
        {
            doc.Entries.Add(new MrpLogEntry
            {
                JobNumber = job,
                EntryType = MrpLogEntryType.Supply
            });
        }
        
        return doc;
    }
}
```

### 5. Explanation Engine Tests

**Analysis/ExplanationEngineTests.cs**:
```csharp
using FluentAssertions;
using MRP.Assistant.Analysis;
using MRP.Assistant.Core;
using Xunit;

namespace MRP.Assistant.Tests.Analysis;

public class ExplanationEngineTests
{
    private readonly ExplanationEngine _engine;
    
    public ExplanationEngineTests()
    {
        _engine = new ExplanationEngine();
    }
    
    [Fact]
    [Trait("Category", "Explanation")]
    public void GenerateExplanations_JobRemoved_ProvidesFACTAndINFERENCE()
    {
        // Arrange
        var diff = new MrpDifference
        {
            Type = DifferenceType.JobRemoved,
            JobNumber = "14567",
            PartNumber = "ABC123",
            RunAEntry = new MrpLogEntry
            {
                JobNumber = "14567",
                ErrorMessage = "Job 14567 abandoned due to timeout",
                EntryType = MrpLogEntryType.Error,
                LineNumber = 42
            }
        };
        
        var comparison = CreateComparisonWithDifference(diff);
        
        // Act
        var explanations = _engine.GenerateExplanations(comparison);
        
        // Assert
        explanations.Should().ContainSingle();
        var explanation = explanations[0];
        
        explanation.Facts.Should().NotBeEmpty("FACTS must be provided");
        explanation.Inferences.Should().NotBeEmpty("INFERENCES must be provided");
        explanation.NextStepsInEpicor.Should().NotBeEmpty("Next steps must be provided");
    }
    
    [Fact]
    [Trait("Category", "Explanation")]
    public void GenerateExplanations_WithTimeoutError_AssignsHighConfidenceInference()
    {
        // Arrange
        var diff = new MrpDifference
        {
            Type = DifferenceType.JobRemoved,
            JobNumber = "14567",
            RunAEntry = new MrpLogEntry
            {
                ErrorMessage = "timeout",
                EntryType = MrpLogEntryType.Error
            }
        };
        
        var comparison = CreateComparisonWithDifference(diff);
        
        // Act
        var explanations = _engine.GenerateExplanations(comparison);
        
        // Assert
        var timeoutInference = explanations[0].Inferences
            .FirstOrDefault(i => i.Statement.Contains("timeout", StringComparison.OrdinalIgnoreCase));
            
        timeoutInference.Should().NotBeNull();
        timeoutInference!.ConfidenceLevel.Should().BeGreaterThan(0.7);
    }
    
    private MrpLogComparison CreateComparisonWithDifference(MrpDifference diff)
    {
        return new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument(),
            Differences = new List<MrpDifference> { diff }
        };
    }
}
```

## 🧪 Acceptance Criteria
- [ ] FluentAssertions package added
- [ ] Minimum 30+ parser tests
- [ ] Minimum 25+ comparer tests
- [ ] Minimum 20+ explanation engine tests
- [ ] All tests pass: `dotnet test`
- [ ] Tests categorized with `[Trait("Category", "...")]`
- [ ] Tests run in <30 seconds total
- [ ] Tests use fixtures from testdata folder
- [ ] Tests are independent (no shared state)
- [ ] Edge cases covered (empty files, malformed lines, etc.)
- [ ] Minimum 80% code coverage on core classes

## 🧪 Validation Commands
```bash
# Run all tests
dotnet test MRP.Assistant.Tests/MRP.Assistant.Tests.csproj

# Run parser tests only
dotnet test --filter Category=Parser

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Check code coverage (if tool available)
dotnet test --collect:"XPlat Code Coverage"
```

## 📝 Notes
- Use FluentAssertions for readable test assertions
- Create helper methods to build test data
- Test both happy path and edge cases
- Use descriptive test names (Method_Scenario_ExpectedResult)
- Keep tests fast and focused
- Next issue will add integration tests
