using System.IO;
using System.Windows.Media;

namespace ColorfulSvg.IconSearcher;

internal static class SvgDirectoryIconReader
{
    public static SvgDirectoryLoadResult Load(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var fullPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("未找到指定的 SVG 目录。");
        }

        var svgFiles = Directory
            .EnumerateFiles(fullPath, "*.svg", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return LoadCore(svgFiles, fullPath);
    }

    public static SvgDirectoryLoadResult LoadFiles(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var normalizedFilePaths = filePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedFilePaths.Length == 0)
        {
            throw new InvalidOperationException("未选择任何 SVG 文件。");
        }

        foreach (var filePath in normalizedFilePaths)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("未找到指定的 SVG 文件。", filePath);
            }
        }

        return LoadCore(normalizedFilePaths, GetCommonBaseDirectory(normalizedFilePaths));
    }

    private static SvgDirectoryLoadResult LoadCore(IReadOnlyList<string> svgFiles, string? basePath)
    {
        var exportService = new IconExportService();
        var items = new List<SvgDirectoryIconEntry>(svgFiles.Count);
        var skippedFileCount = 0;

        foreach (var filePath in svgFiles)
        {
            try
            {
                var svgContent = File.ReadAllText(filePath);
                var relativePath = BuildDisplayPath(basePath, filePath);
                var relativeDirectory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
                var preview = exportService.CreatePreview(svgContent, BuildPreviewKey(relativePath));
                if (preview.CanFreeze)
                {
                    preview.Freeze();
                }

                items.Add(new SvgDirectoryIconEntry(
                    Path.GetFileName(filePath),
                    Path.GetFileNameWithoutExtension(filePath),
                    relativePath,
                    string.IsNullOrWhiteSpace(relativeDirectory) ? "根目录" : relativeDirectory,
                    Path.GetFullPath(filePath),
                    preview));
            }
            catch
            {
                skippedFileCount++;
            }
        }

        return new SvgDirectoryLoadResult(items, svgFiles.Count, skippedFileCount);
    }

    private static string BuildDisplayPath(string? basePath, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!string.IsNullOrWhiteSpace(basePath))
        {
            return Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
        }

        return fullPath.Replace('\\', '/');
    }

    private static string BuildPreviewKey(string relativePath)
    {
        var withoutExtension = Path.ChangeExtension(relativePath, null);
        return string.IsNullOrWhiteSpace(withoutExtension)
            ? "icon"
            : withoutExtension.Replace('\\', '/').Replace(':', '_');
    }

    private static string? GetCommonBaseDirectory(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
        {
            return null;
        }

        var rootPath = Path.GetPathRoot(filePaths[0]);
        if (string.IsNullOrWhiteSpace(rootPath) ||
            filePaths.Any(path => !string.Equals(Path.GetPathRoot(path), rootPath, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var commonDirectory = Path.GetDirectoryName(filePaths[0]) ?? rootPath;
        while (!string.IsNullOrWhiteSpace(commonDirectory))
        {
            if (filePaths.All(path => IsFileUnderDirectory(path, commonDirectory)))
            {
                return commonDirectory;
            }

            if (string.Equals(commonDirectory, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                return rootPath;
            }

            commonDirectory = Path.GetDirectoryName(commonDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(commonDirectory))
            {
                return rootPath;
            }
        }

        return rootPath;
    }

    private static bool IsFileUnderDirectory(string filePath, string directoryPath)
    {
        var normalizedDirectoryPath = directoryPath.EndsWith(Path.DirectorySeparatorChar) || directoryPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? directoryPath
            : $"{directoryPath}{Path.DirectorySeparatorChar}";

        return filePath.StartsWith(normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record SvgDirectoryLoadResult(
    IReadOnlyList<SvgDirectoryIconEntry> Items,
    int TotalSvgFileCount,
    int SkippedFileCount);

internal sealed record SvgDirectoryIconEntry(
    string FileName,
    string DisplayName,
    string RelativePath,
    string RelativeDirectory,
    string FilePath,
    DrawingImage Image);
