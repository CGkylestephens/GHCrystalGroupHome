using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using MRP.Assistant.Core;
using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using MRP.Assistant.Reporting;

namespace MRP.Assistant.CLI.Commands;

[Command(Name = "compare", Description = "Compare two MRP log files")]
public class CompareCommand
{
    [Option("--run-a", Description = "Path to first log file (Run A)")]
    [Required]
    public string? RunAFile { get; set; }
    
    [Option("--run-b", Description = "Path to second log file (Run B)")]
    [Required]
    public string? RunBFile { get; set; }
    
    [Option("--output", Description = "Output file path (default: comparison_report.md)")]
    public string OutputFile { get; set; } = "comparison_report.md";
    
    [Option("--format", Description = "Report format: markdown, plaintext, html, json (default: markdown)")]
    public string Format { get; set; } = "markdown";
    
    [Option("--max-diffs", Description = "Maximum differences to show (default: 10)")]
    public int MaxDifferences { get; set; } = 10;
    
    public int OnExecute()
    {
        Console.WriteLine("MRP Log Comparison Tool");
        Console.WriteLine("======================");
        Console.WriteLine();
        
        // Validate inputs
        if (!File.Exists(RunAFile))
        {
            Console.Error.WriteLine($"Error: Run A file not found: {RunAFile}");
            return 1;
        }
        
        if (!File.Exists(RunBFile))
        {
            Console.Error.WriteLine($"Error: Run B file not found: {RunBFile}");
            return 1;
        }
        
        try
        {
            // Step 1: Parse logs
            Console.WriteLine($"Parsing Run A: {Path.GetFileName(RunAFile)}...");
            var parser = new MrpLogParser();
            var runA = parser.Parse(RunAFile!);
            Console.WriteLine($"  ✓ Parsed {runA.Entries.Count:N0} entries");
            
            Console.WriteLine($"Parsing Run B: {Path.GetFileName(RunBFile)}...");
            var runB = parser.Parse(RunBFile!);
            Console.WriteLine($"  ✓ Parsed {runB.Entries.Count:N0} entries");
            Console.WriteLine();
            
            // Step 2: Compare logs
            Console.WriteLine("Comparing logs...");
            var comparer = new MrpLogComparer();
            var comparison = comparer.Compare(runA, runB);
            Console.WriteLine($"  ✓ Found {comparison.Summary.TotalDifferences} differences");
            Console.WriteLine($"    - Critical: {comparison.Summary.CriticalCount}");
            Console.WriteLine($"    - Warning: {comparison.Summary.WarningCount}");
            Console.WriteLine($"    - Info: {comparison.Summary.InfoCount}");
            Console.WriteLine();
            
            // Step 3: Generate explanations
            Console.WriteLine("Generating explanations...");
            var explainer = new ExplanationEngine();
            var explanations = explainer.GenerateExplanations(comparison);
            Console.WriteLine($"  ✓ Generated {explanations.Count} explanations");
            Console.WriteLine();
            
            // Step 4: Generate report
            Console.WriteLine("Generating report...");
            var reportFormat = Enum.Parse<ReportFormat>(Format, ignoreCase: true);
            var reportOptions = new ReportOptions
            {
                Format = reportFormat,
                MaxDifferencesToShow = MaxDifferences
            };
            
            var generator = new MrpReportGenerator();
            var report = generator.GenerateReport(comparison, explanations, reportOptions);
            
            // Step 5: Write report
            File.WriteAllText(OutputFile, report);
            Console.WriteLine($"  ✓ Report saved to: {OutputFile}");
            Console.WriteLine();
            
            // Summary
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Comparison complete!");
            Console.ResetColor();
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error during comparison: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
