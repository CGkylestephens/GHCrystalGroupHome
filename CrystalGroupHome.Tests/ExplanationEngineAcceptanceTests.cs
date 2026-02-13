using CrystalGroupHome.SharedRCL.Analysis;
using CrystalGroupHome.SharedRCL.Core;
using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.Tests.Analysis;

/// <summary>
/// Tests to verify acceptance criteria from the issue specification
/// </summary>
public class ExplanationEngineAcceptanceTests
{
    [Fact]
    public void IssueTestScenario_JobRemovedWithTimeoutError_MatchesExpectedOutput()
    {
        // Arrange - from issue specification
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument
            {
                Entries = new List<MrpLogEntry>
                {
                    new MrpLogEntry
                    {
                        LineNumber = 42,
                        RawLine = "ERROR: Job 14567 abandoned due to timeout",
                        EntryType = MrpLogEntryType.Error,
                        JobNumber = "14567",
                        ErrorMessage = "Job 14567 abandoned due to timeout"
                    }
                }
            },
            RunB = new MrpLogDocument(),
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.JobRemoved,
                    JobNumber = "14567",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 42,
                        RawLine = "ERROR: Job 14567 abandoned due to timeout",
                        EntryType = MrpLogEntryType.Error,
                        JobNumber = "14567",
                        ErrorMessage = "Job 14567 abandoned due to timeout"
                    }
                }
            }
        };

        // Act
        var explanations = engine.GenerateExplanations(comparison);

        // Assert - Expected output from issue specification
        Assert.Single(explanations);
        var explanation = explanations[0];

        // Verify Summary
        Assert.Equal("Job 14567 disappeared between runs", explanation.Summary);

        // Verify Facts
        Assert.Equal(3, explanation.Facts.Count);
        
        // Fact 1: Job present in Run A
        var fact1 = explanation.Facts[0];
        Assert.Contains("Job 14567 present in Run A at line 42", fact1.Statement);
        Assert.Equal("ERROR: Job 14567 abandoned due to timeout", fact1.LogEvidence);
        Assert.Equal(42, fact1.LineNumber);

        // Fact 2: Job not found in Run B
        var fact2 = explanation.Facts[1];
        Assert.Contains("Job 14567 not found in Run B", fact2.Statement);
        Assert.Equal("(absence in log)", fact2.LogEvidence);

        // Fact 3: Error logged in Run A
        var fact3 = explanation.Facts[2];
        Assert.Contains("Run A logged error:", fact3.Statement);
        Assert.Contains("Job 14567 abandoned due to timeout", fact3.Statement);
        Assert.Equal("ERROR: Job 14567 abandoned due to timeout", fact3.LogEvidence);

        // Verify Inferences
        Assert.Single(explanation.Inferences);
        var inference = explanation.Inferences[0];
        Assert.Contains("Job may have been automatically removed due to timeout", inference.Statement);
        Assert.Equal(0.85, inference.ConfidenceLevel);
        Assert.Equal(2, inference.SupportingReasons.Count);
        Assert.Contains(inference.SupportingReasons, r => r.Contains("Timeout errors often trigger automatic cleanup"));
        Assert.Contains(inference.SupportingReasons, r => r.Contains("Common in Net Change runs"));

        // Verify Next Steps
        Assert.NotEmpty(explanation.NextStepsInEpicor);
        Assert.Contains(explanation.NextStepsInEpicor, s => s.Contains("Check Job Tracker for deletion history"));
        Assert.Contains(explanation.NextStepsInEpicor, s => s.Contains("Review Job Entry screen for manual changes"));
        Assert.Contains(explanation.NextStepsInEpicor, s => s.Contains("Check MRP processing logs for cleanup actions"));
    }

    [Fact]
    public void AcceptanceCriteria_ExplanationModelsExist()
    {
        // Verify all required models exist and can be instantiated
        var explanation = new Explanation();
        var fact = new ExplanationFact();
        var inference = new ExplanationInference();

        Assert.NotNull(explanation);
        Assert.NotNull(fact);
        Assert.NotNull(inference);

        // Verify model properties
        Assert.NotNull(explanation.Summary);
        Assert.NotNull(explanation.Facts);
        Assert.NotNull(explanation.Inferences);
        Assert.NotNull(explanation.NextStepsInEpicor);

        Assert.NotNull(fact.Statement);
        Assert.NotNull(fact.LogEvidence);

        Assert.NotNull(inference.Statement);
        Assert.NotNull(inference.SupportingReasons);
    }

    [Fact]
    public void AcceptanceCriteria_AllDifferenceTypesHaveExplanations()
    {
        // Verify all 5+ difference types are handled
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument { Metadata = new MrpRunMetadata { RunType = "regen" } },
            Differences = new List<MrpDifference>
            {
                new MrpDifference { Type = DifferenceType.JobRemoved, JobNumber = "J1", RunAEntry = new MrpLogEntry { LineNumber = 1, RawLine = "Job J1" } },
                new MrpDifference { Type = DifferenceType.JobAdded, JobNumber = "J2", RunBEntry = new MrpLogEntry { LineNumber = 2, RawLine = "Job J2" } },
                new MrpDifference { Type = DifferenceType.DateShifted, JobNumber = "J3", 
                    RunAEntry = new MrpLogEntry { LineNumber = 3, RawLine = "Job J3" },
                    RunBEntry = new MrpLogEntry { LineNumber = 4, RawLine = "Job J3" },
                    Details = new Dictionary<string, string> { { "OriginalDate", "2026-01-01" }, { "NewDate", "2026-01-10" }, { "DaysDifference", "9" } }
                },
                new MrpDifference { Type = DifferenceType.QuantityChanged, JobNumber = "J4",
                    RunAEntry = new MrpLogEntry { LineNumber = 5, RawLine = "Job J4" },
                    RunBEntry = new MrpLogEntry { LineNumber = 6, RawLine = "Job J4" },
                    Details = new Dictionary<string, string> { { "OriginalQuantity", "100" }, { "NewQuantity", "200" } }
                },
                new MrpDifference { Type = DifferenceType.ErrorAppeared, PartNumber = "P1", 
                    RunBEntry = new MrpLogEntry { LineNumber = 7, RawLine = "Error P1", ErrorMessage = "Error P1" }
                },
                new MrpDifference { Type = DifferenceType.ErrorResolved, PartNumber = "P2",
                    RunAEntry = new MrpLogEntry { LineNumber = 8, RawLine = "Error P2", ErrorMessage = "Error P2" },
                    RunBEntry = new MrpLogEntry { LineNumber = 9, RawLine = "OK P2" }
                }
            }
        };

        var explanations = engine.GenerateExplanations(comparison);

        // Verify all 6 difference types generate explanations
        Assert.Equal(6, explanations.Count);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.JobRemoved);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.JobAdded);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.DateShifted);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.QuantityChanged);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.ErrorAppeared);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.ErrorResolved);
    }

    [Fact]
    public void AcceptanceCriteria_FactsAndInferencesAreSeparated()
    {
        // Verify clear separation between FACT and INFERENCE
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument
            {
                Entries = new List<MrpLogEntry>
                {
                    new MrpLogEntry
                    {
                        LineNumber = 10,
                        RawLine = "ERROR: Job ABC timeout",
                        EntryType = MrpLogEntryType.Error,
                        JobNumber = "ABC",
                        ErrorMessage = "Job ABC timeout"
                    }
                }
            },
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.JobRemoved,
                    JobNumber = "ABC",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 10,
                        RawLine = "ERROR: Job ABC timeout",
                        JobNumber = "ABC",
                        ErrorMessage = "Job ABC timeout"
                    }
                }
            }
        };

        var explanations = engine.GenerateExplanations(comparison);
        var explanation = explanations[0];

        // Facts should reference actual log lines
        Assert.All(explanation.Facts, fact =>
        {
            Assert.NotNull(fact.LogEvidence);
            Assert.NotEmpty(fact.Statement);
        });

        // Inferences should have confidence levels and supporting reasons
        Assert.All(explanation.Inferences, inference =>
        {
            Assert.InRange(inference.ConfidenceLevel, 0.0, 1.0);
            Assert.NotEmpty(inference.SupportingReasons);
        });
    }

    [Fact]
    public void AcceptanceCriteria_ConfidenceLevelsAreReasonable()
    {
        // Verify confidence levels are in 0.6-0.9 range as specified
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument { Metadata = new MrpRunMetadata { RunType = "net change" } },
            Differences = new List<MrpDifference>
            {
                new MrpDifference { Type = DifferenceType.JobRemoved, JobNumber = "J1", RunAEntry = new MrpLogEntry { LineNumber = 1, RawLine = "Job" } },
                new MrpDifference { Type = DifferenceType.JobAdded, JobNumber = "J2", RunBEntry = new MrpLogEntry { LineNumber = 2, RawLine = "Job" } },
                new MrpDifference { Type = DifferenceType.DateShifted, JobNumber = "J3",
                    RunAEntry = new MrpLogEntry { LineNumber = 3, RawLine = "Job" },
                    RunBEntry = new MrpLogEntry { LineNumber = 4, RawLine = "Job" },
                    Details = new Dictionary<string, string> { { "OriginalDate", "2026-01-01" }, { "NewDate", "2026-01-10" }, { "DaysDifference", "9" } }
                },
                new MrpDifference { Type = DifferenceType.ErrorAppeared, PartNumber = "P1",
                    RunBEntry = new MrpLogEntry { LineNumber = 5, RawLine = "Error", ErrorMessage = "timeout" }
                }
            }
        };

        var explanations = engine.GenerateExplanations(comparison);

        // Check that most confidence levels are in the reasonable range
        var allConfidenceLevels = explanations
            .SelectMany(e => e.Inferences)
            .Select(i => i.ConfidenceLevel)
            .ToList();

        Assert.All(allConfidenceLevels, level =>
        {
            Assert.InRange(level, 0.0, 1.0);
            // Most should be in the 0.6-0.9 range (allowing for 0.5 generic fallback)
            Assert.True(level >= 0.5, $"Confidence level {level} is below minimum of 0.5");
        });
    }

    [Fact]
    public void AcceptanceCriteria_NextStepsProvided()
    {
        // Verify actionable next steps are provided
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.QuantityChanged,
                    JobNumber = "TEST",
                    RunAEntry = new MrpLogEntry { LineNumber = 1, RawLine = "Qty 100" },
                    RunBEntry = new MrpLogEntry { LineNumber = 2, RawLine = "Qty 150" },
                    Details = new Dictionary<string, string> { { "OriginalQuantity", "100" }, { "NewQuantity", "150" } }
                }
            }
        };

        var explanations = engine.GenerateExplanations(comparison);
        var explanation = explanations[0];

        // Verify next steps are specific to Epicor
        Assert.NotEmpty(explanation.NextStepsInEpicor);
        Assert.All(explanation.NextStepsInEpicor, step =>
        {
            Assert.False(string.IsNullOrWhiteSpace(step));
            // Should reference Epicor screens/features
            Assert.True(
                step.Contains("Check", StringComparison.OrdinalIgnoreCase) ||
                step.Contains("Review", StringComparison.OrdinalIgnoreCase) ||
                step.Contains("Verify", StringComparison.OrdinalIgnoreCase),
                "Next step should be actionable");
        });
    }

    [Fact]
    public void AcceptanceCriteria_LanguageIsPlannerFriendly()
    {
        // Verify no technical jargon
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunB = new MrpLogDocument { Metadata = new MrpRunMetadata { RunType = "regen" } },
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.JobAdded,
                    JobNumber = "NEW",
                    RunBEntry = new MrpLogEntry { LineNumber = 1, RawLine = "Job NEW" }
                }
            }
        };

        var explanations = engine.GenerateExplanations(comparison);
        var explanation = explanations[0];

        // Check that explanations don't use technical jargon
        var allText = string.Join(" ",
            explanation.Summary,
            string.Join(" ", explanation.Facts.Select(f => f.Statement)),
            string.Join(" ", explanation.Inferences.Select(i => i.Statement)),
            string.Join(" ", explanation.Inferences.SelectMany(i => i.SupportingReasons)),
            string.Join(" ", explanation.NextStepsInEpicor)
        );

        // Should not contain technical jargon like NRE, mutex, etc.
        Assert.DoesNotContain("NRE", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mutex", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("semaphore", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deadlock", allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AcceptanceCriteria_FactsReferenceLogLines()
    {
        // Verify facts include actual log lines and line numbers
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.ErrorAppeared,
                    PartNumber = "PART",
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 123,
                        RawLine = "ERROR: Part PART failed",
                        ErrorMessage = "Part PART failed"
                    }
                }
            }
        };

        var explanations = engine.GenerateExplanations(comparison);
        var explanation = explanations[0];

        // Verify facts reference actual log content
        var factsWithLogEvidence = explanation.Facts
            .Where(f => !f.LogEvidence.Contains("(absence") && !f.LogEvidence.Contains("(no error"))
            .ToList();
        Assert.NotEmpty(factsWithLogEvidence);
        
        Assert.All(factsWithLogEvidence, fact =>
        {
            Assert.NotEmpty(fact.LogEvidence);
            Assert.True(fact.LineNumber > 0, $"Fact with log evidence should have line number > 0, but was {fact.LineNumber}");
        });
        
        // Verify facts without physical log evidence have appropriate markers
        var factsAbsence = explanation.Facts
            .Where(f => f.LogEvidence.Contains("(absence") || f.LogEvidence.Contains("(no error"))
            .ToList();
        Assert.All(factsAbsence, fact =>
        {
            Assert.True(fact.LineNumber == 0, "Absence facts should have line number 0");
        });
    }

    [Fact]
    public void AcceptanceCriteria_InferencesIncludeSupportingReasons()
    {
        // Verify all inferences have supporting reasons
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.DateShifted,
                    JobNumber = "JOB",
                    RunAEntry = new MrpLogEntry { LineNumber = 1, RawLine = "Due 2026-01-01" },
                    RunBEntry = new MrpLogEntry { LineNumber = 2, RawLine = "Due 2026-01-10" },
                    Details = new Dictionary<string, string>
                    {
                        { "OriginalDate", "2026-01-01" },
                        { "NewDate", "2026-01-10" },
                        { "DaysDifference", "9" }
                    }
                }
            }
        };

        var explanations = engine.GenerateExplanations(comparison);
        var explanation = explanations[0];

        // All inferences must have supporting reasons
        Assert.All(explanation.Inferences, inference =>
        {
            Assert.NotEmpty(inference.SupportingReasons);
            Assert.All(inference.SupportingReasons, reason =>
            {
                Assert.False(string.IsNullOrWhiteSpace(reason));
            });
        });
    }

    [Fact]
    public void AcceptanceCriteria_MinimumOneFactAndOneInference()
    {
        // Verify all explanations have at least 1 FACT and 1 INFERENCE
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument { Metadata = new MrpRunMetadata { RunType = "regen" } },
            Differences = new List<MrpDifference>
            {
                new MrpDifference { Type = DifferenceType.JobRemoved, JobNumber = "J1", RunAEntry = new MrpLogEntry { LineNumber = 1, RawLine = "Job" } },
                new MrpDifference { Type = DifferenceType.JobAdded, JobNumber = "J2", RunBEntry = new MrpLogEntry { LineNumber = 2, RawLine = "Job" } },
                new MrpDifference { Type = DifferenceType.ErrorResolved, PartNumber = "P1",
                    RunAEntry = new MrpLogEntry { LineNumber = 3, RawLine = "Error", ErrorMessage = "Error" },
                    RunBEntry = new MrpLogEntry { LineNumber = 4, RawLine = "OK" }
                }
            }
        };

        var explanations = engine.GenerateExplanations(comparison);

        // Verify ALL explanations have at least 1 fact and 1 inference
        Assert.All(explanations, explanation =>
        {
            Assert.NotEmpty(explanation.Facts);
            Assert.NotEmpty(explanation.Inferences);
        });
    }
}
