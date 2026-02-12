namespace MRP.Assistant.Reporting;

public class ReportOptions
{
    public bool IncludeEvidence { get; set; } = true;
    public bool IncludeInferences { get; set; } = true;
    public int MaxEvidenceLines { get; set; } = 10;
}
