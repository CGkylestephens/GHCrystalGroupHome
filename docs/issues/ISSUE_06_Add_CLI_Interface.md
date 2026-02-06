---
name: Add CLI Interface for File-Based Comparison
about: Create command-line interface that ties all components together
title: "[Agent Task] Add CLI Interface for File-Based Comparison"
labels: [cli, agent]
assignees: [copilot]
---

## 🧠 Task Intent
Create a user-friendly command-line interface (CLI) that integrates the parser, comparer, explainer, and reporter into a cohesive tool. Planners should be able to compare logs, parse individual files, and generate demo reports with simple commands.

## 🔍 Scope / Input
**Dependencies**: Issues #2, #3, #4, #5 must be complete (all core components exist)

**CLI Framework**: Use `McMaster.Extensions.CommandLineUtils` for command parsing

**Commands to Implement**:
1. `compare` - Compare two MRP log files
2. `parse` - Parse a single log file and show stats
3. `demo` - Generate sample report from included test data

## ✅ Expected Output

### 1. Update Project File

**MRP.Assistant.CLI.csproj** - Add package reference:
```xml
<ItemGroup>
  <PackageReference Include="McMaster.Extensions.CommandLineUtils" Version="4.1.1" />
</ItemGroup>
```

### 2. Main Program (Program.cs)

```csharp
using McMaster.Extensions.CommandLineUtils;
using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using MRP.Assistant.Reporting;

namespace MRP.Assistant.CLI;

[Command(Name = "mrp-assistant", Description = "Epicor MRP Log Investigation Assistant")]
[Subcommand(typeof(CompareCommand), typeof(ParseCommand), typeof(DemoCommand))]
public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return CommandLineApplication.Execute<Program>(args);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
    
    private int OnExecute(CommandLineApplication app)
    {
        app.ShowHelp();
        return 0;
    }
}
```

### 3. Compare Command

**Commands/CompareCommand.cs**:
```csharp
using McMaster.Extensions.CommandLineUtils;
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
    
    private int OnExecute()
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
```

### 4. Parse Command

**Commands/ParseCommand.cs**:
```csharp
using McMaster.Extensions.CommandLineUtils;
using MRP.Assistant.Parsers;
using System.Text.Json;

namespace MRP.Assistant.CLI.Commands;

[Command(Name = "parse", Description = "Parse a single MRP log file and show statistics")]
public class ParseCommand
{
    [Option("--file", Description = "Path to MRP log file")]
    [Required]
    public string? LogFile { get; set; }
    
    [Option("--format", Description = "Output format: summary, json (default: summary)")]
    public string Format { get; set; } = "summary";
    
    private int OnExecute()
    {
        if (!File.Exists(LogFile))
        {
            Console.Error.WriteLine($"Error: Log file not found: {LogFile}");
            return 1;
        }
        
        try
        {
            Console.WriteLine($"Parsing: {Path.GetFileName(LogFile)}...");
            Console.WriteLine();
            
            var parser = new MrpLogParser();
            var document = parser.Parse(LogFile!);
            
            if (Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(document, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                Console.WriteLine(json);
            }
            else
            {
                // Summary format
                Console.WriteLine("=== MRP Log Summary ===");
                Console.WriteLine();
                Console.WriteLine($"Source File:    {Path.GetFileName(document.SourceFile)}");
                Console.WriteLine($"Run Type:       {document.RunType}");
                Console.WriteLine($"Site:           {document.Site ?? "Unknown"}");
                Console.WriteLine($"Start Time:     {document.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
                Console.WriteLine($"End Time:       {document.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
                Console.WriteLine($"Duration:       {document.Duration?.ToString(@"hh\:mm\:ss") ?? "Unknown"}");
                Console.WriteLine();
                Console.WriteLine($"Total Entries:  {document.Entries.Count:N0}");
                Console.WriteLine($"Parts:          {document.Entries.Where(e => !string.IsNullOrEmpty(e.PartNumber)).Select(e => e.PartNumber).Distinct().Count()}");
                Console.WriteLine($"Jobs:           {document.Entries.Where(e => !string.IsNullOrEmpty(e.JobNumber)).Select(e => e.JobNumber).Distinct().Count()}");
                Console.WriteLine($"Errors:         {document.Entries.Count(e => e.EntryType == MrpLogEntryType.Error)}");
                Console.WriteLine($"Warnings:       {document.Entries.Count(e => e.EntryType == MrpLogEntryType.Warning)}");
                Console.WriteLine();
                
                if (document.ParsingErrors.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Parsing Errors: {document.ParsingErrors.Count}");
                    Console.ResetColor();
                }
            }
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Parse complete!");
            Console.ResetColor();
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error parsing log: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
```

