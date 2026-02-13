using FluentAssertions;
using CrystalGroupHome.SharedRCL.Services;
using CrystalGroupHome.SharedRCL.Tests.Fixtures;
using Xunit;

namespace CrystalGroupHome.SharedRCL.Tests.Parsers;

public class RunTypeDetectionTests
{
    private readonly MrpLogParser _parser;
    
    public RunTypeDetectionTests()
    {
        _parser = new MrpLogParser();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithRegenerationKeyword_ReturnsRegenType()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP Regeneration process begin"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("regen");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithMRPRegenKeyword_ReturnsRegenType()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 01:00:00",
            "01:00:00 MRP Regen starting..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("regen");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithNetChangeKeyword_ReturnsNetChangeType()
    {
        // Arrange
        var logContent = new[]
        {
            "Thursday, February 5, 2026 14:00:00",
            "14:00:00 MRP Net Change process begin"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("net change");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithBuildingPegging_ReturnsRegenType()
    {
        // Arrange
        var logContent = new[]
        {
            "Date: 6/15/2026",
            "01:00:46 Building Pegging Demand Master...",
            "01:05:00 Processing continues..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("regen");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithProcessingPartOnly_ReturnsNetChangeType()
    {
        // Arrange
        var logContent = new[]
        {
            "Date: 6/15/2026",
            "01:00:00 Start Processing Part:ABC123",
            "01:05:00 Processing continues..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("net change");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_NoRunTypeIndicators_ReturnsUnknown()
    {
        // Arrange
        var logContent = new[]
        {
            "Some log content",
            "No specific run type mentioned",
            "Just generic processing"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("unknown");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_MixedIndicators_PrefersExplicitKeyword()
    {
        // Arrange - Net Change explicitly mentioned, even with processing
        var logContent = new[]
        {
            "Thursday, February 5, 2026 14:00:00",
            "14:00:00 MRP Net Change process begin",
            "14:01:00 Start Processing Part:ABC123"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("net change");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_CaseInsensitiveKeywords_DetectsRunType()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 mrp REGENERATION process begin"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.RunType.Should().Be("regen");
    }
}
