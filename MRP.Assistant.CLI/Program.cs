using McMaster.Extensions.CommandLineUtils;
using MRP.Assistant.CLI.Commands;

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
