using FluentAssertions;
using CrystalGroupHome.SharedRCL.Services;
using CrystalGroupHome.SharedRCL.Tests.Fixtures;
using Xunit;

namespace CrystalGroupHome.SharedRCL.Tests.Parsers;

public class StatusDeterminationTests
{
    private readonly MrpLogParser _parser;
    
    public StatusDeterminationTests()
    {
        _parser = new MrpLogParser();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithErrorFlag_ReturnsFailedStatus()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin",
            "01:05:00 ERROR: Something went wrong",
            "01:10:00 Process ended"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("failed");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithFailedFlag_ReturnsFailedStatus()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin",
            "01:05:00 Process failed",
            "01:10:00 Cleanup"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("failed");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithAbandonedFlag_ReturnsFailedStatus()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin",
            "01:05:00 Job abandoned due to timeout"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("failed");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_StartTimeOnly_ReturnsIncompleteStatus()
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
    public void ParseLogContent_CompletedSuccessfully_ReturnsSuccessStatus()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin",
            "Date: 2/5/2026",
            "01:30:00 Processing...",
            "02:00:00 MRP process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("success");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_StartAndEndTimesNoErrors_ReturnsSuccessStatus()
    {
        // Arrange
        var logContent = TestLogData.SimpleRegenLog.Split(Environment.NewLine);
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().NotBeNull();
        result.EndTime.Should().NotBeNull();
        result.HealthFlags.Should().BeEmpty();
        result.Status.Should().Be("success");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_NoTimesNoErrors_ReturnsUncertainStatus()
    {
        // Arrange
        var logContent = new[]
        {
            "Some log content",
            "No timestamps",
            "No errors"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().BeNull();
        result.EndTime.Should().BeNull();
        result.Status.Should().Be("uncertain");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_ProcessComplete_ReturnsSuccessStatus()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin",
            "Date: 2/5/2026",
            "02:00:00 Process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("success");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_TimeoutOnly_ReturnsUncertainStatus()
    {
        // Arrange - Timeout without error or abandoned should not automatically fail
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin",
            "Date: 2/5/2026",
            "01:30:00 Connection timeout detected",
            "02:00:00 MRP process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("timeout");
        // Timeout alone doesn't fail the status if process completed
        result.Status.Should().Be("success");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_DefunctOnly_DoesNotFailStatus()
    {
        // Arrange - Defunct parts are informational, not necessarily a failure
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP process begin",
            "Date: 2/5/2026",
            "01:30:00 Part ABC123 is defunct",
            "02:00:00 MRP process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("defunct");
        result.Status.Should().Be("success");
    }
}
