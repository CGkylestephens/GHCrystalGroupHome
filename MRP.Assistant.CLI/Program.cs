using MRP.Assistant.CLI.Commands;

if (args.Length == 0)
{
    Console.WriteLine("MRP.Assistant - MRP Log Analysis Tool");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  mrp-assistant compare <run-a> <run-b> [--output <file>] [--format <format>]");
    Console.WriteLine("  mrp-assistant parse <log-file> [--format <format>]");
    Console.WriteLine("  mrp-assistant demo [--output <file>]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  compare    Compare two MRP log files");
    Console.WriteLine("  parse      Parse and summarize an MRP log file");
    Console.WriteLine("  demo       Generate a demo report");
    return 0;
}

var command = args[0].ToLower();

try
{
    switch (command)
    {
        case "compare":
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Error: compare requires two file arguments");
                return 1;
            }
            
            var compareCmd = new CompareCommand
            {
                RunAFile = args[1],
                RunBFile = args[2],
                OutputFile = GetOptionValue(args, "--output", "comparison_report.md"),
                Format = GetOptionValue(args, "--format", "markdown")
            };
            return compareCmd.OnExecute();
            
        case "parse":
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Error: parse requires a file argument");
                return 1;
            }
            
            var parseCmd = new ParseCommand
            {
                LogFile = args[1],
                Format = GetOptionValue(args, "--format", "summary")
            };
            return parseCmd.OnExecute();
            
        case "demo":
            var demoCmd = new DemoCommand
            {
                OutputFile = GetOptionValue(args, "--output", "demo_report.md")
            };
            return demoCmd.OnExecute();
            
        default:
            Console.Error.WriteLine($"Error: Unknown command '{command}'");
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    return 1;
}

static string GetOptionValue(string[] args, string option, string defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == option)
            return args[i + 1];
    }
    return defaultValue;
}
