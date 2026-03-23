using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
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
        return CreateItems(dictionary);
    }

    public static ResourceDictionaryDeleteResult Delete(string filePath, IReadOnlyCollection<string> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(keys);

        var normalizedKeys = keys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (normalizedKeys.Count == 0)
        {
            return new ResourceDictionaryDeleteResult(0, Load(filePath));
        }

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("未找到指定的 XAML 资源文件。", fullPath);
        }

        var xaml = File.ReadAllText(fullPath);
        var dictionary = (ResourceDictionary)XamlReader.Parse(xaml);
        var removedCount = RemoveKeys(dictionary, normalizedKeys);
        if (removedCount > 0)
        {
            File.WriteAllText(fullPath, SerializeDictionary(dictionary));
        }

        return new ResourceDictionaryDeleteResult(removedCount, CreateItems(dictionary));
    }

    private static IReadOnlyList<ResourceDictionaryIconEntry> CreateItems(ResourceDictionary dictionary)
    {
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
                items.Add(new ResourceDictionaryIconEntry(
                    key.ToString() ?? string.Empty,
                    drawingImage,
                    DrawingImagePreviewFactory.CreatePreviewImage(drawingImage)));
            }
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries)
        {
            CollectIcons(mergedDictionary, items);
        }
    }

    private static int RemoveKeys(ResourceDictionary dictionary, ISet<string> keys)
    {
        var removedCount = 0;
        var existingKeys = dictionary.Keys.Cast<object?>().ToArray();
        foreach (var key in existingKeys)
        {
            if (key is null)
            {
                continue;
            }

            var keyText = key.ToString();
            if (string.IsNullOrWhiteSpace(keyText) || !keys.Contains(keyText))
            {
                continue;
            }

            dictionary.Remove(key);
            removedCount++;
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries)
        {
            removedCount += RemoveKeys(mergedDictionary, keys);
        }

        return removedCount;
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

internal sealed record ResourceDictionaryDeleteResult(int RemovedCount, IReadOnlyList<ResourceDictionaryIconEntry> Items);

internal sealed class ResourceDictionaryIconEntry : INotifyPropertyChanged
{
    private bool _isSelected;

    public ResourceDictionaryIconEntry(string key, DrawingImage image, ImageSource previewImage)
    {
        Key = key;
        Image = image;
        PreviewImage = previewImage;
    }

    public string Key { get; }

    public DrawingImage Image { get; }

    public ImageSource PreviewImage { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ClearSelection()
    {
        IsSelected = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
