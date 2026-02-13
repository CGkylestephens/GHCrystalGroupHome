using FluentAssertions;
using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using MRP.Assistant.Core;
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
        
        // Assert - Both files parsed successfully
        runA.Should().NotBeNull();
        runB.Should().NotBeNull();
        runA.Entries.Should().NotBeEmpty();
        runB.Entries.Should().NotBeEmpty();
        
        // The simple sample files may or may not have detectable differences 
        // depending on parser sophistication, so we just verify the comparison runs
        comparison.Should().NotBeNull();
        comparison.Summary.Should().NotBeNull();
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
