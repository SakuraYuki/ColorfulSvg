namespace ColorfulSvg.Core;

public sealed record SvgConversionIssue(string Scope, string Message, bool IsError = true);
