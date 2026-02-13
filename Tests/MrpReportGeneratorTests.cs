using System;
using System.Collections.Generic;
using Xunit;
using CrystalGroupHome.SharedRCL.Analysis;
using CrystalGroupHome.SharedRCL.Models;
using CrystalGroupHome.SharedRCL.Reporting;

namespace MRP.Assistant.Tests
{
    public class MrpReportGeneratorTests
    {
        [Fact]
        public void GenerateReport_Markdown_ProducesValidOutput()
        {
            // Arrange
            var comparison = CreateSampleComparison();
            var explanations = CreateSampleExplanations();
            var generator = new MrpReportGenerator();
            var options = new ReportOptions
            {
                Format = ReportFormat.Markdown,
                MaxDifferencesToShow = 10,
                IncludeRawLogExcerpts = true
            };

            // Act
            var report = generator.GenerateReport(comparison, explanations, options);

            // Assert
            Assert.NotNull(report);
            Assert.Contains("# MRP Log Comparison Report", report);
            Assert.Contains("## A) RUN SUMMARY", report);
            Assert.Contains("## B) WHAT CHANGED", report);
            Assert.Contains("## C) MOST LIKELY WHY", report);
            Assert.Contains("## D) LOG EVIDENCE", report);
            Assert.Contains("## E) NEXT CHECKS IN EPICOR", report);
        }

        [Fact]
        public void GenerateReport_PlainText_ProducesValidOutput()
        {
            // Arrange
            var comparison = CreateSampleComparison();
            var explanations = CreateSampleExplanations();
            var generator = new MrpReportGenerator();
            var options = new ReportOptions
            {
                Format = ReportFormat.PlainText,
                MaxDifferencesToShow = 10,
                IncludeRawLogExcerpts = true
            };

            // Act
            var report = generator.GenerateReport(comparison, explanations, options);

            // Assert
            Assert.NotNull(report);
            Assert.Contains("MRP LOG COMPARISON REPORT", report);
            Assert.Contains("A) RUN SUMMARY", report);
            Assert.Contains("B) WHAT CHANGED", report);
            Assert.Contains("C) MOST LIKELY WHY", report);
            Assert.Contains("D) LOG EVIDENCE", report);
            Assert.Contains("E) NEXT CHECKS IN EPICOR", report);
        }

        [Fact]
        public void GenerateReport_HandlesEmptyDifferences()
        {
            // Arrange
            var comparison = new MrpLogComparison
            {
                RunA = new MrpLogDocument { SourceFile = "test_a.txt", RunType = "Regeneration" },
                RunB = new MrpLogDocument { SourceFile = "test_b.txt", RunType = "Net Change" },
                Differences = new List<Difference>(),
                Summary = new ComparisonSummary { TotalDifferences = 0 }
            };
            var explanations = new List<Explanation>();
            var generator = new MrpReportGenerator();
            var options = new ReportOptions { Format = ReportFormat.Markdown };

            // Act
            var report = generator.GenerateReport(comparison, explanations, options);

            // Assert
            Assert.NotNull(report);
            Assert.Contains("No significant differences found", report);
        }

        [Fact]
        public void GenerateReport_IncludesFactsAndInferences()
        {
            // Arrange
            var comparison = CreateSampleComparison();
            var explanations = CreateSampleExplanations();
            var generator = new MrpReportGenerator();
            var options = new ReportOptions { Format = ReportFormat.Markdown };

            // Act
            var report = generator.GenerateReport(comparison, explanations, options);

            // Assert
            Assert.Contains("**FACTS** (log-supported evidence):", report);
            Assert.Contains("**INFERENCES** (plausible explanations):", report);
            Assert.Contains("✅", report); // Fact icon
            Assert.Contains("🔍", report); // Inference icon
        }

        [Fact]
        public void GenerateReport_IncludesNextSteps()
        {
            // Arrange
            var comparison = CreateSampleComparison();
            var explanations = CreateSampleExplanations();
            var generator = new MrpReportGenerator();
            var options = new ReportOptions { Format = ReportFormat.Markdown };

            // Act
            var report = generator.GenerateReport(comparison, explanations, options);

            // Assert
            Assert.Contains("Check Job Tracker", report);
            Assert.Contains("Review System Monitor", report);
        }

