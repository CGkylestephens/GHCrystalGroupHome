using System.Text;

namespace CrystalGroupHome.SharedRCL.Tests.Fixtures;

/// <summary>
/// Helper class for building test MRP log content.
/// </summary>
public class SampleLogBuilder
{
    private readonly StringBuilder _content = new();

    public SampleLogBuilder WithHeader(DateTime date)
    {
        _content.AppendLine($"{date:dddd, MMMM d, yyyy HH:mm:ss}");
        return this;
    }

    public SampleLogBuilder WithLine(string line)
    {
        _content.AppendLine(line);
        return this;
    }

    public SampleLogBuilder WithTimestampedLine(TimeSpan time, string content)
    {
        _content.AppendLine($"{time:hh\\:mm\\:ss} {content}");
        return this;
    }

    public SampleLogBuilder WithRegenStart(TimeSpan time)
    {
        _content.AppendLine($"{time:hh\\:mm\\:ss} MRP Regeneration process begin");
        return this;
    }

    public SampleLogBuilder WithNetChangeStart(TimeSpan time)
    {
        _content.AppendLine($"{time:hh\\:mm\\:ss} MRP Net Change process begin");
        return this;
    }

    public SampleLogBuilder WithSite(string siteName)
    {
        _content.AppendLine($"Site List -> {siteName}");
        return this;
    }

    public SampleLogBuilder WithError(TimeSpan time, string errorMessage)
    {
        _content.AppendLine($"{time:hh\\:mm\\:ss} ERROR: {errorMessage}");
        return this;
    }

    public SampleLogBuilder WithTimeout(TimeSpan time, string jobNumber)
    {
        _content.AppendLine($"{time:hh\\:mm\\:ss} Job {jobNumber} abandoned due to timeout");
        return this;
    }

    public SampleLogBuilder WithPegging(TimeSpan time)
    {
        _content.AppendLine($"{time:hh\\:mm\\:ss} Building Pegging Demand Master...");
        return this;
    }

    public SampleLogBuilder WithProcessingPart(TimeSpan time, string partNumber)
    {
        _content.AppendLine($"{time:hh\\:mm\\:ss} Processing Part:{partNumber}, Attribute Set:''");
        return this;
    }

    public SampleLogBuilder WithDate(DateTime date)
    {
        _content.AppendLine($"Date: {date:M/d/yyyy}");
        return this;
    }

    public SampleLogBuilder WithCompletion(TimeSpan time)
    {
        _content.AppendLine($"{time:hh\\:mm\\:ss} MRP process complete");
        return this;
    }

    public string Build()
    {
        return _content.ToString();
    }

    public string[] BuildLines()
    {
        return _content.ToString().Split(Environment.NewLine, StringSplitOptions.None);
    }

    public static SampleLogBuilder CreateRegenLog(DateTime date)
    {
        return new SampleLogBuilder()
            .WithHeader(date)
            .WithRegenStart(new TimeSpan(0, 0, 0))
            .WithSite("MfgSys")
            .WithDate(date)
            .WithPegging(new TimeSpan(0, 0, 46));
    }

    public static SampleLogBuilder CreateNetChangeLog(DateTime date)
    {
        return new SampleLogBuilder()
            .WithHeader(date)
            .WithNetChangeStart(new TimeSpan(0, 0, 0))
            .WithSite("MfgSys")
            .WithDate(date)
            .WithProcessingPart(new TimeSpan(0, 1, 0), "ABC123");
    }
}
