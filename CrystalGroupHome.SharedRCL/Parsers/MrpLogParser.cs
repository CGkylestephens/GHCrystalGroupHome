using MRP.Assistant.Core;

namespace MRP.Assistant.Parsers;

public class MrpLogParser
{
    public MrpLogDocument Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Log file not found: {filePath}", filePath);
        }

        var document = new MrpLogDocument
        {
            SourceFile = filePath
        };

        var lines = File.ReadAllLines(filePath);
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;

            // Simple parsing - look for timestamps, errors, part/job numbers
            var entry = new MrpLogEntry
            {
                LineNumber = lineNumber,
                Message = line,
                EntryType = DetermineEntryType(line)
            };

            // Extract part and job numbers if present
            ExtractPartAndJobNumbers(line, entry);

            document.Entries.Add(entry);

            // Extract metadata
            ExtractMetadata(line, document);
        }

        return document;
    }

    private MrpLogEntryType DetermineEntryType(string line)
    {
        var lowerLine = line.ToLowerInvariant();
        if (lowerLine.Contains("error") || lowerLine.Contains("failed"))
            return MrpLogEntryType.Error;
        if (lowerLine.Contains("warning") || lowerLine.Contains("timeout"))
            return MrpLogEntryType.Warning;
        if (lowerLine.Contains("begin") || lowerLine.Contains("start"))
            return MrpLogEntryType.ProcessingStart;
        if (lowerLine.Contains("end") || lowerLine.Contains("complete"))
            return MrpLogEntryType.ProcessingEnd;
        return MrpLogEntryType.Info;
    }

    private void ExtractPartAndJobNumbers(string line, MrpLogEntry entry)
    {
        // Simple pattern matching for part/job numbers
        var parts = line.Split(new[] { ' ', ':', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Extract part number (looks for P followed by digits)
        foreach (var part in parts)
        {
            if (part.StartsWith("P") && part.Length > 1 && char.IsDigit(part[1]))
            {
                entry.PartNumber = part;
                break;
            }
        }

        // Extract job number (looks for J followed by digits)
        foreach (var part in parts)
        {
            if (part.StartsWith("J") && part.Length > 1 && char.IsDigit(part[1]))
            {
                entry.JobNumber = part;
                break;
            }
        }
    }

    private void ExtractMetadata(string line, MrpLogDocument document)
    {
        var lowerLine = line.ToLowerInvariant();

        // Extract site - look for "Site:" followed by a single word/identifier
        if (lowerLine.Contains("site") && line.Contains(":"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line,
                @"site[:\s]+([A-Za-z0-9_-]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success && string.IsNullOrEmpty(document.Site))
            {
                document.Site = match.Groups[1].Value.Trim();
            }
        }

        // Extract run type
        if (lowerLine.Contains("regen"))
        {
            document.RunType = "regen";
        }
        else if (lowerLine.Contains("net change"))
        {
            document.RunType = "net change";
        }

        // Extract timestamps - look for time patterns after keywords
        if (lowerLine.Contains("start") && lowerLine.Contains("time"))
        {
            // Try to extract time portion after "start time:"
            var match = System.Text.RegularExpressions.Regex.Match(
                line, 
                @"start\s+time[:\s]+(.+?)(?:\r|\n|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success && DateTime.TryParse(match.Groups[1].Value.Trim(), out var startTime))
            {
                document.StartTime = startTime;
            }
        }
        
        if (lowerLine.Contains("end") && lowerLine.Contains("time"))
        {
            // Try to extract time portion after "end time:"
            var match = System.Text.RegularExpressions.Regex.Match(
                line,
                @"end\s+time[:\s]+(.+?)(?:\r|\n|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success && DateTime.TryParse(match.Groups[1].Value.Trim(), out var endTime))
            {
                document.EndTime = endTime;
            }
        }
    }
}