### 5. Demo Command

**Commands/DemoCommand.cs**:
```csharp
using McMaster.Extensions.CommandLineUtils;

namespace MRP.Assistant.CLI.Commands;

[Command(Name = "demo", Description = "Generate sample report from included test data")]
public class DemoCommand
{
    [Option("--output", Description = "Output file path (default: demo_report.md)")]
    public string OutputFile { get; set; } = "demo_report.md";
    
    private int OnExecute()
    {
        Console.WriteLine("MRP Assistant Demo");
        Console.WriteLine("==================");
        Console.WriteLine();
        Console.WriteLine("This will compare sample logs A and B...");
        Console.WriteLine();
        
        // Find testdata directory (try multiple paths)
        var testDataPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "testdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "testdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "testdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "testdata")
        };
        
        var testDataDir = testDataPaths.FirstOrDefault(Directory.Exists);
        
        if (testDataDir == null)
        {
            Console.Error.WriteLine("Error: Could not find testdata directory");
            Console.Error.WriteLine("Make sure you run this from the project root");
            return 1;
        }
        
        var fileA = Path.Combine(testDataDir, "mrp_log_sample_A.txt");
        var fileB = Path.Combine(testDataDir, "mrp_log_sample_B.txt");
        
        if (!File.Exists(fileA) || !File.Exists(fileB))
        {
            Console.Error.WriteLine("Error: Sample log files not found in testdata");
            return 1;
        }
        
        // Invoke compare command
        var compareCmd = new CompareCommand
        {
            RunAFile = fileA,
            RunBFile = fileB,
            OutputFile = OutputFile,
            Format = "markdown"
        };
        
        var result = compareCmd.OnExecute();
        
        if (result == 0)
        {
            Console.WriteLine();
            Console.WriteLine("Demo complete! Open the report to see the results:");
            Console.WriteLine($"  {Path.GetFullPath(OutputFile)}");
        }
        
        return result;
    }
}
```

## 🧪 Acceptance Criteria
- [ ] McMaster.Extensions.CommandLineUtils package added
- [ ] All 3 commands implemented (compare, parse, demo)
- [ ] `compare` command works with two log files
- [ ] `parse` command shows summary statistics
- [ ] `demo` command generates report from test data
- [ ] Help text is clear and informative
- [ ] Error messages are helpful and user-friendly
- [ ] Exit codes: 0 = success, 1 = error
- [ ] File paths validated before processing
- [ ] Progress messages show during long operations
- [ ] Colored output for success/error (green/red)

## 🧪 Validation Commands
```bash
# Build CLI
dotnet build MRP.Assistant.CLI/MRP.Assistant.CLI.csproj

# Show help
dotnet run --project MRP.Assistant.CLI -- --help

# Compare command help
dotnet run --project MRP.Assistant.CLI -- compare --help

# Run demo
dotnet run --project MRP.Assistant.CLI -- demo

# Parse single file
dotnet run --project MRP.Assistant.CLI -- parse --file testdata/MRPRegenSample.txt

# Compare two files
dotnet run --project MRP.Assistant.CLI -- compare \
  --run-a testdata/mrp_log_sample_A.txt \
  --run-b testdata/mrp_log_sample_B.txt \
  --output test_report.md
```

**Expected `--help` Output**:
```
MRP.Assistant CLI - Epicor MRP Log Investigation Assistant

Usage: mrp-assistant [command] [options]

Commands:
  compare    Compare two MRP log files
  parse      Parse a single MRP log file and show statistics
  demo       Generate sample report from included test data

Run 'mrp-assistant [command] --help' for more information on a command.
```

## 📝 Notes
- Use McMaster.Extensions.CommandLineUtils for robust CLI parsing
- Validate all file paths before processing
- Show progress during long operations
- Use colored console output for clarity
- Handle exceptions gracefully with helpful error messages
- Demo command is useful for first-time users
- Next issues will add comprehensive tests
