using FluentAssertions;
using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using MRP.Assistant.Reporting;
using MRP.Assistant.Core;
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
        
        // Assert - Run types detected correctly
        runA.RunType.Should().Be(MrpRunType.Regeneration, "Sample A filename contains 'regen'");
        runB.RunType.Should().Be(MrpRunType.NetChange, "Sample B should be detected as Net Change");
        
        // Assert - Both documents parsed
        runA.Entries.Should().NotBeEmpty("Run A should have entries");
        runB.Entries.Should().NotBeEmpty("Run B should have entries");
        
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
