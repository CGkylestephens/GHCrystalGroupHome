using MRP.Assistant.Core;
using System.Text.RegularExpressions;

namespace MRP.Assistant.Parsers;

public class MrpLogParser
{
    private static readonly Regex JobPattern = new(@"Job[:\s]+([A-Z0-9\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PartPattern = new(@"Part[:\s]+([A-Z0-9\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DemandPattern = new(@"Demand:\s+S:\s+(\d+/\d+/\d+)", RegexOptions.Compiled);
    private static readonly Regex SupplyPattern = new(@"Supply:\s+J:\s+([A-Z0-9\-]+/\d+/\d+)", RegexOptions.Compiled);
    
    public MrpLogDocument Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");
        
        var lines = File.ReadAllLines(filePath);
        return ParseLines(lines, filePath);
    }
    
    private MrpLogDocument ParseLines(string[] lines, string sourceFile)
    {
        var doc = new MrpLogDocument
        {
            SourceFile = sourceFile
        };
        
        // Detect run type
        var content = string.Join(" ", lines);
        if (content.Contains("Regen", StringComparison.OrdinalIgnoreCase) || 
            content.Contains("Building Pegging", StringComparison.OrdinalIgnoreCase))
        {
            doc.RunType = MrpRunType.Regeneration;
        }
        else if (content.Contains("Net Change", StringComparison.OrdinalIgnoreCase))
        {
            doc.RunType = MrpRunType.NetChange;
        }
        
        // Parse entries
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var entry = new MrpLogEntry
            {
                LineNumber = i + 1,
                RawLine = line
            };
            
            // Extract job number
            var jobMatch = JobPattern.Match(line);
            if (jobMatch.Success)
            {
                entry.JobNumber = jobMatch.Groups[1].Value;
            }
            
            // Extract part number
            var partMatch = PartPattern.Match(line);
            if (partMatch.Success)
            {
                entry.PartNumber = partMatch.Groups[1].Value;
            }
            
            // Extract demand
            var demandMatch = DemandPattern.Match(line);
            if (demandMatch.Success)
            {
                entry.DemandSource = demandMatch.Groups[1].Value;
                entry.EntryType = MrpLogEntryType.Demand;
            }
            
            // Extract supply
            var supplyMatch = SupplyPattern.Match(line);
            if (supplyMatch.Success)
            {
                entry.SupplySource = supplyMatch.Groups[1].Value;
                entry.EntryType = MrpLogEntryType.Supply;
            }
            
            // Detect errors
            if (line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("abandoned", StringComparison.OrdinalIgnoreCase))
            {
                entry.EntryType = MrpLogEntryType.Error;
                entry.ErrorMessage = line;
            }
            
            // Extract start/end times
            if (line.Contains("Start Time:", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(line.Split(':',2)[1].Trim(), out var startTime))
                    doc.StartTime = startTime;
            }
            if (line.Contains("End Time:", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(line.Split(':', 2)[1].Trim(), out var endTime))
                    doc.EndTime = endTime;
            }
            if (line.Contains("Site:", StringComparison.OrdinalIgnoreCase))
            {
                doc.Site = line.Split(':', 2)[1].Trim();
            }
            
            doc.Entries.Add(entry);
        }
        
        return doc;
    }
}
