using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using MRP.Assistant.Parsers;
using MRP.Assistant.Core;
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
    
    public int OnExecute()
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
