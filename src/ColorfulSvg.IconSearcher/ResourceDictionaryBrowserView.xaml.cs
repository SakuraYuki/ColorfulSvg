using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    public static readonly DependencyProperty IconPreviewSizeProperty = DependencyProperty.Register(
        nameof(IconPreviewSize),
        typeof(double),
        typeof(ResourceDictionaryBrowserView),
        new PropertyMetadata(
            ResourceDictionaryBrowserPreviewSizing.DefaultIconPreviewSize,
            OnIconPreviewSizeChanged));

    public static readonly DependencyProperty ItemCardWidthProperty = DependencyProperty.Register(
        nameof(ItemCardWidth),
        typeof(double),
        typeof(ResourceDictionaryBrowserView),
        new PropertyMetadata(
            ResourceDictionaryBrowserPreviewSizing.GetCardWidth(ResourceDictionaryBrowserPreviewSizing.DefaultIconPreviewSize)));

    public static readonly DependencyProperty IsManagementModeProperty = DependencyProperty.Register(
        nameof(IsManagementMode),
        typeof(bool),
        typeof(ResourceDictionaryBrowserView),
        new PropertyMetadata(false));

    private IReadOnlyList<ResourceDictionaryIconEntry> _allItems = [];
    private string? _currentFilePath;
    private int _selectedItemCount;
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
        UpdateManagementModeControls();
    }

    public double IconPreviewSize
    {
        get => (double)GetValue(IconPreviewSizeProperty);
        set => SetValue(IconPreviewSizeProperty, NormalizeIconPreviewSize(value));
    }

    public double ItemCardWidth
    {
        get => (double)GetValue(ItemCardWidthProperty);
        private set => SetValue(ItemCardWidthProperty, value);
    }

    public bool IsManagementMode
    {
        get => (bool)GetValue(IsManagementModeProperty);
        set => SetValue(IsManagementModeProperty, value);
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
            ReplaceItems(items);

            FilePathTextBlock.Text = _currentFilePath;
            FilePathTextBlock.ToolTip = _currentFilePath;
            ApplyFilter();
            PersistLastOpenedFile();
        }
        catch (Exception ex)
        {
            _currentFilePath = null;
            SetManagementMode(false);
            ReplaceItems([]);
            FilePathTextBlock.Text = "尚未选择资源文件";
            FilePathTextBlock.ToolTip = null;
            CountTextBlock.Text = "0 个图标";
            ResourceItemsControl.ItemsSource = null;
            EmptyStateTextBlock.Visibility = Visibility.Visible;
            EmptyStateTextBlock.Text = "打开一个导出的 XAML 资源文件。";
            MessageBox.Show(Window.GetWindow(this), ex.Message, "读取资源文件失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleManagementMode_Click(object sender, RoutedEventArgs e)
    {
        if (_allItems.Count == 0)
        {
            return;
        }

        SetManagementMode(!IsManagementMode);
        ApplyFilter();
    }

    private void DeleteSelectedItems_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return;
        }

        var selectedKeys = _allItems
            .Where(static item => item.IsSelected)
            .Select(static item => item.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedKeys.Length == 0)
        {
            return;
        }

        var message = selectedKeys.Length == 1
            ? $"确认删除图标资源“{selectedKeys[0]}”吗？"
            : $"确认删除选中的 {selectedKeys.Length} 个图标资源吗？";
        if (MessageBox.Show(
                Window.GetWindow(this),
                message,
                "删除图标资源",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = ResourceDictionaryIconReader.Delete(_currentFilePath, selectedKeys);
            ReplaceItems(result.Items);
            if (_allItems.Count == 0)
            {
                SetManagementMode(false);
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "删除资源失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResourceCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ResourceDictionaryIconEntry item })
        {
            return;
        }

        if (IsManagementMode)
        {
            if (FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) is null)
            {
                ToggleItemSelection(item);
            }

            e.Handled = true;
            return;
        }

        Clipboard.SetText(item.Key);
        e.Handled = true;
    }

    private void SelectionCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ResourceDictionaryIconEntry item })
        {
            return;
        }

        ToggleItemSelection(item);
        e.Handled = true;
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
            IconPreviewSize = NormalizeIconPreviewSize(state?.IconPreviewSize ?? IconPreviewSize);
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
                LastOpenedFilePath = _currentFilePath,
                IconPreviewSize = IconPreviewSize
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
        CountTextBlock.Text = BuildCountText(filteredItems.Count, terms.Length > 0);

        EmptyStateTextBlock.Visibility = filteredItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateTextBlock.Text = _allItems.Count == 0
            ? "打开一个导出的 XAML 资源文件。"
            : "没有匹配的图标。";
    }

    private string BuildCountText(int filteredCount, bool hasFilter)
    {
        var countText = hasFilter
            ? $"{filteredCount} / {_allItems.Count} 个图标"
            : $"{filteredCount} 个图标";
        var selectedCount = _selectedItemCount;

        return IsManagementMode && selectedCount > 0
            ? $"{countText}，已选 {selectedCount} 个"
            : countText;
    }

    private void ReplaceItems(IReadOnlyList<ResourceDictionaryIconEntry> items)
    {
        _allItems = items;
        _selectedItemCount = _allItems.Count(static item => item.IsSelected);
        UpdateManagementModeControls();
    }

    private void SetManagementMode(bool enabled)
    {
        IsManagementMode = enabled;
        if (!enabled)
        {
            ClearSelection();
        }

        UpdateManagementModeControls();
    }

    private void ClearSelection()
    {
        foreach (var item in _allItems.Where(static item => item.IsSelected))
        {
            item.ClearSelection();
        }

        _selectedItemCount = 0;
        CountTextBlock.Text = BuildCountText(
            ResourceItemsControl.Items.Count,
            (SearchTextBox.Text ?? string.Empty).Length > 0);
    }

    private void UpdateManagementModeControls()
    {
        ToggleManagementButton.Content = IsManagementMode ? "退出管理" : "进入管理";
        ToggleManagementButton.IsEnabled = _allItems.Count > 0;
        DeleteSelectedButton.Visibility = IsManagementMode ? Visibility.Visible : Visibility.Collapsed;

        DeleteSelectedButton.IsEnabled = _selectedItemCount > 0;
        DeleteSelectedButton.Content = _selectedItemCount > 0
            ? $"删除已选 ({_selectedItemCount})"
            : "删除已选";
    }

    private void ToggleItemSelection(ResourceDictionaryIconEntry item)
    {
        SetItemSelected(item, !item.IsSelected);
    }

    private void SetItemSelected(ResourceDictionaryIconEntry item, bool isSelected)
    {
        if (item.IsSelected == isSelected)
        {
            return;
        }

        item.IsSelected = isSelected;
        _selectedItemCount += isSelected ? 1 : -1;
        if (_selectedItemCount < 0)
        {
            _selectedItemCount = 0;
        }

        UpdateManagementModeControls();
        CountTextBlock.Text = BuildCountText(
            ResourceItemsControl.Items.Count,
            (SearchTextBox.Text ?? string.Empty).Length > 0);
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T target)
            {
                return target;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    private static void OnIconPreviewSizeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ResourceDictionaryBrowserView view ||
            e.NewValue is not double iconPreviewSize)
        {
            return;
        }

        view.ItemCardWidth = ResourceDictionaryBrowserPreviewSizing.GetCardWidth(iconPreviewSize);
    }

    private static double NormalizeIconPreviewSize(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value)
            ? ResourceDictionaryBrowserPreviewSizing.DefaultIconPreviewSize
            : Math.Clamp(
                value,
                ResourceDictionaryBrowserPreviewSizing.MinIconPreviewSize,
                ResourceDictionaryBrowserPreviewSizing.MaxIconPreviewSize);
    }

    private sealed class ResourceBrowserSessionState
    {
        public string? LastOpenedFilePath { get; init; }
        public double? IconPreviewSize { get; init; }
    }
}
