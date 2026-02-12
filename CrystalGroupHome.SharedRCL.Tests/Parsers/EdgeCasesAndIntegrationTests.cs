using FluentAssertions;
using CrystalGroupHome.SharedRCL.Services;
using CrystalGroupHome.SharedRCL.Tests.Fixtures;
using Xunit;

namespace CrystalGroupHome.SharedRCL.Tests.Parsers;

public class EdgeCasesAndIntegrationTests
{
    private readonly MrpLogParser _parser;
    
    public EdgeCasesAndIntegrationTests()
    {
        _parser = new MrpLogParser();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_EmptyLines_HandlesGracefully()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "",
            "01:00:00 MRP process begin",
            "",
            "",
            "Site List -> MfgSys",
            "",
            "01:30:00 Process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys");
        result.StartTime.Should().NotBeNull();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_OnlyWhitespace_ReturnsDefaults()
    {
        // Arrange
        var logContent = new[]
        {
            "   ",
            "    ",
            "\t\t",
            ""
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().BeNull();
        result.StartTime.Should().BeNull();
        result.EndTime.Should().BeNull();
        result.RunType.Should().Be("unknown");
        result.Status.Should().Be("uncertain");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_VeryLargeLog_PerformsWell()
    {
        // Arrange - Create a log with many lines
        var lines = new List<string>
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP Regeneration process begin",
            "Site List -> MfgSys"
        };
        
        // Add 10000 processing lines
        for (int i = 0; i < 10000; i++)
        {
            lines.Add($"01:{i % 60:D2}:{i % 60:D2} Processing part PART{i}...");
        }
        
        lines.Add("23:59:59 Process complete");
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = _parser.ParseLogContent(lines);
        stopwatch.Stop();
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "parsing should be reasonably fast");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_MalformedTimestamps_IgnoresInvalid()
    {
        // Arrange
        var logContent = new[]
        {
            "Date: 6/15/2026",
            "25:00:00 Invalid hour",
            "12:60:00 Invalid minute",
            "12:34:60 Invalid second",
            "01:00:46 Valid timestamp"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        result.StartTime?.TimeOfDay.Should().Be(new TimeSpan(1, 0, 46));
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_SpecialCharactersInContent_HandlesCorrectly()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin with special chars: @#$%^&*()",
            "Site: MfgSys",
            "01:05:00 Processing Part:<>&\"|'",
            "01:10:00 Process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys");
        result.StartTime.Should().NotBeNull();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_UnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin",
            "Site: MfgSys",
            "01:05:00 Processing Part:部品123",
            "01:10:00 Process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_MultipleRunsInOneLog_ParsesFirstRun()
    {
        // Arrange - Multiple run starts (unusual but possible)
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP Regeneration process begin",
            "Site List -> MfgSys",
            "01:30:00 Process complete",
            "02:00:00 MRP Regeneration process begin",
            "Site List -> PLANT02",
            "02:30:00 Process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys"); // First site
        result.RunType.Should().Be("regen");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public async Task ParseLogFileAsync_RealTestDataFile_ParsesCorrectly()
    {
        // Arrange
        var testFile = Path.Combine("/home/runner/work/GHCrystalGroupHome/GHCrystalGroupHome", "testdata", "MRPRegenSample.txt");
        
        // Skip if file doesn't exist
        if (!File.Exists(testFile))
        {
            return; // Skip test if sample file not available
        }
        
        // Act
        var result = await _parser.ParseLogFileAsync(testFile);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull("log should contain timestamps");
        result.RunType.Should().Be("regen", "log contains Building Pegging operations");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_AllTestDataSamples_ParseWithoutErrors()
    {
        // Arrange
        var samples = new[]
        {
            TestLogData.SimpleRegenLog,
            TestLogData.SimpleNetChangeLog,
            TestLogData.LogWithErrors,
            TestLogData.LogWithMultipleHealthFlags,
            TestLogData.IncompleteLog,
            TestLogData.EmptyLog,
            TestLogData.LogWithContextualDate,
            TestLogData.LogWithMultipleParts
        };
        
        // Act & Assert
        foreach (var sample in samples)
        {
            var result = _parser.ParseLogContent(sample.Split(Environment.NewLine));
            result.Should().NotBeNull();
        }
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_NullInput_ThrowsException()
    {
        // Arrange
        IEnumerable<string>? nullInput = null;
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _parser.ParseLogContent(nullInput!));
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_ComplexRealWorldScenario_ParsesAllAttributes()
    {
        // Arrange - A complex log with all features
        var date = new DateTime(2026, 2, 5, 1, 0, 0);
        var logContent = new SampleLogBuilder()
            .WithHeader(date)
            .WithRegenStart(new TimeSpan(1, 0, 0))
            .WithSite("MfgSys")
            .WithDate(date)
            .WithPegging(new TimeSpan(1, 0, 46))
            .WithProcessingPart(new TimeSpan(1, 5, 0), "ABC123")
            .WithProcessingPart(new TimeSpan(1, 10, 0), "XYZ789")
            .WithError(new TimeSpan(1, 15, 0), "Connection timeout")
            .WithProcessingPart(new TimeSpan(1, 20, 0), "DEF456")
            .WithCompletion(new TimeSpan(2, 30, 0))
            .BuildLines();
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys");
        result.RunType.Should().Be("regen");
        result.StartTime.Should().NotBeNull();
        result.EndTime.Should().NotBeNull();
        result.HealthFlags.Should().Contain("error");
        result.HealthFlags.Should().Contain("timeout");
        result.Status.Should().Be("failed"); // Has errors
    }
}
