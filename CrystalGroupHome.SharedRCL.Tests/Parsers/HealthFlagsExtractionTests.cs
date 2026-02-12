using FluentAssertions;
using CrystalGroupHome.SharedRCL.Services;
using CrystalGroupHome.SharedRCL.Tests.Fixtures;
using Xunit;

namespace CrystalGroupHome.SharedRCL.Tests.Parsers;

public class HealthFlagsExtractionTests
{
    private readonly MrpLogParser _parser;
    
    public HealthFlagsExtractionTests()
    {
        _parser = new MrpLogParser();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithErrorKeyword_ExtractsErrorFlag()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 Processing starts",
            "01:05:00 ERROR: Something went wrong",
            "01:10:00 Processing continues"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("error");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithTimeoutKeyword_ExtractsTimeoutFlag()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 Processing starts",
            "01:05:00 Job 12345 encountered a timeout",
            "01:10:00 Processing continues"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("timeout");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithAbandonedKeyword_ExtractsAbandonedFlag()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 Processing starts",
            "01:05:00 Job 14567 abandoned due to timeout",
            "01:10:00 Processing continues"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("abandoned");
        result.HealthFlags.Should().Contain("timeout");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithDefunctKeyword_ExtractsDefunctFlag()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 Processing starts",
            "01:05:00 Part ABC123 is defunct",
            "01:10:00 Processing continues"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("defunct");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithFailedKeyword_ExtractsFailedFlag()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 Processing starts",
            "01:05:00 Operation failed",
            "01:10:00 Processing continues"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("failed");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithMultipleErrors_ExtractsEachFlagOnce()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 ERROR: First error",
            "01:05:00 ERROR: Second error",
            "01:10:00 ERROR: Third error"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("error");
        result.HealthFlags.Count(f => f == "error").Should().Be(1);
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithAllHealthFlags_ExtractsAll()
    {
        // Arrange
        var logContent = TestLogData.LogWithMultipleHealthFlags.Split(Environment.NewLine);
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().HaveCount(5);
        result.HealthFlags.Should().Contain("error");
        result.HealthFlags.Should().Contain("timeout");
        result.HealthFlags.Should().Contain("abandoned");
        result.HealthFlags.Should().Contain("defunct");
        result.HealthFlags.Should().Contain("failed");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_NoHealthIssues_ReturnsEmptyHealthFlags()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 Processing starts",
            "01:05:00 Processing part ABC123",
            "01:10:00 Processing complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().BeEmpty();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_CaseInsensitiveHealthFlags_StillExtracted()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 Error: Something went wrong",
            "01:05:00 TIMEOUT occurred",
            "01:10:00 Job Abandoned",
            "01:15:00 Part is DEFUNCT",
            "01:20:00 Process Failed"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain("error");
        result.HealthFlags.Should().Contain("timeout");
        result.HealthFlags.Should().Contain("abandoned");
        result.HealthFlags.Should().Contain("defunct");
        result.HealthFlags.Should().Contain("failed");
    }
    
    [Theory]
    [Trait("Category", "Parser")]
    [InlineData("error", "ERROR: Database connection failed")]
    [InlineData("timeout", "Request timeout after 30 seconds")]
    [InlineData("abandoned", "Job 123 has been abandoned")]
    [InlineData("defunct", "This part is now defunct")]
    [InlineData("failed", "The process has failed")]
    public void ParseLogContent_SingleHealthFlag_ExtractsCorrectly(string expectedFlag, string logLine)
    {
        // Arrange
        var logContent = new[] { logLine };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.HealthFlags.Should().Contain(expectedFlag);
    }
}
