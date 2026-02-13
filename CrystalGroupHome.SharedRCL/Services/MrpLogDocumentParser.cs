using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.SharedRCL.Services
{
    /// <summary>
    /// Service for parsing MRP log files into detailed document structures.
    /// </summary>
    public class MrpLogDocumentParser
    {
        private readonly MrpLogParser _metadataParser;

        public MrpLogDocumentParser()
        {
            _metadataParser = new MrpLogParser();
        }

        /// <summary>
        /// Parses an MRP log file into a detailed document structure.
        /// </summary>
        /// <param name="filePath">The path to the MRP log file.</param>
        /// <returns>An MrpLogDocument with metadata and entries.</returns>
        public async Task<MrpLogDocument> ParseLogFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Log file not found", filePath);
            }

            var lines = await File.ReadAllLinesAsync(filePath);
            return ParseLogContent(lines);
        }

        /// <summary>
        /// Parses MRP log content from lines into a detailed document structure.
        /// </summary>
        /// <param name="lines">The lines of the log file.</param>
        /// <returns>An MrpLogDocument with metadata and entries.</returns>
        public MrpLogDocument ParseLogContent(IEnumerable<string> lines)
        {
            var linesList = lines.ToList();
            var document = new MrpLogDocument
            {
                Metadata = _metadataParser.ParseLogContent(linesList),
                RawLines = linesList
            };

            // Parse detailed entries from the log
            document.Entries = ParseEntries(linesList);

            return document;
        }

        /// <summary>
        /// Parses individual entries from log lines.
        /// </summary>
        private List<MrpLogEntry> ParseEntries(List<string> lines)
        {
            var entries = new List<MrpLogEntry>();
            var lineNumber = 0;

            foreach (var line in lines)
            {
                lineNumber++;
                
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                var entry = new MrpLogEntry
                {
                    LineNumber = lineNumber,
                    RawLine = line
                };

                // Extract job number
                entry.JobNumber = ExtractJobNumber(line);

                // Extract part number
                entry.PartNumber = ExtractPartNumber(line);

                // Extract date
                entry.Date = ExtractDate(line);

                // Extract quantity
                entry.Quantity = ExtractQuantity(line);

                // Determine if this is an error entry
                entry.IsError = IsErrorLine(line);

                // Determine entry type
                entry.EntryType = DetermineEntryType(line, entry);

                // Only add entries that have meaningful content
                if (!string.IsNullOrEmpty(entry.JobNumber) || 
                    !string.IsNullOrEmpty(entry.PartNumber) || 
                    entry.IsError ||
                    entry.Date.HasValue)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        /// <summary>
        /// Extracts job number from a log line.
        /// </summary>
        private string? ExtractJobNumber(string line)
        {
            // Pattern: "Job 14567" or "Job: 14567" or "J:14567"
            var match = Regex.Match(line, @"\bJob\s*:?\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            match = Regex.Match(line, @"\bJ:\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            return null;
        }

        /// <summary>
        /// Extracts part number from a log line.
        /// </summary>
        private string? ExtractPartNumber(string line)
        {
            // Pattern: "Part: ABC123" or "Part ABC123"
            var match = Regex.Match(line, @"\bPart\s*:?\s*([A-Z0-9-]+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            // Pattern: "P:ABC123"
            match = Regex.Match(line, @"\bP:\s*([A-Z0-9-]+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            return null;
        }

        /// <summary>
        /// Extracts date from a log line.
        /// </summary>
        private DateTime? ExtractDate(string line)
        {
            // Pattern: "2024-02-01", "2/15/2026", "02/15/2026"
            var match = Regex.Match(line, @"(\d{4}-\d{2}-\d{2})");
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date1))
                return date1;

            match = Regex.Match(line, @"(\d{1,2}/\d{1,2}/\d{4})");
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date2))
                return date2;

            return null;
        }

        /// <summary>
        /// Extracts quantity from a log line.
        /// </summary>
        private decimal? ExtractQuantity(string line)
        {
            // Pattern: "Quantity: 100" or "Qty: 100" or "Q:100"
            var match = Regex.Match(line, @"\b(?:Quantity|Qty|Q)\s*:?\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var qty))
                return qty;

            return null;
        }

        /// <summary>
        /// Determines if a line represents an error.
        /// </summary>
        private bool IsErrorLine(string line)
        {
            return Regex.IsMatch(line, @"\b(ERROR|timeout|abandoned|defunct|failed|cannot)\b", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Determines the type of entry.
        /// </summary>
        private string DetermineEntryType(string line, MrpLogEntry entry)
        {
            if (entry.IsError)
                return "Error";

            if (!string.IsNullOrEmpty(entry.JobNumber))
                return "Job";

            if (!string.IsNullOrEmpty(entry.PartNumber))
                return "Part";

            if (Regex.IsMatch(line, @"\b(Supply|Demand)\b", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(line, @"\bSupply\b", RegexOptions.IgnoreCase))
                    return "Supply";
                return "Demand";
            }

            return "Other";
        }
    }
}
