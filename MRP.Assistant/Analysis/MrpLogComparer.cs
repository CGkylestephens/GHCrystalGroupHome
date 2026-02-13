using MRP.Assistant.Core;

namespace MRP.Assistant.Analysis;

public class MrpLogComparer
{
    public MrpLogComparison Compare(MrpLogDocument runA, MrpLogDocument runB)
    {
        var comparison = new MrpLogComparison
        {
            RunA = runA,
            RunB = runB
        };
        
        // Detect job differences
        var jobsA = runA.Entries.Where(e => !string.IsNullOrEmpty(e.JobNumber))
            .Select(e => e.JobNumber!).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var jobsB = runB.Entries.Where(e => !string.IsNullOrEmpty(e.JobNumber))
            .Select(e => e.JobNumber!).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        foreach (var job in jobsA)
        {
            if (!jobsB.Contains(job))
            {
                var entry = runA.Entries.First(e => e.JobNumber == job);
                comparison.Differences.Add(new MrpDifference
                {
                    Type = DifferenceType.JobRemoved,
                    Severity = DifferenceSeverity.Warning,
                    JobNumber = job,
                    Description = $"Job {job} present in Run A but not in Run B"
                });
            }
        }
        
        foreach (var job in jobsB)
        {
            if (!jobsA.Contains(job))
            {
                comparison.Differences.Add(new MrpDifference
                {
                    Type = DifferenceType.JobAdded,
                    Severity = DifferenceSeverity.Info,
                    JobNumber = job,
                    Description = $"Job {job} present in Run B but not in Run A"
                });
            }
        }
        
        // Detect part differences
        var partsA = runA.Entries.Where(e => !string.IsNullOrEmpty(e.PartNumber))
            .Select(e => e.PartNumber!).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var partsB = runB.Entries.Where(e => !string.IsNullOrEmpty(e.PartNumber))
            .Select(e => e.PartNumber!).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        foreach (var part in partsA)
        {
            if (!partsB.Contains(part))
            {
                comparison.Differences.Add(new MrpDifference
                {
                    Type = DifferenceType.PartRemoved,
                    Severity = DifferenceSeverity.Info,
                    PartNumber = part,
                    Description = $"Part {part} present in Run A but not in Run B"
                });
            }
        }
        
        foreach (var part in partsB)
        {
            if (!partsA.Contains(part))
            {
                comparison.Differences.Add(new MrpDifference
                {
                    Type = DifferenceType.PartAdded,
                    Severity = DifferenceSeverity.Info,
                    PartNumber = part,
                    Description = $"Part {part} present in Run B but not in Run A"
                });
            }
        }
        
        // Update summary
        comparison.Summary.TotalDifferences = comparison.Differences.Count;
        comparison.Summary.CriticalDifferences = comparison.Differences.Count(d => d.Severity == DifferenceSeverity.Critical);
        comparison.Summary.WarningDifferences = comparison.Differences.Count(d => d.Severity == DifferenceSeverity.Warning);
        comparison.Summary.InfoDifferences = comparison.Differences.Count(d => d.Severity == DifferenceSeverity.Info);
        
        return comparison;
    }
}
