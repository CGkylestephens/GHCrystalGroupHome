using FluentAssertions;
using MRP.Assistant.CLI.Commands;
using Xunit;

namespace MRP.Assistant.Tests.Integration;

[Trait("Category", "Integration")]
public class CLIIntegrationTests
{
    private readonly string _testDataPath;
    
    public CLIIntegrationTests()
    {
        var currentDir = Directory.GetCurrentDirectory();
        _testDataPath = FindTestDataDirectory(currentDir);
    }
    
    [Fact]
    public void CLI_CompareCommand_ProducesValidReport()
    {
        // Arrange
        var outputFile = Path.Combine("TestOutput", "cli_test_report.md");
        Directory.CreateDirectory("TestOutput");
        
        var command = new CompareCommand
        {
            RunAFile = Path.Combine(_testDataPath, "mrp_log_sample_A.txt"),
            RunBFile = Path.Combine(_testDataPath, "mrp_log_sample_B.txt"),
            OutputFile = outputFile,
            Format = "markdown"
        };
        
        // Act
        var exitCode = command.OnExecute();
        
        // Assert
        exitCode.Should().Be(0, "Command should succeed");
        File.Exists(outputFile).Should().BeTrue("Report file should be created");
        
        var reportContent = File.ReadAllText(outputFile);
        reportContent.Should().Contain("# MRP Log Comparison Report");
    }
    
    [Fact]
    public void CLI_ParseCommand_ShowsStatistics()
    {
        // Arrange
        var command = new ParseCommand
        {
            LogFile = Path.Combine(_testDataPath, "MRPRegenSample.txt"),
            Format = "summary"
        };
        
        // Capture console output
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        
        // Act
        var exitCode = command.OnExecute();
        
        // Restore console
        Console.SetOut(originalOut);
        var output = writer.ToString();
        
        // Assert
        exitCode.Should().Be(0, "Command should succeed");
        output.Should().Contain("MRP Log Summary");
        output.Should().Contain("Total Entries:");
        output.Should().Contain("Parts:");
        output.Should().Contain("Jobs:");
    }
    
    [Fact]
    public void CLI_DemoCommand_GeneratesReport()
    {
        // Arrange
        var outputFile = Path.Combine("TestOutput", "demo_test_report.md");
        var command = new DemoCommand
        {
            OutputFile = outputFile
        };
        
        // Act
        var exitCode = command.OnExecute();
        
        // Assert
        exitCode.Should().Be(0, "Demo command should succeed");
        File.Exists(outputFile).Should().BeTrue("Demo report should be created");
    }
    
    [Fact]
    public void CLI_CompareCommand_WithInvalidFile_ReturnsError()
    {
        // Arrange
        var command = new CompareCommand
        {
            RunAFile = "non_existent_file.txt",
            RunBFile = Path.Combine(_testDataPath, "mrp_log_sample_B.txt"),
            OutputFile = "output.md"
        };
        
        // Act
        var exitCode = command.OnExecute();
        
        // Assert
        exitCode.Should().Be(1, "Command should fail with invalid file");
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
