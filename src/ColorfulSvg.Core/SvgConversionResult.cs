namespace ColorfulSvg.Core;

public sealed class SvgConversionResult
{
    public required string Xaml { get; init; }

    public required IReadOnlyList<SvgResourceEntry> Resources { get; init; }

    public required IReadOnlyList<SvgConversionIssue> Issues { get; init; }

    public bool HasErrors => Issues.Any(issue => issue.IsError);
}
