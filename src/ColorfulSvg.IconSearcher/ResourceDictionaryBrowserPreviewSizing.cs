namespace ColorfulSvg.IconSearcher;

internal static class ResourceDictionaryBrowserPreviewSizing
{
    public const double MinIconPreviewSize = 24d;
    public const double MaxIconPreviewSize = 160d;
    public const double DefaultIconPreviewSize = 72d;

    public static double GetCardWidth(double iconPreviewSize)
    {
        return Math.Max(156d, iconPreviewSize + 56d);
    }
}
