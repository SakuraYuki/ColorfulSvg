using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ColorfulSvg.IconSearcher;

public partial class SvgDirectoryBrowserView : UserControl
{
    private static readonly string SvgDirectoryBrowserSessionStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ColorfulSvg",
        "IconSearcher",
        "svg-directory-browser-session.json");

    public static readonly DependencyProperty IconPreviewSizeProperty = DependencyProperty.Register(
        nameof(IconPreviewSize),
        typeof(double),
        typeof(SvgDirectoryBrowserView),
        new PropertyMetadata(32d));

    private IReadOnlyList<SvgDirectoryIconEntry> _allItems = [];
    private string? _currentDirectoryPath;
    private IReadOnlyList<string> _selectedFilePaths = [];
    private int _skippedFileCount;
    private readonly DispatcherTimer _searchDebounceTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(350)
    };
    private CancellationTokenSource? _loadCancellationTokenSource;

    public SvgDirectoryBrowserView()
    {
        InitializeComponent();
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        Loaded += SvgDirectoryBrowserView_Loaded;
        Unloaded += SvgDirectoryBrowserView_Unloaded;
    }

    public double IconPreviewSize
    {
        get => (double)GetValue(IconPreviewSizeProperty);
        set => SetValue(IconPreviewSizeProperty, value);
    }

    public bool HasLoadedSource => !string.IsNullOrWhiteSpace(_currentDirectoryPath) || _selectedFilePaths.Count > 0;

    public void PromptOpenDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择包含 SVG 图标的目录",
            Multiselect = false,
            InitialDirectory = GetInitialDirectory()
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true ||
            string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        LoadDirectory(dialog.FolderName);
    }

    public void PromptOpenFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SVG 文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
            Title = "选择一个或多个 SVG 文件",
            CheckFileExists = true,
            Multiselect = true,
            InitialDirectory = GetInitialDirectory()
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true ||
            dialog.FileNames.Length == 0)
        {
            return;
        }

        LoadFiles(dialog.FileNames);
    }

    private void SvgDirectoryBrowserView_Loaded(object sender, RoutedEventArgs e)
    {
        TryRestoreLastOpenedSource();
    }

    private void SvgDirectoryBrowserView_Unloaded(object sender, RoutedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = null;
        PersistLastOpenedSource();
    }

    private void OpenDirectory_Click(object sender, RoutedEventArgs e)
    {
        PromptOpenDirectory();
    }

    private void OpenFiles_Click(object sender, RoutedEventArgs e)
    {
        PromptOpenFiles();
    }

    private async void LoadDirectory(string directoryPath)
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _loadCancellationTokenSource.Token;
        var fullPath = Path.GetFullPath(directoryPath);

        try
        {
            BeginLoading("正在读取目录中的 SVG 图标...");

            var result = await Task.Run(() => SvgDirectoryIconReader.Load(fullPath), cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _currentDirectoryPath = fullPath;
            _selectedFilePaths = [];
            _allItems = result.Items;
            _skippedFileCount = result.SkippedFileCount;
            UpdateSourceHeader(fullPath, fullPath);
            ApplyFilter();
            PersistLastOpenedSource();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ResetSourceState();
            MessageBox.Show(Window.GetWindow(this), ex.Message, "读取 SVG 目录失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LoadFiles(IReadOnlyList<string> filePaths)
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _loadCancellationTokenSource.Token;
        var normalizedFilePaths = filePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        try
        {
            BeginLoading("正在读取选中的 SVG 文件...");

            var result = await Task.Run(() => SvgDirectoryIconReader.LoadFiles(normalizedFilePaths), cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _currentDirectoryPath = null;
            _selectedFilePaths = normalizedFilePaths;
            _allItems = result.Items;
            _skippedFileCount = result.SkippedFileCount;
            UpdateSourceHeader(
                BuildSelectedFilesLabel(normalizedFilePaths),
                string.Join(Environment.NewLine, normalizedFilePaths));
            ApplyFilter();
            PersistLastOpenedSource();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ResetSourceState();
            MessageBox.Show(Window.GetWindow(this), ex.Message, "读取 SVG 文件失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    private void TryRestoreLastOpenedSource()
    {
        if (HasLoadedSource)
        {
            return;
        }

        try
        {
            if (!File.Exists(SvgDirectoryBrowserSessionStatePath))
            {
                return;
            }

            var json = File.ReadAllText(SvgDirectoryBrowserSessionStatePath);
            var state = JsonSerializer.Deserialize<SvgDirectoryBrowserSessionState>(json);
            var filePaths = state?.LastOpenedFilePaths?
                .Where(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (filePaths is { Length: > 0 })
            {
                LoadFiles(filePaths);
                return;
            }

            if (string.IsNullOrWhiteSpace(state?.LastOpenedDirectoryPath) ||
                !Directory.Exists(state.LastOpenedDirectoryPath))
            {
                return;
            }

            LoadDirectory(state.LastOpenedDirectoryPath);
        }
        catch
        {
        }
    }

    private void PersistLastOpenedSource()
    {
        if (!HasLoadedSource)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(SvgDirectoryBrowserSessionStatePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new SvgDirectoryBrowserSessionState
            {
                LastOpenedDirectoryPath = _currentDirectoryPath,
                LastOpenedFilePaths = _selectedFilePaths.Count == 0 ? null : _selectedFilePaths.ToArray()
            };

            File.WriteAllText(SvgDirectoryBrowserSessionStatePath, JsonSerializer.Serialize(state));
        }
        catch
        {
        }
    }

    private string GetInitialDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_currentDirectoryPath) && Directory.Exists(_currentDirectoryPath))
        {
            return _currentDirectoryPath;
        }

        var firstSelectedFilePath = _selectedFilePaths.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstSelectedFilePath))
        {
            var directory = Path.GetDirectoryName(firstSelectedFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void BeginLoading(string message)
    {
        CountTextBlock.Text = "加载中...";
        ResourceItemsControl.ItemsSource = null;
        EmptyStateTextBlock.Visibility = Visibility.Visible;
        EmptyStateTextBlock.Text = message;
    }

    private void ResetSourceState()
    {
        _currentDirectoryPath = null;
        _selectedFilePaths = [];
        _allItems = [];
        _skippedFileCount = 0;
        UpdateSourceHeader("尚未选择 SVG 目录或文件", null);
        CountTextBlock.Text = "0 个图标";
        ResourceItemsControl.ItemsSource = null;
        EmptyStateTextBlock.Visibility = Visibility.Visible;
        EmptyStateTextBlock.Text = "打开一个包含 SVG 图标的目录，或选择多个 SVG 文件。";
    }

    private void UpdateSourceHeader(string text, string? toolTip)
    {
        DirectoryPathTextBlock.Text = text;
        DirectoryPathTextBlock.ToolTip = toolTip;
    }

    private static string BuildSelectedFilesLabel(IReadOnlyList<string> filePaths)
    {
        return filePaths.Count switch
        {
            0 => "尚未选择 SVG 目录或文件",
            1 => filePaths[0],
            _ => $"已选择 {filePaths.Count} 个 SVG 文件"
        };
    }

    private void ApplyFilter()
    {
        var terms = (SearchTextBox.Text ?? string.Empty)
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var filteredItems = terms.Length == 0
            ? _allItems
            : _allItems
                .Where(item => terms.Any(term =>
                    item.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    item.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    item.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    item.FilePath.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        ResourceItemsControl.ItemsSource = filteredItems;
        CountTextBlock.Text = BuildCountText(filteredItems.Count);
        EmptyStateTextBlock.Visibility = filteredItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateTextBlock.Text = GetEmptyStateText();
    }

    private string BuildCountText(int filteredCount)
    {
        var countText = filteredCount == _allItems.Count
            ? $"{filteredCount} 个图标"
            : $"{filteredCount} / {_allItems.Count} 个图标";

        return _skippedFileCount == 0
            ? countText
            : $"{countText}，跳过 {_skippedFileCount} 个";
    }

    private string GetEmptyStateText()
    {
        if (!HasLoadedSource)
        {
            return "打开一个包含 SVG 图标的目录，或选择多个 SVG 文件。";
        }

        if (_allItems.Count == 0)
        {
            return _skippedFileCount > 0
                ? "已找到 SVG，但未能生成可预览图标。"
                : string.IsNullOrWhiteSpace(_currentDirectoryPath)
                    ? "所选文件中没有可显示的 SVG 图标。"
                    : "所选目录下未找到 SVG 图标。";
        }

        return "没有匹配的图标。";
    }

    private sealed class SvgDirectoryBrowserSessionState
    {
        public string? LastOpenedDirectoryPath { get; init; }
        public string[]? LastOpenedFilePaths { get; init; }
    }
}
