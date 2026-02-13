using MRP.Assistant.Parsers;
using MRP.Assistant.Analysis;
using MRP.Assistant.Reporting;

namespace MRP.Assistant.CLI.Commands;

public class CompareCommand
{
    public string RunAFile { get; set; } = string.Empty;
    public string RunBFile { get; set; } = string.Empty;
    public string OutputFile { get; set; } = string.Empty;
    public string Format { get; set; } = "markdown";
    
    public int OnExecute()
    {
        try
        {
            if (!File.Exists(RunAFile))
            {
                Console.Error.WriteLine($"Error: File not found: {RunAFile}");
                return 1;
            }
            if (!File.Exists(RunBFile))
            {
                Console.Error.WriteLine($"Error: File not found: {RunBFile}");
                return 1;
            }
            
            var parser = new MrpLogParser();
            var runA = parser.Parse(RunAFile);
            var runB = parser.Parse(RunBFile);
            
            var comparer = new MrpLogComparer();
            var comparison = comparer.Compare(runA, runB);
            
            var explainer = new ExplanationEngine();
            var explanations = explainer.GenerateExplanations(comparison);
            
            var generator = new MrpReportGenerator();
            var report = generator.GenerateReport(comparison, explanations, new ReportOptions());
            
            File.WriteAllText(OutputFile, report);
            
            Console.WriteLine($"Comparison complete. Report written to: {OutputFile}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
