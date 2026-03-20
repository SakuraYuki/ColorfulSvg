using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using ColorfulSvg.Core;

namespace ColorfulSvg.IconSearcher;

internal sealed class IconExportService
{
    private readonly SvgResourceConverter _converter = new();

    public ExportSummary Export(
        IReadOnlyList<IconExportItem> items,
        string outputPath,
        ExportMode exportMode,
        Func<ResourceConflict, ConflictResolution> conflictResolver)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(conflictResolver);

        var dictionary = exportMode == ExportMode.AppendOrCreate && File.Exists(outputPath)
            ? LoadDictionary(File.ReadAllText(outputPath))
            : new ResourceDictionary();

        var overwriteAll = false;
        var skipAll = false;
        var added = 0;
        var overwritten = 0;
        var skipped = 0;
        var failed = 0;
        var issues = new List<string>();

        foreach (var item in items)
        {
            try
            {
                var conversion = _converter.ConvertContent(item.SvgContent, item.ResourceKey);
                if (conversion.HasErrors || conversion.Resources.Count == 0)
                {
                    failed++;
                    issues.Add($"{item.ResourceKey}: {conversion.Issues.FirstOrDefault()?.Message ?? "转换失败。"}");
                    continue;
                }

                var image = ExtractSingleImage(conversion.Xaml);
                if (dictionary.Contains(item.ResourceKey))
                {
                    var resolution = skipAll
                        ? ConflictResolution.Skip
                        : overwriteAll
                            ? ConflictResolution.Overwrite
                            : conflictResolver(new ResourceConflict(item.ResourceKey, item.SourceId));

                    switch (resolution)
                    {
                        case ConflictResolution.OverwriteAll:
                            overwriteAll = true;
                            dictionary.Remove(item.ResourceKey);
                            dictionary.Add(item.ResourceKey, image);
                            overwritten++;
                            break;
                        case ConflictResolution.Overwrite:
                            dictionary.Remove(item.ResourceKey);
                            dictionary.Add(item.ResourceKey, image);
                            overwritten++;
                            break;
                        case ConflictResolution.SkipAll:
                            skipAll = true;
                            skipped++;
                            break;
                        case ConflictResolution.Skip:
                            skipped++;
                            break;
                        case ConflictResolution.Cancel:
                            return new ExportSummary(false, true, added, overwritten, skipped, failed, issues);
                        default:
                            throw new InvalidOperationException($"Unsupported conflict resolution '{resolution}'.");
                    }

                    continue;
                }

                dictionary.Add(item.ResourceKey, image);
                added++;
            }
            catch (Exception ex)
            {
                failed++;
                issues.Add($"{item.ResourceKey}: {ex.Message}");
            }
        }

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, SerializeDictionary(dictionary));
        return new ExportSummary(true, false, added, overwritten, skipped, failed, issues);
    }

    public DrawingImage CreatePreview(string svgContent, string resourceKey)
    {
        var conversion = _converter.ConvertContent(svgContent, resourceKey);
        if (conversion.HasErrors || conversion.Resources.Count == 0)
        {
            throw new InvalidOperationException(conversion.Issues.FirstOrDefault()?.Message ?? "预览转换失败。");
        }

        return ExtractSingleImage(conversion.Xaml);
    }

    private static DrawingImage ExtractSingleImage(string xaml)
    {
        var dictionary = LoadDictionary(xaml);
        var image = dictionary.Values.OfType<DrawingImage>().FirstOrDefault();
        return image ?? throw new InvalidOperationException("未找到任何 DrawingImage 资源。");
    }

    private static ResourceDictionary LoadDictionary(string xaml)
    {
        return (ResourceDictionary)XamlReader.Parse(xaml);
    }

    private static string SerializeDictionary(ResourceDictionary dictionary)
    {
        return XamlWriter.Save(dictionary)
            .Replace("  <ResourceDictionary.Entries>\r\n", string.Empty, StringComparison.Ordinal)
            .Replace("  </ResourceDictionary.Entries>\r\n", string.Empty, StringComparison.Ordinal)
            .Replace("  <ResourceDictionary.Entries>\n", string.Empty, StringComparison.Ordinal)
            .Replace("  </ResourceDictionary.Entries>\n", string.Empty, StringComparison.Ordinal);
    }
}

internal sealed record IconExportItem(string ResourceKey, string SourceId, string SvgContent);

public sealed record ResourceConflict(string ResourceKey, string SourceId);

internal sealed record ExportSummary(
    bool Saved,
    bool WasCanceled,
    int Added,
    int Overwritten,
    int Skipped,
    int Failed,
    IReadOnlyList<string> Issues);

internal enum ExportMode
{
    CreateNewFile,
    AppendOrCreate
}

public enum ConflictResolution
{
    Overwrite,
    Skip,
    OverwriteAll,
    SkipAll,
    Cancel
}
