using MRP.Assistant.Parsers;

namespace MRP.Assistant.CLI.Commands;

public class ParseCommand
{
    public string LogFile { get; set; } = string.Empty;
    public string Format { get; set; } = "summary";
    
    public int OnExecute()
    {
        try
        {
            if (!File.Exists(LogFile))
            {
                Console.Error.WriteLine($"Error: File not found: {LogFile}");
                return 1;
            }
            
            var parser = new MrpLogParser();
            var doc = parser.Parse(LogFile);
            
            Console.WriteLine("MRP Log Summary");
            Console.WriteLine("===============");
            Console.WriteLine($"File: {doc.SourceFile}");
            Console.WriteLine($"Run Type: {doc.RunType}");
            Console.WriteLine($"Start Time: {doc.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
            Console.WriteLine($"End Time: {doc.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
            Console.WriteLine($"Site: {doc.Site ?? "Unknown"}");
            Console.WriteLine($"Total Entries: {doc.Entries.Count}");
            
            var parts = doc.Entries.Where(e => !string.IsNullOrEmpty(e.PartNumber))
                .Select(e => e.PartNumber).Distinct().Count();
            var jobs = doc.Entries.Where(e => !string.IsNullOrEmpty(e.JobNumber))
                .Select(e => e.JobNumber).Distinct().Count();
            var errors = doc.Entries.Count(e => e.EntryType == Core.MrpLogEntryType.Error);
            
            Console.WriteLine($"Parts: {parts}");
            Console.WriteLine($"Jobs: {jobs}");
            Console.WriteLine($"Errors: {errors}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
