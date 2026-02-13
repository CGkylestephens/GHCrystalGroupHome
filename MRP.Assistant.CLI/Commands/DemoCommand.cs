using McMaster.Extensions.CommandLineUtils;

namespace MRP.Assistant.CLI.Commands;

[Command(Name = "demo", Description = "Generate sample report from included test data")]
public class DemoCommand
{
    [Option("--output", Description = "Output file path (default: demo_report.md)")]
    public string OutputFile { get; set; } = "demo_report.md";
    
    public int OnExecute()
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
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "testdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "testdata")
        };
        
        var testDataDir = testDataPaths.FirstOrDefault(Directory.Exists);
        
        if (testDataDir == null)
        {
            Console.Error.WriteLine("Error: Could not find testdata directory");
            Console.Error.WriteLine("Make sure you run this from the project root");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Searched paths:");
            foreach (var path in testDataPaths)
            {
                Console.Error.WriteLine($"  - {path}");
            }
            return 1;
        }
        
        // Look for any sample log files in testdata
        var logFiles = Directory.GetFiles(testDataDir, "*.log")
            .Concat(Directory.GetFiles(testDataDir, "*.txt"))
            .OrderBy(f => f)
            .ToList();
        
        if (logFiles.Count < 2)
        {
            Console.Error.WriteLine("Error: Need at least 2 log files in testdata for demo");
            Console.Error.WriteLine($"Found {logFiles.Count} files in: {testDataDir}");
            return 1;
        }
        
        var fileA = logFiles[0];
        var fileB = logFiles[1];
        
        Console.WriteLine($"Using files:");
        Console.WriteLine($"  Run A: {Path.GetFileName(fileA)}");
        Console.WriteLine($"  Run B: {Path.GetFileName(fileB)}");
        Console.WriteLine();
        
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
