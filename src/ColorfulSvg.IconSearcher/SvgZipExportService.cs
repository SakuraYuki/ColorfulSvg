using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorfulSvg.IconSearcher;

internal sealed class SvgZipExportService
{
    private static readonly char[] DirectorySeparators = ['/', '\\'];
    private static readonly char[] InvalidEntryNameChars = BuildInvalidEntryNameChars();

    public SvgZipExportSummary Export(IReadOnlyList<SvgZipExportItem> items, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var usedEntryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var renamedCount = 0;

        using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var item in items)
        {
            var entryPath = BuildUniqueEntryPath(item.ResourceKey, item.SourceId, usedEntryPaths, ref renamedCount);
            var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(item.SvgContent);
        }

        return new SvgZipExportSummary(items.Count, renamedCount, fullPath);
    }

    private static string BuildUniqueEntryPath(
        string? resourceKey,
        string sourceId,
        ISet<string> usedEntryPaths,
        ref int renamedCount)
    {
        var entryPath = BuildEntryPath(resourceKey, sourceId);
        if (usedEntryPaths.Add(entryPath))
        {
            return entryPath;
        }

        var directory = Path.GetDirectoryName(entryPath)?.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(entryPath);
        var extension = Path.GetExtension(entryPath);
        var suffix = 2;

        while (true)
        {
            var candidateFileName = $"{fileName}-{suffix}{extension}";
            var candidatePath = string.IsNullOrWhiteSpace(directory)
                ? candidateFileName
                : $"{directory}/{candidateFileName}";

            if (usedEntryPaths.Add(candidatePath))
            {
                renamedCount++;
                return candidatePath;
            }

            suffix++;
        }
    }

    private static string BuildEntryPath(string? resourceKey, string sourceId)
    {
        var segments = (resourceKey ?? string.Empty)
            .Split(DirectorySeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizePathSegment)
            .Where(static segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count == 0)
        {
            return EnsureSvgExtension(SanitizeFlatFileName(sourceId));
        }

        segments[^1] = EnsureSvgExtension(segments[^1]);
        return string.Join("/", segments);
    }

    private static string SanitizeFlatFileName(string sourceId)
    {
        var sanitized = sourceId
            .Replace('/', '_')
            .Replace('\\', '_');

        sanitized = SanitizePathSegment(sanitized);
        return string.IsNullOrWhiteSpace(sanitized) ? "icon" : sanitized;
    }

    private static string SanitizePathSegment(string segment)
    {
        var sanitized = segment.Trim();
        foreach (var invalidChar in InvalidEntryNameChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        sanitized = sanitized.TrimEnd(' ', '.');
        if (sanitized is "." or "..")
        {
            sanitized = sanitized.Replace('.', '_');
        }

        return sanitized;
    }

    private static string EnsureSvgExtension(string fileName)
    {
        return fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.svg";
    }

    private static char[] BuildInvalidEntryNameChars()
    {
        return Path.GetInvalidFileNameChars()
            .Concat([':'])
            .Distinct()
            .ToArray();
    }
}

internal sealed record SvgZipExportItem(string ResourceKey, string SourceId, string SvgContent);

internal sealed record SvgZipExportSummary(int EntryCount, int RenamedCount, string OutputPath);
