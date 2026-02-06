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
    /// Service for parsing MRP (Material Requirements Planning) log files and extracting metadata.
    /// </summary>
    public class MrpLogParser
    {
        // Constants for parsing thresholds
        private const int HeaderLinesToSkip = 2;
        private const int EndOfFileThreshold = 20;
        private const int DateSearchLineLimit = 5;

        /// <summary>
        /// Parses an MRP log file and extracts top-level run metadata.
        /// </summary>
        /// <param name="filePath">The path to the MRP log file.</param>
        /// <returns>An MrpRunMetadata object containing the parsed metadata.</returns>
        public async Task<MrpRunMetadata> ParseLogFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Log file not found", filePath);
            }

            var lines = await File.ReadAllLinesAsync(filePath);
            return ParseLogContent(lines);
        }

        /// <summary>
        /// Parses MRP log content from a collection of lines.
        /// </summary>
        /// <param name="lines">The lines of the log file.</param>
        /// <returns>An MrpRunMetadata object containing the parsed metadata.</returns>
        public MrpRunMetadata ParseLogContent(IEnumerable<string> lines)
        {
            var metadata = new MrpRunMetadata();
            var linesList = lines.ToList();

            // Parse start time
            metadata.StartTime = ExtractStartTime(linesList);

            // Parse end time
            metadata.EndTime = ExtractEndTime(linesList);

            // Parse site name
            metadata.Site = ExtractSite(linesList);

            // Determine run type (regen vs net change)
            metadata.RunType = DetermineRunType(linesList);

            // Check for health flags
            metadata.HealthFlags = ExtractHealthFlags(linesList);

            // Determine status
            metadata.Status = DetermineStatus(metadata, linesList);

            return metadata;
        }

        private DateTime? ExtractStartTime(List<string> lines)
        {
            // Look for patterns like:
            // "Start Time: 2024-02-01 02:00 UTC"
            // "Thursday, February 5, 2026 11:59:02"
            // "23:59:02 MRP Regeneration process begin"
            // "01:00:46 Building Pegging Demand Master..." (from MRP logs with contextual dates)
            
            foreach (var line in lines)
            {
                // Pattern: "Start Time: 2024-02-01 02:00 UTC"
                var match = Regex.Match(line, @"Start Time:\s*(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2})\s*UTC", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd HH:mm", 
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var startTime))
                    {
                        return startTime;
                    }
                }

                // Pattern: "Thursday, February 5, 2026 11:59:02" (first line of file)
                match = Regex.Match(line, @"^[A-Za-z]+,\s+([A-Za-z]+\s+\d{1,2},\s+\d{4}\s+\d{2}:\d{2}:\d{2})");
                if (match.Success)
                {
                    if (DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
                    {
                        return startTime;
                    }
                }

                // Pattern: "23:59:02 MRP Regeneration process begin"
                match = Regex.Match(line, @"^(\d{2}:\d{2}:\d{2})\s+MRP.*process begin", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    // Look for date from previous lines
                    var dateTime = TryParseTimeWithDate(lines, match.Groups[1].Value);
                    if (dateTime.HasValue)
                    {
                        return dateTime;
                    }
                }
            }

            // If no explicit start time found, try to extract first timestamp with contextual date
            var contextualDate = ExtractContextualDate(lines);
            if (contextualDate.HasValue)
            {
                // Find the first timestamp in the file
                foreach (var line in lines.Skip(HeaderLinesToSkip)) // Skip header lines
                {
                    var match = Regex.Match(line, @"^(\d{2}:\d{2}:\d{2})");
                    if (match.Success)
                    {
                        var timeStr = match.Groups[1].Value;
                        if (TimeSpan.TryParse(timeStr, out var time))
                        {
                            return contextualDate.Value.Date.Add(time);
                        }
                    }
                }
            }

            return null;
        }

        private DateTime? ExtractEndTime(List<string> lines)
        {
            // Look for patterns like:
            // "End Time: 2024-02-01 03:10 UTC"
            
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"End Time:\s*(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2})\s*UTC", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var endTime))
                    {
                        return endTime;
                    }
                }
            }

            // Try to find last timestamp with contextual date
            var contextualDate = ExtractContextualDate(lines);
            if (contextualDate.HasValue)
            {
                // Find the last timestamp in the file that looks like an end time
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    var match = Regex.Match(line, @"^(\d{2}:\d{2}:\d{2})");
                    if (match.Success)
                    {
                        var timeStr = match.Groups[1].Value;
                        if (TimeSpan.TryParse(timeStr, out var time))
                        {
                            // Only return if we've found a reasonable end time (not just any timestamp)
                            // Look for indicators this might be an end time
                            if (Regex.IsMatch(line, @"(finish|complete|done|end)", RegexOptions.IgnoreCase) ||
                                i > lines.Count - EndOfFileThreshold) // Or if it's near the end of the file
                            {
                                return contextualDate.Value.Date.Add(time);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private string? ExtractSite(List<string> lines)
        {
            // Look for patterns like:
            // "Site: PLANT01"
            // "Site List -> MfgSys"
            
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"Site:\s*([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }

                match = Regex.Match(line, @"Site List\s*->\s*([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return null;
        }

        private string DetermineRunType(List<string> lines)
        {
            // Check for explicit mentions or patterns in lines
            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"MRP\s+Regen", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"Regeneration", RegexOptions.IgnoreCase))
                {
                    return "regen";
                }

                if (Regex.IsMatch(line, @"Net\s+Change", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"NetChange", RegexOptions.IgnoreCase))
                {
                    return "net change";
                }
            }

            // Check for indicators in the log patterns
            var hasPegging = false;
            var hasProcessingPart = false;

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"Building\s+Pegging", RegexOptions.IgnoreCase))
                {
                    hasPegging = true;
                }

                if (Regex.IsMatch(line, @"Start\s+Processing\s+Part", RegexOptions.IgnoreCase))
                {
                    hasProcessingPart = true;
                }
            }

            // Regen runs typically have "Building Pegging" operations
            if (hasPegging)
            {
                return "regen";
            }

            // Net change runs typically have "Start Processing Part" without full pegging
            if (hasProcessingPart && !hasPegging)
            {
                return "net change";
            }

            return "unknown";
        }

        private List<string> ExtractHealthFlags(List<string> lines)
        {
            var flags = new List<string>();

            foreach (var line in lines)
            {
                // Check for various error indicators
                if (Regex.IsMatch(line, @"\bERROR\b", RegexOptions.IgnoreCase))
                {
                    if (!flags.Contains("error"))
                        flags.Add("error");
                }

                if (Regex.IsMatch(line, @"\btimeout\b", RegexOptions.IgnoreCase))
                {
                    if (!flags.Contains("timeout"))
                        flags.Add("timeout");
                }

                if (Regex.IsMatch(line, @"\babandoned\b", RegexOptions.IgnoreCase))
                {
                    if (!flags.Contains("abandoned"))
                        flags.Add("abandoned");
                }

                if (Regex.IsMatch(line, @"\bdefunct\b", RegexOptions.IgnoreCase))
                {
                    if (!flags.Contains("defunct"))
                        flags.Add("defunct");
                }

                if (Regex.IsMatch(line, @"\bfailed\b", RegexOptions.IgnoreCase))
                {
                    if (!flags.Contains("failed"))
                        flags.Add("failed");
                }
            }

            return flags;
        }

        private string DetermineStatus(MrpRunMetadata metadata, List<string> lines)
        {
            // If we have health flags indicating problems, mark as failed
            if (metadata.HealthFlags.Contains("error") || 
                metadata.HealthFlags.Contains("failed") ||
                metadata.HealthFlags.Contains("abandoned"))
            {
                return "failed";
            }

            // If we have start time but no end time, it's incomplete
            if (metadata.StartTime.HasValue && !metadata.EndTime.HasValue)
            {
                return "incomplete";
            }

            // Look for success indicators in lines
            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"completed successfully", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"process.*complete", RegexOptions.IgnoreCase))
                {
                    return "success";
                }
            }

            // If we have both start and end times and no errors, assume success
            if (metadata.StartTime.HasValue && metadata.EndTime.HasValue && metadata.HealthFlags.Count == 0)
            {
                return "success";
            }

            return "uncertain";
        }

        private DateTime? TryParseTimeWithDate(List<string> lines, string timeStr)
        {
            // Try to find a date in the first few lines
            foreach (var line in lines.Take(DateSearchLineLimit))
            {
                var match = Regex.Match(line, @"([A-Za-z]+,\s+[A-Za-z]+\s+\d{1,2},\s+\d{4})");
                if (match.Success)
                {
                    var dateStr = match.Groups[1].Value;
                    var combinedStr = $"{dateStr} {timeStr}";
                    if (DateTime.TryParse(combinedStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private DateTime? ExtractContextualDate(List<string> lines)
        {
            // Look for dates in "Date: M/D/YYYY" format within the log content
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"Date:\s*(\d{1,2}/\d{1,2}/\d{4})");
                if (match.Success)
                {
                    if (DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        return date;
                    }
                }
            }

            return null;
        }
    }
}
