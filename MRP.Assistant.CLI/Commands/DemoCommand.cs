using MRP.Assistant.Core;
using MRP.Assistant.Analysis;
using MRP.Assistant.Reporting;

namespace MRP.Assistant.CLI.Commands;

public class DemoCommand
{
    public string OutputFile { get; set; } = "demo_report.md";
    
    public int OnExecute()
    {
        try
        {
            // Create demo documents
            var runA = new MrpLogDocument
            {
                SourceFile = "demo_run_a.txt",
                RunType = MrpRunType.Regeneration,
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now.AddDays(-1).AddHours(1),
                Site = "DEMO"
            };
            
            var runB = new MrpLogDocument
            {
                SourceFile = "demo_run_b.txt",
                RunType = MrpRunType.NetChange,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                Site = "DEMO"
            };
            
            var comparison = new MrpLogComparison
            {
                RunA = runA,
                RunB = runB
            };
            
            comparison.Differences.Add(new MrpDifference
            {
                Type = DifferenceType.JobRemoved,
                Severity = DifferenceSeverity.Warning,
                JobNumber = "DEMO123",
                Description = "Demo job removed"
            });
            
            comparison.Summary.TotalDifferences = 1;
            comparison.Summary.WarningDifferences = 1;
            
            var explanations = new List<Explanation>
            {
                new Explanation
                {
                    Type = ExplanationType.Fact,
                    Text = "This is a demo report",
                    Confidence = 1.0
                }
            };
            
            var generator = new MrpReportGenerator();
            var report = generator.GenerateReport(comparison, explanations, new ReportOptions());
            
            File.WriteAllText(OutputFile, report);
            
            Console.WriteLine($"Demo report generated: {OutputFile}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
