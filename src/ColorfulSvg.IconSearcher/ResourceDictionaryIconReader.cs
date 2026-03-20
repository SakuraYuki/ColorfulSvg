using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace ColorfulSvg.IconSearcher;

internal static class ResourceDictionaryIconReader
{
    public static IReadOnlyList<ResourceDictionaryIconEntry> Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("未找到指定的 XAML 资源文件。", fullPath);
        }

        var xaml = File.ReadAllText(fullPath);
        var dictionary = (ResourceDictionary)XamlReader.Parse(xaml);
        var items = new List<ResourceDictionaryIconEntry>();
        CollectIcons(dictionary, items);

        return items
            .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void CollectIcons(ResourceDictionary dictionary, ICollection<ResourceDictionaryIconEntry> items)
    {
        foreach (var key in dictionary.Keys)
        {
            if (key is null)
            {
                continue;
            }

            if (dictionary[key] is DrawingImage drawingImage)
            {
                items.Add(new ResourceDictionaryIconEntry(key.ToString() ?? string.Empty, drawingImage));
            }
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries)
        {
            CollectIcons(mergedDictionary, items);
        }
    }
}

internal sealed record ResourceDictionaryIconEntry(string Key, DrawingImage Image);
