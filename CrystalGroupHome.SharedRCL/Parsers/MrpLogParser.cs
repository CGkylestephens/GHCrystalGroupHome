using MRP.Assistant.Core;

namespace MRP.Assistant.Parsers;

public class MrpLogParser
{
    public MrpLogDocument Parse(string filePath)
    {
        var document = new MrpLogDocument
        {
            SourceFile = filePath
        };

        if (!File.Exists(filePath))
        {
            document.ParsingErrors.Add($"File not found: {filePath}");
            return document;
        }

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
        // Part: P123456 or Part: 123456
        if (line.Contains("Part:") || line.Contains("P") && char.IsDigit(line.Skip(line.IndexOf('P') + 1).FirstOrDefault()))
        {
            var parts = line.Split(new[] { ' ', ':', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.StartsWith("P") && part.Length > 1 && char.IsDigit(part[1]))
                {
                    entry.PartNumber = part;
                    break;
                }
            }
        }

        // Job: J123456 or Job: 123456
        if (line.Contains("Job:") || line.Contains("J") && char.IsDigit(line.Skip(line.IndexOf('J') + 1).FirstOrDefault()))
        {
            var parts = line.Split(new[] { ' ', ':', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.StartsWith("J") && part.Length > 1 && char.IsDigit(part[1]))
                {
                    entry.JobNumber = part;
                    break;
                }
            }
        }
    }

    private void ExtractMetadata(string line, MrpLogDocument document)
    {
        var lowerLine = line.ToLowerInvariant();

        // Extract site
        if (lowerLine.Contains("site") && lowerLine.Contains(":"))
        {
            var parts = line.Split(':');
            if (parts.Length > 1)
            {
                document.Site = parts[1].Trim();
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

        // Extract timestamps - simple approach
        if (lowerLine.Contains("start") && DateTime.TryParse(line, out var startTime))
        {
            document.StartTime = startTime;
        }
        if (lowerLine.Contains("end") && DateTime.TryParse(line, out var endTime))
        {
            document.EndTime = endTime;
        }
    }
}