        [Fact]
        public void GenerateReport_RespectMaxDifferencesToShow()
        {
            // Arrange
            var comparison = CreateSampleComparison();
            var explanations = CreateSampleExplanations();
            var generator = new MrpReportGenerator();
            var options = new ReportOptions
            {
                Format = ReportFormat.Markdown,
                MaxDifferencesToShow = 1
            };

            // Act
            var report = generator.GenerateReport(comparison, explanations, options);

            // Assert
            Assert.NotNull(report);
            // Should only include first explanation
            Assert.Contains("Job 14567 disappeared", report);
        }

        private MrpLogComparison CreateSampleComparison()
        {
            var runA = new MrpLogDocument
            {
                SourceFile = "mrp_log_sample_A.txt",
                RunType = "Regeneration",
                Site = "PLANT01",
                StartTime = new DateTime(2024, 2, 1, 2, 0, 0),
                EndTime = new DateTime(2024, 2, 1, 3, 15, 0),
                Entries = new List<MrpLogEntry>
                {
                    new MrpLogEntry
                    {
                        LineNumber = 42,
                        EntryType = MrpLogEntryType.Processing,
                        PartNumber = "PART-12345",
                        JobNumber = "14567"
                    },
                    new MrpLogEntry
                    {
                        LineNumber = 58,
                        EntryType = MrpLogEntryType.Error,
                        PartNumber = "PART-12345",
                        JobNumber = "14567"
                    }
                }
            };

            var runB = new MrpLogDocument
            {
                SourceFile = "mrp_log_sample_B.txt",
                RunType = "Net Change",
                Site = "PLANT01",
                StartTime = new DateTime(2024, 2, 2, 2, 0, 0),
                EndTime = new DateTime(2024, 2, 2, 2, 45, 0),
                Entries = new List<MrpLogEntry>
                {
                    new MrpLogEntry
                    {
                        LineNumber = 35,
                        EntryType = MrpLogEntryType.Processing,
                        PartNumber = "PART-67890",
                        JobNumber = "14568"
                    }
                }
            };

            var differences = new List<Difference>
            {
                new Difference
                {
                    Type = DifferenceType.JobRemoved,
                    Severity = DifferenceSeverity.Critical,
                    Description = "Job 14567 present in Run A but missing in Run B",
                    PartNumber = "PART-12345",
                    JobNumber = "14567"
                }
            };

            var summary = new ComparisonSummary
            {
                TotalDifferences = 1,
                CriticalCount = 1,
                JobsRemoved = 1
            };

            return new MrpLogComparison
            {
                RunA = runA,
                RunB = runB,
                Differences = differences,
                Summary = summary
            };
        }

        private List<Explanation> CreateSampleExplanations()
        {
            return new List<Explanation>
            {
                new Explanation
                {
                    Summary = "Job 14567 disappeared between runs",
                    RelatedDifference = new Difference
                    {
                        Type = DifferenceType.JobRemoved,
                        Severity = DifferenceSeverity.Critical
                    },
                    Facts = new List<Fact>
                    {
                        new Fact
                        {
                            Statement = "Job 14567 present in Run A at line 42",
                            LineNumber = 42,
                            LogEvidence = "02:30:00 Processing Part PART-12345 for Job 14567"
                        }
                    },
                    Inferences = new List<Inference>
                    {
                        new Inference
                        {
                            Statement = "Job may have been automatically removed due to timeout",
                            ConfidenceLevel = 0.85,
                            SupportingReasons = new List<string>
                            {
                                "Timeout errors often trigger automatic cleanup"
                            }
                        }
                    },
                    NextStepsInEpicor = new List<string>
                    {
                        "Check Job Tracker for deletion history of Job 14567",
                        "Review System Monitor for timeout configuration"
                    }
                }
            };
        }
    }
}
