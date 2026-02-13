using FluentAssertions;
using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using MRP.Assistant.Reporting;
using MRP.Assistant.Core;
using System.Diagnostics;
using Xunit;

namespace MRP.Assistant.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Category", "Performance")]
public class PerformanceTests
{
    [Fact]
    public void Parser_HandlesLargeLog_UnderFiveSeconds()
    {
        // Arrange
        var testDataPath = FindTestDataDirectory(Directory.GetCurrentDirectory());
        var largeLogFile = Path.Combine(testDataPath, "AUTOMRPREGEN2026_20260205_2359.log");
        
        // Skip if large log not available
        if (!File.Exists(largeLogFile))
        {
            // Generate synthetic large log for testing
            largeLogFile = GenerateLargeTestLog(10000);
        }
        
        var parser = new MrpLogParser();
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        var result = parser.Parse(largeLogFile);
        stopwatch.Stop();
        
        // Assert
        result.Should().NotBeNull();
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(5, 
            "Parser should handle large logs in <5 seconds");
    }
    
    [Fact]
    public void Comparer_Handles10000EntriesEach_UnderFiveSeconds()
    {
        // Arrange
        var docA = GenerateSyntheticDocument("A", 10000);
        var docB = GenerateSyntheticDocument("B", 10000);
        
        var comparer = new MrpLogComparer();
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        var result = comparer.Compare(docA, docB);
        stopwatch.Stop();
        
        // Assert
        result.Should().NotBeNull();
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(5,
            "Comparer should handle 10k+ entries in <5 seconds");
    }
    
    [Fact]
    public void FullPipeline_WithRealLogs_CompletesInReasonableTime()
    {
        // Arrange
        var testDataPath = FindTestDataDirectory(Directory.GetCurrentDirectory());
        var fileA = Path.Combine(testDataPath, "mrp_log_sample_A.txt");
        var fileB = Path.Combine(testDataPath, "mrp_log_sample_B.txt");
        
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        
        var parser = new MrpLogParser();
        var runA = parser.Parse(fileA);
        var runB = parser.Parse(fileB);
        
        var comparer = new MrpLogComparer();
        var comparison = comparer.Compare(runA, runB);
        
        var explainer = new ExplanationEngine();
        var explanations = explainer.GenerateExplanations(comparison);
        
        var generator = new MrpReportGenerator();
        var report = generator.GenerateReport(comparison, explanations, new ReportOptions());
        
        stopwatch.Stop();
        
        // Assert
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(10,
            "Full pipeline should complete in <10 seconds");
    }
    
    private MrpLogDocument GenerateSyntheticDocument(string source, int entryCount)
    {
        var doc = new MrpLogDocument
        {
            SourceFile = $"{source}_synthetic.txt",
            RunType = MrpRunType.Regeneration,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1)
        };
        
        for (int i = 0; i < entryCount; i++)
        {
            doc.Entries.Add(new MrpLogEntry
            {
                RawLine = $"01:00:00 Processing Part:PART{i:D6}",
                PartNumber = $"PART{i:D6}",
                JobNumber = $"JOB{i:D6}",
                EntryType = MrpLogEntryType.ProcessingPart,
                LineNumber = i + 1
            });
        }
        
        return doc;
    }
    
    private string GenerateLargeTestLog(int lineCount)
    {
        var tempFile = Path.Combine("TestOutput", "large_synthetic_log.txt");
        Directory.CreateDirectory("TestOutput");
        
        using var writer = new StreamWriter(tempFile);
        for (int i = 0; i < lineCount; i++)
        {
            writer.WriteLine($"01:00:00 Processing Part:PART{i:D6}, Attribute Set:''");
            writer.WriteLine($"01:00:01 Demand: S: {i}/1/1 Date: 6/15/2026 Quantity: {i}.00000000");
            writer.WriteLine($"01:00:02 Supply: J: JOB{i:D6}/0/0 Date: 6/15/2026 Quantity: {i}.00000000");
        }
        
        return tempFile;
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
