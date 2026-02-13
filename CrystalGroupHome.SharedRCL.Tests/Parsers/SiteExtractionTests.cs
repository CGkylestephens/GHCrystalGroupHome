using FluentAssertions;
using CrystalGroupHome.SharedRCL.Services;
using CrystalGroupHome.SharedRCL.Tests.Fixtures;
using Xunit;

namespace CrystalGroupHome.SharedRCL.Tests.Parsers;

public class SiteExtractionTests
{
    private readonly MrpLogParser _parser;
    
    public SiteExtractionTests()
    {
        _parser = new MrpLogParser();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithSiteListArrow_ExtractsSiteName()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 MRP process begin",
            "Site List -> MfgSys",
            "01:05:00 Processing..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("MfgSys");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_WithSiteColon_ExtractsSiteName()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 MRP process begin",
            "Site: PLANT01",
            "01:05:00 Processing..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("PLANT01");
    }
    
    [Theory]
    [Trait("Category", "Parser")]
    [InlineData("MfgSys")]
    [InlineData("PLANT01")]
    [InlineData("SITE_123")]
    [InlineData("Factory-A")]
    public void ParseLogContent_VariousSiteNames_ExtractsCorrectly(string siteName)
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 MRP process begin",
            $"Site List -> {siteName}",
            "01:05:00 Processing..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be(siteName);
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_NoSiteInformation_ReturnsNull()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 MRP process begin",
            "01:05:00 Processing...",
            "01:10:00 Process complete"
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().BeNull();
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_SiteWithWhitespace_TrimsCorrectly()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 MRP process begin",
            "Site:   PLANT01  ",
            "01:05:00 Processing..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("PLANT01");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_MultipleSiteReferences_UsesFirst()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 MRP process begin",
            "Site: PLANT01",
            "01:05:00 Processing...",
            "Site: PLANT02"  // Second site reference
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("PLANT01");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_CaseInsensitiveSite_ExtractsCorrectly()
    {
        // Arrange
        var logContent = new[]
        {
            "01:00:00 MRP process begin",
            "site: PLANT01",
            "01:05:00 Processing..."
        };
        
        // Act
        var result = _parser.ParseLogContent(logContent);
        
        // Assert
        result.Should().NotBeNull();
        result.Site.Should().Be("PLANT01");
    }
    
    [Fact]
    [Trait("Category", "Parser")]
    public void ParseLogContent_SampleLogsWithSite_ExtractCorrectSites()
    {
        // Arrange & Act & Assert
        var regenResult = _parser.ParseLogContent(TestLogData.SimpleRegenLog.Split(Environment.NewLine));
        regenResult.Site.Should().Be("MfgSys");
        
        var netChangeResult = _parser.ParseLogContent(TestLogData.SimpleNetChangeLog.Split(Environment.NewLine));
        netChangeResult.Site.Should().Be("MfgSys");
        
        var errorResult = _parser.ParseLogContent(TestLogData.LogWithErrors.Split(Environment.NewLine));
        errorResult.Site.Should().Be("PLANT01");
    }
}
