using CrystalGroupHome.SharedRCL.Analysis;
using CrystalGroupHome.SharedRCL.Core;
using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.Tests.Analysis;

public class ExplanationEngineTests
{
    [Fact]
    public void GenerateExplanations_EmptyComparison_ReturnsEmptyList()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison();
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.NotNull(explanations);
        Assert.Empty(explanations);
    }
    
    [Fact]
    public void ExplainJobDisappearance_WithTimeoutError_GeneratesCorrectExplanation()
    {
        // Arrange
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
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Equal("Job 14567 disappeared between runs", explanation.Summary);
        Assert.Equal(3, explanation.Facts.Count);
        Assert.Contains(explanation.Facts, f => f.Statement.Contains("Job 14567 present in Run A"));
        Assert.Contains(explanation.Facts, f => f.Statement.Contains("not found in Run B"));
        Assert.Contains(explanation.Facts, f => f.Statement.Contains("Run A logged error"));
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("timeout", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.85, explanation.Inferences[0].ConfidenceLevel);
        Assert.Equal(2, explanation.Inferences[0].SupportingReasons.Count);
        
        Assert.NotEmpty(explanation.NextStepsInEpicor);
        Assert.Contains(explanation.NextStepsInEpicor, s => s.Contains("Job Tracker"));
    }
    
    [Fact]
    public void ExplainJobDisappearance_WithAbandonedError_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument
            {
                Entries = new List<MrpLogEntry>
                {
                    new MrpLogEntry
                    {
                        LineNumber = 50,
                        RawLine = "ERROR: Job 12345 abandoned",
                        EntryType = MrpLogEntryType.Error,
                        JobNumber = "12345",
                        ErrorMessage = "Job 12345 abandoned"
                    }
                }
            },
            RunB = new MrpLogDocument(),
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.JobRemoved,
                    JobNumber = "12345",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 50,
                        RawLine = "ERROR: Job 12345 abandoned",
                        JobNumber = "12345",
                        ErrorMessage = "Job 12345 abandoned"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("manually deleted", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.75, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainJobDisappearance_NoError_GeneratesGenericInference()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument
            {
                Entries = new List<MrpLogEntry>()
            },
            RunB = new MrpLogDocument(),
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.JobRemoved,
                    JobNumber = "99999",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 100,
                        RawLine = "Job 99999 processed",
                        JobNumber = "99999"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("manually deleted", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.60, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainJobAppearance_RegenRun_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument
            {
                Metadata = new MrpRunMetadata { RunType = "regen" },
                Entries = new List<MrpLogEntry>()
            },
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.JobAdded,
                    JobNumber = "55555",
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 200,
                        RawLine = "Job 55555 created",
                        JobNumber = "55555"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Equal("Job 55555 appeared in Run B", explanation.Summary);
        Assert.Equal(2, explanation.Facts.Count);
        Assert.Single(explanation.Inferences);
        Assert.Contains("regeneration", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.80, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainJobAppearance_NetChangeRun_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument
            {
                Metadata = new MrpRunMetadata { RunType = "net change" }
            },
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.JobAdded,
                    JobNumber = "66666",
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 300,
                        RawLine = "Job 66666 created",
                        JobNumber = "66666"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("new demand", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.85, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainDateShift_WithQuantityChange_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument(),
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.DateShifted,
                    JobNumber = "77777",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 400,
                        RawLine = "Job 77777 due 2026-03-01",
                        JobNumber = "77777"
                    },
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 500,
                        RawLine = "Job 77777 due 2026-03-10",
                        JobNumber = "77777"
                    },
                    Details = new Dictionary<string, string>
                    {
                        { "OriginalDate", "2026-03-01" },
                        { "NewDate", "2026-03-10" },
                        { "DaysDifference", "9" }
                    }
                },
                new MrpDifference
                {
                    Type = DifferenceType.QuantityChanged,
                    JobNumber = "77777"
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Equal(2, explanations.Count);
        var dateExplanation = explanations.First(e => e.RelatedDifference.Type == DifferenceType.DateShifted);
        
        Assert.Single(dateExplanation.Inferences);
        Assert.Contains("quantity increase", dateExplanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.80, dateExplanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainDateShift_NoQuantityChange_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.DateShifted,
                    JobNumber = "88888",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 600,
                        RawLine = "Job 88888 due 2026-04-01",
                        JobNumber = "88888"
                    },
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 700,
                        RawLine = "Job 88888 due 2026-04-05",
                        JobNumber = "88888"
                    },
                    Details = new Dictionary<string, string>
                    {
                        { "OriginalDate", "2026-04-01" },
                        { "NewDate", "2026-04-05" },
                        { "DaysDifference", "4" }
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("resource availability", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.65, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainQuantityChange_Increase_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.QuantityChanged,
                    JobNumber = "11111",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 800,
                        RawLine = "Job 11111 qty 100",
                        JobNumber = "11111"
                    },
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 900,
                        RawLine = "Job 11111 qty 150",
                        JobNumber = "11111"
                    },
                    Details = new Dictionary<string, string>
                    {
                        { "OriginalQuantity", "100" },
                        { "NewQuantity", "150" }
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("additional demand", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.80, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainQuantityChange_Decrease_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.QuantityChanged,
                    JobNumber = "22222",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 1000,
                        RawLine = "Job 22222 qty 200",
                        JobNumber = "22222"
                    },
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 1100,
                        RawLine = "Job 22222 qty 50",
                        JobNumber = "22222"
                    },
                    Details = new Dictionary<string, string>
                    {
                        { "OriginalQuantity", "200" },
                        { "NewQuantity", "50" }
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("demand reduction", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.75, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainNewError_Timeout_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.ErrorAppeared,
                    PartNumber = "PART123",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 1200,
                        RawLine = "Part PART123 processed successfully"
                    },
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 1300,
                        RawLine = "ERROR: Part PART123 timeout",
                        EntryType = MrpLogEntryType.Error,
                        ErrorMessage = "Part PART123 timeout"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Equal("New error appeared in Run B for Part PART123", explanation.Summary);
        Assert.Equal(2, explanation.Facts.Count);
        Assert.Single(explanation.Inferences);
        Assert.Contains("network latency", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.75, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainNewError_Exception_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.ErrorAppeared,
                    PartNumber = "PART456",
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 1400,
                        RawLine = "ERROR: Part PART456 cannot be processed - exception",
                        EntryType = MrpLogEntryType.Error,
                        ErrorMessage = "Part PART456 cannot be processed - exception"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("configuration change", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.70, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainErrorResolution_Timeout_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.ErrorResolved,
                    PartNumber = "PART789",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 1500,
                        RawLine = "ERROR: Part PART789 timeout",
                        EntryType = MrpLogEntryType.Error,
                        ErrorMessage = "Part PART789 timeout"
                    },
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 1600,
                        RawLine = "Part PART789 processed successfully"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Equal("Error resolved in Run B for Part PART789", explanation.Summary);
        Assert.Single(explanation.Inferences);
        Assert.Contains("improved system performance", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.70, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainErrorResolution_DataError_GeneratesCorrectExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.ErrorResolved,
                    PartNumber = "PART999",
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 1700,
                        RawLine = "ERROR: Part PART999 cannot be processed",
                        EntryType = MrpLogEntryType.Error,
                        ErrorMessage = "Part PART999 cannot be processed"
                    },
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 1800,
                        RawLine = "Part PART999 processed successfully"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Single(explanation.Inferences);
        Assert.Contains("data correction", explanation.Inferences[0].Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.80, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void ExplainGeneric_GeneratesBasicExplanation()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            Differences = new List<MrpDifference>
            {
                new MrpDifference
                {
                    Type = DifferenceType.Other,
                    RunAEntry = new MrpLogEntry
                    {
                        LineNumber = 1900,
                        RawLine = "Some entry in Run A"
                    },
                    RunBEntry = new MrpLogEntry
                    {
                        LineNumber = 2000,
                        RawLine = "Some entry in Run B"
                    }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Single(explanations);
        var explanation = explanations[0];
        
        Assert.Equal("Difference detected: Other", explanation.Summary);
        Assert.Equal(2, explanation.Facts.Count);
        Assert.Single(explanation.Inferences);
        Assert.Equal(0.50, explanation.Inferences[0].ConfidenceLevel);
    }
    
    [Fact]
    public void GenerateExplanations_AllDifferenceTypes_GeneratesAllExplanations()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument
            {
                Metadata = new MrpRunMetadata { RunType = "regen" }
            },
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
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        Assert.Equal(6, explanations.Count);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.JobRemoved);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.JobAdded);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.DateShifted);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.QuantityChanged);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.ErrorAppeared);
        Assert.Contains(explanations, e => e.RelatedDifference.Type == DifferenceType.ErrorResolved);
        
        // Verify all have at least one fact and one inference
        foreach (var explanation in explanations)
        {
            Assert.NotEmpty(explanation.Facts);
            Assert.NotEmpty(explanation.Inferences);
            Assert.NotEmpty(explanation.NextStepsInEpicor);
            Assert.NotEmpty(explanation.Summary);
        }
    }
    
    [Fact]
    public void AllInferences_HaveConfidenceLevelInValidRange()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument { Metadata = new MrpRunMetadata { RunType = "regen" } },
            Differences = new List<MrpDifference>
            {
                new MrpDifference { Type = DifferenceType.JobRemoved, JobNumber = "J1", RunAEntry = new MrpLogEntry { LineNumber = 1, RawLine = "Job" } },
                new MrpDifference { Type = DifferenceType.JobAdded, JobNumber = "J2", RunBEntry = new MrpLogEntry { LineNumber = 2, RawLine = "Job" } },
                new MrpDifference { Type = DifferenceType.DateShifted, JobNumber = "J3",
                    RunAEntry = new MrpLogEntry { LineNumber = 3, RawLine = "Job" },
                    RunBEntry = new MrpLogEntry { LineNumber = 4, RawLine = "Job" },
                    Details = new Dictionary<string, string> { { "OriginalDate", "2026-01-01" }, { "NewDate", "2026-01-10" }, { "DaysDifference", "9" } }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        foreach (var explanation in explanations)
        {
            foreach (var inference in explanation.Inferences)
            {
                Assert.True(inference.ConfidenceLevel >= 0.0 && inference.ConfidenceLevel <= 1.0, 
                    $"Confidence level {inference.ConfidenceLevel} is outside valid range [0.0, 1.0]");
                Assert.True(inference.ConfidenceLevel >= 0.5, 
                    $"Confidence level {inference.ConfidenceLevel} is below expected minimum of 0.5");
            }
        }
    }
    
    [Fact]
    public void AllExplanations_HaveRequiredComponents()
    {
        // Arrange
        var engine = new ExplanationEngine();
        var comparison = new MrpLogComparison
        {
            RunA = new MrpLogDocument(),
            RunB = new MrpLogDocument { Metadata = new MrpRunMetadata { RunType = "net change" } },
            Differences = new List<MrpDifference>
            {
                new MrpDifference { Type = DifferenceType.QuantityChanged, JobNumber = "J1",
                    RunAEntry = new MrpLogEntry { LineNumber = 1, RawLine = "Qty 100" },
                    RunBEntry = new MrpLogEntry { LineNumber = 2, RawLine = "Qty 150" },
                    Details = new Dictionary<string, string> { { "OriginalQuantity", "100" }, { "NewQuantity", "150" } }
                },
                new MrpDifference { Type = DifferenceType.ErrorAppeared, PartNumber = "P1",
                    RunBEntry = new MrpLogEntry { LineNumber = 3, RawLine = "Error", ErrorMessage = "timeout error" }
                }
            }
        };
        
        // Act
        var explanations = engine.GenerateExplanations(comparison);
        
        // Assert
        foreach (var explanation in explanations)
        {
            Assert.NotNull(explanation.RelatedDifference);
            Assert.False(string.IsNullOrWhiteSpace(explanation.Summary));
            Assert.NotEmpty(explanation.Facts);
            Assert.NotEmpty(explanation.Inferences);
            Assert.NotEmpty(explanation.NextStepsInEpicor);
            
            // Verify facts have required components
            foreach (var fact in explanation.Facts)
            {
                Assert.False(string.IsNullOrWhiteSpace(fact.Statement));
                Assert.NotNull(fact.LogEvidence);
            }
            
            // Verify inferences have required components
            foreach (var inference in explanation.Inferences)
            {
                Assert.False(string.IsNullOrWhiteSpace(inference.Statement));
                Assert.NotEmpty(inference.SupportingReasons);
            }
        }
    }
}
