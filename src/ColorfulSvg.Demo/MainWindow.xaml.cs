using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ColorfulSvg.Core;
using Microsoft.Win32;

namespace ColorfulSvg.Demo;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SVG 文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
            Title = "选择 SVG 文件"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _viewModel.SelectedFilePath = dialog.FileName;
        _viewModel.SelectedResourceKey = MainWindowViewModel.BuildSuggestedResourceKey(dialog.FileName);
        _viewModel.LastOutputPath = MainWindowViewModel.BuildSuggestedOutputPath(dialog.FileName, _viewModel.SelectedResourceKey);
        SetInfoStatus("文件已就绪", "已自动生成建议键名和输出文件名。你可以继续调整，然后开始转换。");
    }

    private void SelectOutputPath_Click(object sender, RoutedEventArgs e)
    {
        var suggestedPath = string.IsNullOrWhiteSpace(_viewModel.LastOutputPath)
            ? MainWindowViewModel.BuildSuggestedOutputPath(_viewModel.SelectedFilePath, _viewModel.SelectedResourceKey)
            : _viewModel.LastOutputPath;

        var dialog = new SaveFileDialog
        {
            Filter = "XAML 资源字典 (*.xaml)|*.xaml|所有文件 (*.*)|*.*",
            Title = "选择输出 XAML 路径",
            FileName = Path.GetFileName(suggestedPath),
            InitialDirectory = GetInitialDirectory(suggestedPath)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _viewModel.LastOutputPath = dialog.FileName;
        SetInfoStatus("输出路径已更新", "转换结果将保存到你刚刚选择的位置。");
    }

    private void ConvertSelectedFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.SelectedFilePath) || !File.Exists(_viewModel.SelectedFilePath))
        {
            SetErrorStatus("缺少 SVG 文件", "请先选择一个有效的 SVG 文件。");
            return;
        }

        if (string.IsNullOrWhiteSpace(_viewModel.SelectedResourceKey))
        {
            SetErrorStatus("资源键名为空", "请先填写资源键名。");
            return;
        }

        if (string.IsNullOrWhiteSpace(_viewModel.LastOutputPath))
        {
            _viewModel.LastOutputPath = MainWindowViewModel.BuildSuggestedOutputPath(_viewModel.SelectedFilePath, _viewModel.SelectedResourceKey);
        }

        try
        {
            var converter = new SvgResourceConverter();
            var result = converter.ConvertFile(_viewModel.SelectedFilePath, _viewModel.SelectedResourceKey);
            if (result.HasErrors || result.Resources.Count == 0)
            {
                var firstIssue = result.Issues.FirstOrDefault()?.Message ?? "转换失败。";
                SetErrorStatus("转换未完成", firstIssue);
                return;
            }

            converter.SaveResult(result, _viewModel.LastOutputPath);

            var resource = XamlResourceLoader.LoadFirstDrawingImage(result.Xaml);
            _viewModel.PreviewImage = resource;
            _viewModel.PreviewKeyLabel = _viewModel.SelectedResourceKey;
            _viewModel.PreviewHint = $"已生成 {Path.GetFileName(_viewModel.LastOutputPath)}，可以直接合并到 App.xaml。";
            SetSuccessStatus("转换完成", $"已输出 XAML 资源字典：{_viewModel.LastOutputPath}");
        }
        catch (Exception ex)
        {
            SetErrorStatus("转换失败", ex.Message);
        }
    }

    private static string GetInitialDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : directory;
    }

    private void SetInfoStatus(string title, string message)
    {
        ApplyStatus(title, message, MainWindowViewModel.InfoBrush, MainWindowViewModel.InfoSurfaceBrush, MainWindowViewModel.InfoIcon);
    }

    private void SetSuccessStatus(string title, string message)
    {
        ApplyStatus(title, message, MainWindowViewModel.SuccessBrush, MainWindowViewModel.SuccessSurfaceBrush, MainWindowViewModel.SuccessIcon);
    }

    private void SetErrorStatus(string title, string message)
    {
        ApplyStatus(title, message, MainWindowViewModel.ErrorBrush, MainWindowViewModel.ErrorSurfaceBrush, MainWindowViewModel.ErrorIcon);
    }

    private void ApplyStatus(string title, string message, Brush foreground, Brush background, DrawingImage icon)
    {
        _viewModel.StatusTitle = title;
        _viewModel.StatusMessage = message;
        _viewModel.StatusBrush = foreground;
        _viewModel.StatusBackground = background;
        _viewModel.StatusIcon = icon;
    }
}

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _selectedFilePath = "尚未选择 SVG 文件。";
    private string _selectedResourceKey = string.Empty;
    private string _lastOutputPath = "尚未生成输出文件。";
    private string _statusTitle = "等待输入";
    private string _statusMessage = "选择一个 SVG 文件后，这里会显示转换进度、错误提示和输出结果。";
    private Brush _statusBrush = InfoBrush;
    private Brush _statusBackground = InfoSurfaceBrush;
    private DrawingImage _statusIcon = InfoIcon;
    private DrawingImage? _previewImage;
    private string _previewKeyLabel = "folder/open";
    private string _previewHint = "右侧完成转换后，新的 DrawingImage 会立即显示在这里。";
    private bool _canConvert;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Brush InfoBrush { get; } = CreateBrush("#175CD3");

    public static Brush InfoSurfaceBrush { get; } = CreateBrush("#EEF4FF");

    public static Brush SuccessBrush { get; } = CreateBrush("#166534");

    public static Brush SuccessSurfaceBrush { get; } = CreateBrush("#EAFBF3");

    public static Brush ErrorBrush { get; } = CreateBrush("#B91C1C");

    public static Brush ErrorSurfaceBrush { get; } = CreateBrush("#FEF3F2");

    public static DrawingImage InfoIcon { get; } = LoadBundledImage("status/info");

    public static DrawingImage SuccessIcon { get; } = LoadBundledImage("action/check");

    public static DrawingImage ErrorIcon { get; } = LoadBundledImage("status/warning");

    public IReadOnlyList<IconCardViewModel> IconCards { get; } =
    [
        new("folder/open", "文件夹图标，展示多层几何和不同填充色。", "Source=\"{StaticResource folder/open}\"", LoadBundledImage("folder/open"), CreateBrush("#E8F1FF")),
        new("status/warning", "状态图标，适合放在提示条、异常消息或提醒入口。", "Source=\"{StaticResource status/warning}\"", LoadBundledImage("status/warning"), CreateBrush("#FFF1D6")),
        new("action/check", "确认图标，可以直接绑定到列表项、按钮或完成态。", "Source=\"{StaticResource action/check}\"", LoadBundledImage("action/check"), CreateBrush("#E6F8EE"))
    ];

    public MainWindowViewModel()
    {
        RefreshCanConvert();
    }

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            if (SetField(ref _selectedFilePath, value))
            {
                RefreshCanConvert();
            }
        }
    }

    public string SelectedResourceKey
    {
        get => _selectedResourceKey;
        set
        {
            if (SetField(ref _selectedResourceKey, value))
            {
                RefreshCanConvert();
            }
        }
    }

    public string LastOutputPath
    {
        get => _lastOutputPath;
        set => SetField(ref _lastOutputPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string StatusTitle
    {
        get => _statusTitle;
        set => SetField(ref _statusTitle, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        set => SetField(ref _statusBrush, value);
    }

    public Brush StatusBackground
    {
        get => _statusBackground;
        set => SetField(ref _statusBackground, value);
    }

    public DrawingImage StatusIcon
    {
        get => _statusIcon;
        set => SetField(ref _statusIcon, value);
    }

    public DrawingImage? PreviewImage
    {
        get => _previewImage ?? LoadBundledImage("folder/open");
        set => SetField(ref _previewImage, value);
    }

    public string PreviewKeyLabel
    {
        get => _previewKeyLabel;
        set => SetField(ref _previewKeyLabel, value);
    }

    public string PreviewHint
    {
        get => _previewHint;
        set => SetField(ref _previewHint, value);
    }

    public bool CanConvert
    {
        get => _canConvert;
        private set => SetField(ref _canConvert, value);
    }

    public static string BuildSuggestedResourceKey(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "ConvertedSvg";
        }

        return fileName.Trim().Replace(" ", "_", StringComparison.Ordinal);
    }

    public static string BuildSuggestedOutputPath(string? selectedFilePath, string? resourceKey)
    {
        var safeKey = CreateSafeOutputFileStem(resourceKey);
        if (!string.IsNullOrWhiteSpace(selectedFilePath) && File.Exists(selectedFilePath))
        {
            var directory = Path.GetDirectoryName(selectedFilePath) ?? Environment.CurrentDirectory;
            return Path.Combine(directory, $"{safeKey}.xaml");
        }

        var fallbackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ColorfulSvg",
            "DemoOutput");
        return Path.Combine(fallbackDirectory, $"{safeKey}.xaml");
    }

    private static DrawingImage LoadBundledImage(string key)
    {
        return (DrawingImage)Application.Current.FindResource(key);
    }

    private static Brush CreateBrush(string hex)
    {
        return (Brush)new BrushConverter().ConvertFrom(hex)!;
    }

    private static string CreateSafeOutputFileStem(string? resourceKey)
    {
        var candidate = string.IsNullOrWhiteSpace(resourceKey) ? "ConvertedSvg" : resourceKey.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidChar, '_');
        }

        candidate = candidate.Replace('/', '_').Replace('\\', '_');
        return string.IsNullOrWhiteSpace(candidate) ? "ConvertedSvg" : candidate;
    }

    private void RefreshCanConvert()
    {
        CanConvert = File.Exists(_selectedFilePath) && !string.IsNullOrWhiteSpace(_selectedResourceKey);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed record IconCardViewModel(string Key, string Description, string UsageExample, DrawingImage Icon, Brush AccentBrush);

internal static class XamlResourceLoader
{
    public static DrawingImage LoadFirstDrawingImage(string xaml)
    {
        var dictionary = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
        var firstEntry = dictionary.Values.OfType<DrawingImage>().FirstOrDefault();
        return firstEntry ?? throw new InvalidOperationException("未生成任何 DrawingImage 资源。");
    }
}
