namespace ColorfulSvg.Core;

public sealed class SvgConversionOptions
{
    public string? BaseDirectory { get; init; }

    public bool ContinueOnError { get; init; } = true;

    public bool TextAsGeometry { get; init; } = true;

    public bool OptimizePath { get; init; } = true;

    public bool EnsureViewboxSize { get; init; } = true;
}
