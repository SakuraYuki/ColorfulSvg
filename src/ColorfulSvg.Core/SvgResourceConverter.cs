using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace ColorfulSvg.Core;

public sealed class SvgResourceConverter
{
    public SvgConversionResult ConvertFile(string filePath, string? key = null, SvgConversionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("SVG file was not found.", fullPath);
        }

        var resourceKey = key ?? CreateKeyFromFileName(fullPath);
        var issues = new List<SvgConversionIssue>();
        var dictionary = CreateDictionary();
        var resources = new List<SvgResourceEntry>();

        try
        {
            var drawing = ReadFromFile(fullPath, options);
            AddResource(dictionary, resourceKey, fullPath, drawing);
            resources.Add(new SvgResourceEntry(resourceKey, fullPath));
        }
        catch (Exception ex)
        {
            issues.Add(new SvgConversionIssue(fullPath, ex.Message));
        }

        return CreateResult(dictionary, resources, issues);
    }

    public SvgConversionResult ConvertContent(string svgContent, string key, SvgConversionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svgContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var scope = "inline-svg";
        var issues = new List<SvgConversionIssue>();
        var dictionary = CreateDictionary();
        var resources = new List<SvgResourceEntry>();

        try
        {
            var drawing = ReadFromContent(svgContent, options);
            AddResource(dictionary, key, scope, drawing);
            resources.Add(new SvgResourceEntry(key, scope));
        }
        catch (Exception ex)
        {
            issues.Add(new SvgConversionIssue(scope, ex.Message));
        }

        return CreateResult(dictionary, resources, issues);
    }

    public SvgConversionResult ConvertDirectory(string directoryPath, SvgConversionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var fullPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory was not found: {fullPath}");
        }

        var issues = new List<SvgConversionIssue>();
        var resources = new List<SvgResourceEntry>();
        var dictionary = CreateDictionary();
        var svgFiles = Directory.EnumerateFiles(fullPath, "*.svg", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (svgFiles.Length == 0)
        {
            issues.Add(new SvgConversionIssue(fullPath, "No SVG files were found."));
            return CreateResult(dictionary, resources, issues);
        }

        foreach (var svgFile in svgFiles)
        {
            var key = CreateKeyFromRelativePath(fullPath, svgFile);

            try
            {
                var drawing = ReadFromFile(svgFile, options);
                AddResource(dictionary, key, svgFile, drawing);
                resources.Add(new SvgResourceEntry(key, svgFile));
            }
            catch (Exception ex)
            {
                issues.Add(new SvgConversionIssue(svgFile, ex.Message));
                if (!(options?.ContinueOnError ?? true))
                {
                    break;
                }
            }
        }

        return CreateResult(dictionary, resources, issues);
    }

    public void SaveResult(SvgConversionResult result, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, result.Xaml);
    }

    private static ResourceDictionary CreateDictionary()
    {
        return new ResourceDictionary();
    }

    private static SvgConversionResult CreateResult(
        ResourceDictionary dictionary,
        IReadOnlyList<SvgResourceEntry> resources,
        IReadOnlyList<SvgConversionIssue> issues)
    {
        var xaml = NormalizeResourceDictionaryXaml(XamlWriter.Save(dictionary));

        return new SvgConversionResult
        {
            Xaml = xaml,
            Resources = resources,
            Issues = issues
        };
    }

    private static DrawingGroup ReadFromFile(string filePath, SvgConversionOptions? options)
    {
        var settings = CreateSettings(options);
        using var reader = new FileSvgReader(settings);
        var drawing = reader.Read(filePath);
        return drawing ?? throw new InvalidOperationException("Failed to convert SVG file.");
    }

    private static DrawingGroup ReadFromContent(string svgContent, SvgConversionOptions? options)
    {
        var settings = CreateSettings(options);
        using var reader = new FileSvgReader(settings);
        if (options?.BaseDirectory is { Length: > 0 } path)
        {
            var baseDirectory = Path.GetFullPath(path);
            Directory.CreateDirectory(baseDirectory);

            var tempFilePath = Path.Combine(baseDirectory, $".colorfulsvg-{Guid.NewGuid():N}.svg");
            try
            {
                File.WriteAllText(tempFilePath, svgContent);
                var fileDrawing = reader.Read(tempFilePath);
                return fileDrawing ?? throw new InvalidOperationException("Failed to convert SVG content.");
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        using var textReader = new StringReader(svgContent);
        var drawing = reader.Read(textReader);
        return drawing ?? throw new InvalidOperationException("Failed to convert SVG content.");
    }

    private static WpfDrawingSettings CreateSettings(SvgConversionOptions? options)
    {
        return new WpfDrawingSettings
        {
            IncludeRuntime = false,
            TextAsGeometry = options?.TextAsGeometry ?? true,
            OptimizePath = options?.OptimizePath ?? true,
            EnsureViewboxSize = options?.EnsureViewboxSize ?? true
        };
    }

    private static void AddResource(ResourceDictionary dictionary, string key, string scope, DrawingGroup drawing)
    {
        if (dictionary.Contains(key))
        {
            throw new InvalidOperationException($"Duplicate resource key '{key}' for '{scope}'.");
        }

        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        dictionary.Add(key, image);
    }

    private static string CreateKeyFromFileName(string filePath)
    {
        return SanitizeKey(Path.GetFileNameWithoutExtension(filePath));
    }

    private static string CreateKeyFromRelativePath(string rootPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, filePath);
        var withoutExtension = Path.ChangeExtension(relativePath, null) ?? relativePath;
        return SanitizeKey(withoutExtension.Replace('\\', '/'));
    }

    private static string SanitizeKey(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Resource key cannot be empty.");
        }

        return trimmed.Replace(" ", "_", StringComparison.Ordinal);
    }

    private static string NormalizeResourceDictionaryXaml(string xaml)
    {
        return xaml
            .Replace("  <ResourceDictionary.Entries>\r\n", string.Empty, StringComparison.Ordinal)
            .Replace("  </ResourceDictionary.Entries>\r\n", string.Empty, StringComparison.Ordinal)
            .Replace("  <ResourceDictionary.Entries>\n", string.Empty, StringComparison.Ordinal)
            .Replace("  </ResourceDictionary.Entries>\n", string.Empty, StringComparison.Ordinal);
    }
}
