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

        // Compare jobs
        var jobsA = runA.Entries.Where(e => !string.IsNullOrEmpty(e.JobNumber))
            .Select(e => e.JobNumber).Distinct().ToHashSet();
        var jobsB = runB.Entries.Where(e => !string.IsNullOrEmpty(e.JobNumber))
            .Select(e => e.JobNumber).Distinct().ToHashSet();

        // Jobs added in B
        foreach (var job in jobsB.Except(jobsA))
        {
            comparison.Differences.Add(new MrpDifference
            {
                Type = DifferenceType.JobAdded,
                Severity = Severity.Warning,
                JobNumber = job,
                Description = $"Job {job} appears in Run B but not in Run A"
            });
        }

        // Jobs removed in B
        foreach (var job in jobsA.Except(jobsB))
        {
            comparison.Differences.Add(new MrpDifference
            {
                Type = DifferenceType.JobRemoved,
                Severity = Severity.Warning,
                JobNumber = job,
                Description = $"Job {job} appears in Run A but not in Run B"
            });
        }

        // Compare errors
        var errorsA = runA.Entries.Where(e => e.EntryType == MrpLogEntryType.Error).ToList();
        var errorsB = runB.Entries.Where(e => e.EntryType == MrpLogEntryType.Error).ToList();

        if (errorsB.Count > errorsA.Count)
        {
            comparison.Differences.Add(new MrpDifference
            {
                Type = DifferenceType.ErrorAppeared,
                Severity = Severity.Critical,
                Description = $"New errors appeared in Run B: {errorsB.Count - errorsA.Count} additional errors"
            });
        }
        else if (errorsA.Count > errorsB.Count)
        {
            comparison.Differences.Add(new MrpDifference
            {
                Type = DifferenceType.ErrorResolved,
                Severity = Severity.Info,
                Description = $"Errors resolved in Run B: {errorsA.Count - errorsB.Count} fewer errors"
            });
        }

        // Update summary
        comparison.Summary.TotalDifferences = comparison.Differences.Count;
        comparison.Summary.CriticalCount = comparison.Differences.Count(d => d.Severity == Severity.Critical);
        comparison.Summary.WarningCount = comparison.Differences.Count(d => d.Severity == Severity.Warning);
        comparison.Summary.InfoCount = comparison.Differences.Count(d => d.Severity == Severity.Info);

        return comparison;
    }
}
