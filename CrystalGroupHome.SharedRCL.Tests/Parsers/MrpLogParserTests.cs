using FluentAssertions;
using CrystalGroupHome.SharedRCL.Services;
using CrystalGroupHome.SharedRCL.Tests.Fixtures;
using Xunit;

namespace CrystalGroupHome.SharedRCL.Tests.Parsers;

public class MrpLogParserTests
{
    private readonly MrpLogParser _parser;
    
    public MrpLogParserTests()
    {
        _parser = new MrpLogParser();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public async Task ParseLogFileAsync_ValidLogFile_ReturnsPopulatedMetadata()
    {
        // Arrange
        var testFile = Path.Combine("..", "..", "..", "..", "testdata", "MRPRegenSample.txt");
        var fullPath = Path.GetFullPath(testFile);
        
        // Act
        var result = await _parser.ParseLogFileAsync(fullPath);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull("log should contain timestamps");
        result.RunType.Should().Be("regen", "log contains Building Pegging operations");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public async Task ParseLogFileAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var invalidFile = "non_existent_file_12345.log";
        
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _parser.ParseLogFileAsync(invalidFile));
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_EmptyContent_ReturnsMetadataWithDefaults()
    {
        // Arrange
        var emptyLines = Array.Empty<string>();
        
        // Act
        var result = _parser.ParseLogContent(emptyLines);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().BeNull();
        result.StartTime.Should().BeNull();
        result.EndTime.Should().BeNull();
        result.RunType.Should().Be("unknown");
        result.Status.Should().Be("uncertain");
        result.HealthFlags.Should().BeEmpty();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_SimpleRegenLog_ExtractsCorrectMetadata()
    {
        // Arrange
        var logContent = TestLogData.SimpleRegenLog.Split(Environment.NewLine);
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys");
        result.RunType.Should().Be("regen");
        result.StartTime.Should().NotBeNull();
        result.EndTime.Should().NotBeNull();
        result.Status.Should().Be("success");
        result.HealthFlags.Should().BeEmpty();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_SimpleNetChangeLog_DetectsNetChange()
    {
        // Arrange
        var logContent = TestLogData.SimpleNetChangeLog.Split(Environment.NewLine);
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys");
        result.RunType.Should().Be("net change");
        result.StartTime.Should().NotBeNull();
        result.EndTime.Should().NotBeNull();
        result.Status.Should().Be("success");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_LogWithErrors_ExtractsErrorFlags()
    {
        // Arrange
        var logContent = TestLogData.LogWithErrors.Split(Environment.NewLine);
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("error");
        result.HealthFlags.Should().Contain("timeout");
        result.HealthFlags.Should().Contain("abandoned");
        result.Status.Should().Be("failed");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_IncompleteLog_ReturnsIncompleteStatus()
    {
        // Arrange
        var logContent = TestLogData.IncompleteLog.Split(Environment.NewLine);
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        result.EndTime.Should().BeNull();
        result.Status.Should().Be("incomplete");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_LogWithMultipleHealthFlags_ExtractsAllFlags()
    {
        // Arrange
        var logContent = TestLogData.LogWithMultipleHealthFlags.Split(Environment.NewLine);
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("error");
        result.HealthFlags.Should().Contain("timeout");
        result.HealthFlags.Should().Contain("abandoned");
        result.HealthFlags.Should().Contain("defunct");
        result.HealthFlags.Should().Contain("failed");
        result.Status.Should().Be("failed");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_LogWithContextualDate_ExtractsDateAndTime()
    {
        // Arrange
        var logContent = TestLogData.LogWithContextualDate.Split(Environment.NewLine);
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        result.StartTime?.Date.Should().Be(new DateTime(2026, 6, 15));
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_RegenLogWithPegging_DetectsRegenType()
    {
        // Arrange
        var logContent = SampleLogBuilder.CreateRegenLog(new DateTime(2026, 2, 5, 1, 0, 0))
            .WithCompletion(new TimeSpan(2, 0, 0))
            .BuildLines();
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("regen");
        result.Site.Should().Be("MfgSys");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_NetChangeWithoutPegging_DetectsNetChangeType()
    {
        // Arrange
        var logContent = SampleLogBuilder.CreateNetChangeLog(new DateTime(2026, 2, 5, 14, 0, 0))
            .WithCompletion(new TimeSpan(14, 30, 0))
            .BuildLines();
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("net change");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_LogWithTimeout_SetsFailedStatus()
    {
        // Arrange
        var date = new DateTime(2026, 2, 5, 1, 0, 0);
        var logContent = new SampleLogBuilder()
            .WithHeader(date)
            .WithRegenStart(new TimeSpan(1, 0, 0))
            .WithSite("PLANT01")
            .WithDate(date)
            .WithTimeout(new TimeSpan(1, 30, 0), "14567")
            .BuildLines();
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("timeout");
        result.HealthFlags.Should().Contain("abandoned");
        result.Status.Should().Be("failed");
    }
}
