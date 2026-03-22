using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ColorfulSvg.IconSearcher;

public partial class ResourceDictionaryBrowserView : UserControl
{
    private static readonly string ResourceBrowserSessionStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ColorfulSvg",
        "IconSearcher",
        "resource-browser-session.json");

    private IReadOnlyList<ResourceDictionaryIconEntry> _allItems = [];
    private string? _currentFilePath;
    private readonly DispatcherTimer _searchDebounceTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(1000)
    };

    public ResourceDictionaryBrowserView()
    {
        InitializeComponent();
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        Loaded += ResourceDictionaryBrowserView_Loaded;
        Unloaded += ResourceDictionaryBrowserView_Unloaded;
    }

    public bool HasLoadedResourceFile => !string.IsNullOrWhiteSpace(_currentFilePath);

    public void PromptOpenResourceFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "XAML 资源字典 (*.xaml)|*.xaml|所有文件 (*.*)|*.*",
            Title = "选择导出的 XAML 资源文件",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = GetInitialDirectory(_currentFilePath)
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        LoadResourceFile(dialog.FileName);
    }

    private void ResourceDictionaryBrowserView_Loaded(object sender, RoutedEventArgs e)
    {
        TryRestoreLastOpenedFile();
    }

    private void ResourceDictionaryBrowserView_Unloaded(object sender, RoutedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        PersistLastOpenedFile();
    }

    private void OpenResourceFile_Click(object sender, RoutedEventArgs e)
    {
        PromptOpenResourceFile();
    }

    private void LoadResourceFile(string filePath)
    {
        try
        {
            var items = ResourceDictionaryIconReader.Load(filePath);
            _currentFilePath = Path.GetFullPath(filePath);
            _allItems = items;

            FilePathTextBlock.Text = _currentFilePath;
            ApplyFilter();
            PersistLastOpenedFile();
        }
        catch (Exception ex)
        {
            _allItems = [];
            CountTextBlock.Text = "0 个图标";
            ResourceItemsControl.ItemsSource = null;
            EmptyStateTextBlock.Visibility = Visibility.Visible;
            EmptyStateTextBlock.Text = "打开一个导出的 XAML 资源文件。";
            MessageBox.Show(Window.GetWindow(this), ex.Message, "读取资源文件失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: ResourceDictionaryIconEntry item })
        {
            return;
        }

        Clipboard.SetText(item.Key);
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        ApplyFilter();
    }

    private void TryRestoreLastOpenedFile()
    {
        if (HasLoadedResourceFile)
        {
            return;
        }

        try
        {
            if (!File.Exists(ResourceBrowserSessionStatePath))
            {
                return;
            }

            var json = File.ReadAllText(ResourceBrowserSessionStatePath);
            var state = JsonSerializer.Deserialize<ResourceBrowserSessionState>(json);
            if (string.IsNullOrWhiteSpace(state?.LastOpenedFilePath) ||
                !File.Exists(state.LastOpenedFilePath))
            {
                return;
            }

            LoadResourceFile(state.LastOpenedFilePath);
        }
        catch
        {
        }
    }

    private void PersistLastOpenedFile()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(ResourceBrowserSessionStatePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new ResourceBrowserSessionState
            {
                LastOpenedFilePath = _currentFilePath
            };

            File.WriteAllText(ResourceBrowserSessionStatePath, JsonSerializer.Serialize(state));
        }
        catch
        {
        }
    }

    private static string GetInitialDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : directory;
    }

    private void ApplyFilter()
    {
        var terms = (SearchTextBox.Text ?? string.Empty)
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var filteredItems = terms.Length == 0
            ? _allItems
            : _allItems
                .Where(item => terms.Any(term => item.Key.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        ResourceItemsControl.ItemsSource = filteredItems;
        CountTextBlock.Text = terms.Length == 0
            ? $"{filteredItems.Count} 个图标"
            : $"{filteredItems.Count} / {_allItems.Count} 个图标";

        EmptyStateTextBlock.Visibility = filteredItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateTextBlock.Text = _allItems.Count == 0
            ? "打开一个导出的 XAML 资源文件。"
            : "没有匹配的图标。";
    }

    private sealed class ResourceBrowserSessionState
    {
        public string? LastOpenedFilePath { get; init; }
    }
}
