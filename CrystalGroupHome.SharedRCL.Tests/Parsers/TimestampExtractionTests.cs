using FluentAssertions;
using CrystalGroupHome.SharedRCL.Services;
using CrystalGroupHome.SharedRCL.Tests.Fixtures;
using Xunit;

namespace CrystalGroupHome.SharedRCL.Tests.Parsers;

public class TimestampExtractionTests
{
    private readonly MrpLogParser _parser;
    
    public TimestampExtractionTests()
    {
        _parser = new MrpLogParser();
    }
    
    [Theory]
    [Trait("Category", "Parser")]
    [InlineData("01:00:46", "01:00:46 Building Pegging Demand Master...")]
    [InlineData("12:34:56", "12:34:56 Processing Part:ABC123")]
    [InlineData("00:00:00", "00:00:00 Start")]
    [InlineData("23:59:59", "23:59:59 End")]
    public void ParseLogContent_WithValidTimestamp_ExtractsTimestamp(string expectedTime, string contentLine)
    {
        // Arrange
        var date = new DateTime(2026, 6, 15);
        var logContent = new SampleLogBuilder()
            .WithLine("System.Collections.Hashtable")  // Header line 1
            .WithLine("==== Normal Planning Entries ====")  // Header line 2
            .WithDate(date)
            .WithLine(contentLine)
            .BuildLines();
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        var timeStr = result.StartTime!.Value.ToString("HH:mm:ss");
        timeStr.Should().Be(expectedTime);
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithHeaderDate_ParsesCorrectDateTime()
    {
        // Arrange
        var expectedDate = new DateTime(2026, 2, 5, 23, 59, 2);
        var logContent = new[]
        {
            "Thursday, February 5, 2026 23:59:02",
            "23:59:02 MRP Regeneration process begin"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        result.StartTime.Should().Be(expectedDate);
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithContextualDate_CombinesDateAndTime()
    {
        // Arrange
        var logContent = new[]
        {
            "==== Normal Planning Entries ====",
            "Date: 6/15/2026",
            "01:00:46 Building Pegging Demand Master..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        result.StartTime?.Date.Should().Be(new DateTime(2026, 6, 15));
        result.StartTime?.TimeOfDay.Should().Be(new TimeSpan(1, 0, 46));
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_StartAndEndTimes_BothExtracted()
    {
        // Arrange
        var date = new DateTime(2026, 2, 5, 1, 0, 0);
        var logContent = new SampleLogBuilder()
            .WithHeader(date)
            .WithRegenStart(new TimeSpan(1, 0, 0))
            .WithSite("MfgSys")
            .WithDate(date)
            .WithPegging(new TimeSpan(1, 0, 46))
            .WithCompletion(new TimeSpan(2, 30, 15))
            .BuildLines();
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        result.EndTime.Should().NotBeNull();
        result.EndTime.Should().BeAfter(result.StartTime!.Value);
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_NoTimestamps_ReturnsNullTimes()
    {
        // Arrange
        var logContent = new[]
        {
            "No timestamps in this log",
            "Just some text",
            "Nothing to see here"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().BeNull();
        result.EndTime.Should().BeNull();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_ExplicitUTCFormat_ParsesCorrectly()
    {
        // Arrange
        var logContent = new[]
        {
            "Start Time: 2024-02-01 02:00 UTC",
            "Some processing...",
            "End Time: 2024-02-01 03:10 UTC"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        result.EndTime.Should().NotBeNull();
        result.StartTime?.Year.Should().Be(2024);
        result.StartTime?.Month.Should().Be(2);
        result.StartTime?.Day.Should().Be(1);
        result.StartTime?.Hour.Should().Be(2);
        result.EndTime?.Hour.Should().Be(3);
    }
}
