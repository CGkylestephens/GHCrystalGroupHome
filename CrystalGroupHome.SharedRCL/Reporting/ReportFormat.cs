namespace CrystalGroupHome.SharedRCL.Reporting
{
    /// <summary>
    /// Report output format options.
    /// </summary>
    public enum ReportFormat
    {
        /// <summary>
        /// Markdown format.
        /// </summary>
        Markdown,

        /// <summary>
        /// Plain text format.
        /// </summary>
        PlainText,

        /// <summary>
        /// HTML format.
        /// </summary>
        Html,

        /// <summary>
        /// JSON format.
        /// </summary>
        Json
    }

    /// <summary>
    /// Options for configuring report generation.
    /// </summary>
    public class ReportOptions
    {
        /// <summary>
        /// Output format for the report.
        /// </summary>
        public ReportFormat Format { get; set; } = ReportFormat.Markdown;

        /// <summary>
        /// Maximum number of differences to show in detail.
        /// </summary>
        public int MaxDifferencesToShow { get; set; } = 10;

        /// <summary>
        /// Whether to include raw log excerpts in the evidence section.
        /// </summary>
        public bool IncludeRawLogExcerpts { get; set; } = true;

        /// <summary>
        /// Whether to group differences by part number.
        /// </summary>
        public bool GroupByPart { get; set; } = false;
    }
}
