using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ColorfulSvg.IconSearcher;

internal sealed class IconSearcherViewModel : INotifyPropertyChanged
{
    private const string AllCollectionsKey = "__all__";

    private string _searchQuery = "folder";
    private string _outputPath = BuildSuggestedOutputPath("folder");
    private bool _isBusy;
    private string _statusTitle = "准备就绪";
    private string _statusMessage = "输入关键词后即可从 yesicon 搜索图标，并导出为 WPF ResourceDictionary。";
    private Brush _statusBrush = InfoBrush;
    private Brush _statusBackground = InfoBackgroundBrush;
    private IconSearchResultItemViewModel? _activeResult;
    private ExportModeOption _selectedExportModeOption;
    private CollectionFilterChoice _selectedCollectionFilter;
    private IReadOnlyList<IconSearchResultItemViewModel> _allSearchResults = [];
    private HashSet<string> _defaultVisibleIds = new(StringComparer.Ordinal);
    private string _browserHint = "在左侧 yesicon 浏览器中搜索。普通点击进入详情页，按住 Ctrl 再左键点击图标可直接在右侧预览。";
    private bool _canImportCurrentIcon;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Brush InfoBrush { get; } = CreateBrush("#175CD3");

    public static Brush InfoBackgroundBrush { get; } = CreateBrush("#EEF4FF");

    public static Brush SuccessBrush { get; } = CreateBrush("#166534");

    public static Brush SuccessBackgroundBrush { get; } = CreateBrush("#EAFBF3");

    public static Brush ErrorBrush { get; } = CreateBrush("#B91C1C");

    public static Brush ErrorBackgroundBrush { get; } = CreateBrush("#FEF3F2");

    public IconSearcherViewModel()
    {
        ExportModes =
        [
            new("导出新文件", "生成一个完整的 ResourceDictionary 文件。", ExportMode.CreateNewFile),
            new("追加到现有文件", "读取现有 XAML 并合并新的资源项。", ExportMode.AppendOrCreate)
        ];
        _selectedExportModeOption = ExportModes[0];
        CollectionFilters = [CollectionFilterChoice.CreateAll()];
        _selectedCollectionFilter = CollectionFilters[0];
    }

    public ObservableCollection<IconSearchResultItemViewModel> SearchResults { get; } = [];

    public ObservableCollection<IconSearchResultItemViewModel> SelectedItems { get; } = [];

    public IReadOnlyList<ExportModeOption> ExportModes { get; }

    public IReadOnlyList<CollectionFilterChoice> CollectionFilters { get; private set; }

    public ExportModeOption SelectedExportModeOption
    {
        get => _selectedExportModeOption;
        set => SetField(ref _selectedExportModeOption, value);
    }

    public CollectionFilterChoice SelectedCollectionFilter
    {
        get => _selectedCollectionFilter;
        set
        {
            if (SetField(ref _selectedCollectionFilter, value))
            {
                ApplyCollectionFilter();
                OnPropertyChanged(nameof(CanChooseCollectionFilter));
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetField(ref _searchQuery, value))
            {
                OnPropertyChanged(nameof(CanSearch));
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set => SetField(ref _outputPath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanSearch));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(SearchButtonLabel));
                OnPropertyChanged(nameof(ExportButtonLabel));
            }
        }
    }

    public string StatusTitle
    {
        get => _statusTitle;
        set => SetField(ref _statusTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string BrowserHint
    {
        get => _browserHint;
        set => SetField(ref _browserHint, value);
    }

    public bool CanImportCurrentIcon
    {
        get => _canImportCurrentIcon;
        set => SetField(ref _canImportCurrentIcon, value);
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

    public IconSearchResultItemViewModel? ActiveResult
    {
        get => _activeResult;
        set
        {
            if (SetField(ref _activeResult, value))
            {
                OnPropertyChanged(nameof(HasActiveResult));
            }
        }
    }

    public bool HasActiveResult => ActiveResult is not null;

    public bool CanSearch => !IsBusy && !string.IsNullOrWhiteSpace(SearchQuery);

    public bool CanExport => !IsBusy && SelectedItems.Count > 0;

    public bool CanChooseCollectionFilter => CollectionFilters.Count > 1;

    public string SearchButtonLabel => IsBusy ? "搜索中..." : "搜索图标";

    public string ExportButtonLabel => IsBusy ? "处理中..." : "导出选中资源";

    public string SelectionSummary => SelectedItems.Count switch
    {
        0 => "尚未选中任何图标",
        1 => "已选 1 个图标，可直接单项导出",
        _ => $"已选 {SelectedItems.Count} 个图标，可批量导出"
    };

    public void ResetSearchResults(
        IEnumerable<IconSearchResultItemViewModel> allItems,
        IEnumerable<CollectionFilterOption> collectionOptions,
        IEnumerable<string> defaultVisibleIds)
    {
        foreach (var item in _allSearchResults)
        {
            item.PropertyChanged -= SearchResult_PropertyChanged;
        }

        SearchResults.Clear();
        SelectedItems.Clear();

        var itemList = allItems.ToArray();
        foreach (var item in itemList)
        {
            item.PropertyChanged += SearchResult_PropertyChanged;
        }

        _allSearchResults = itemList;
        _defaultVisibleIds = new HashSet<string>(defaultVisibleIds, StringComparer.Ordinal);

        var options = new List<CollectionFilterChoice> { CollectionFilterChoice.CreateAll() };
        options.AddRange(collectionOptions
            .GroupBy(static option => option.Prefix, StringComparer.Ordinal)
            .Select(static group => group.First())
            .Select(static option => new CollectionFilterChoice(option.Prefix, option.Label)));
        CollectionFilters = options;

        if (!CollectionFilters.Any(option => string.Equals(option.Key, _selectedCollectionFilter.Key, StringComparison.Ordinal)))
        {
            _selectedCollectionFilter = CollectionFilters[0];
            OnPropertyChanged(nameof(SelectedCollectionFilter));
        }

        ApplyCollectionFilter();
        OnPropertyChanged(nameof(CollectionFilters));
        OnPropertyChanged(nameof(CanChooseCollectionFilter));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanExport));
    }

    public IReadOnlyList<IconSearchResultItemViewModel> GetVisibleResults()
    {
        return SearchResults.ToArray();
    }

    public IconSearchResultItemViewModel? FindSelectedItem(string iconId)
    {
        return SelectedItems.FirstOrDefault(item => string.Equals(item.Id, iconId, StringComparison.Ordinal));
    }

    public void AddSelectedItem(IconSearchResultItemViewModel item)
    {
        if (!SelectedItems.Contains(item))
        {
            SelectedItems.Add(item);
        }

        item.IsSelectedForExport = true;
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanExport));
    }

    public void RemoveSelectedItem(IconSearchResultItemViewModel item)
    {
        if (SelectedItems.Remove(item))
        {
            item.IsSelectedForExport = false;
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(CanExport));
        }
    }

    private void SearchResult_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IconSearchResultItemViewModel item || e.PropertyName != nameof(IconSearchResultItemViewModel.IsSelectedForExport))
        {
            return;
        }

        if (item.IsSelectedForExport)
        {
            if (!SelectedItems.Contains(item))
            {
                SelectedItems.Add(item);
            }
        }
        else
        {
            SelectedItems.Remove(item);
        }

        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanExport));
    }

    private void ApplyCollectionFilter()
    {
        var filtered = string.Equals(_selectedCollectionFilter.Key, AllCollectionsKey, StringComparison.Ordinal)
            ? _allSearchResults.Where(item => _defaultVisibleIds.Contains(item.Id)).ToArray()
            : _allSearchResults.Where(item => string.Equals(item.Prefix, _selectedCollectionFilter.Key, StringComparison.Ordinal)).ToArray();

        SearchResults.Clear();
        foreach (var item in filtered)
        {
            SearchResults.Add(item);
        }

        ActiveResult = SearchResults.FirstOrDefault();
    }

    public static string BuildSuggestedOutputPath(string seed)
    {
        var fileName = SanitizeFileStem(seed);
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ColorfulSvg",
            "IconSearcher");

        return Path.Combine(baseDirectory, $"{fileName}.xaml");
    }

    private static string SanitizeFileStem(string seed)
    {
        var candidate = string.IsNullOrWhiteSpace(seed) ? "icons" : seed.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidChar, '_');
        }

        candidate = candidate.Replace(' ', '-');
        return string.IsNullOrWhiteSpace(candidate) ? "icons" : candidate;
    }

    private static Brush CreateBrush(string hex)
    {
        return (Brush)new BrushConverter().ConvertFrom(hex)!;
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class IconSearchResultItemViewModel : INotifyPropertyChanged
{
    private string _resourceKey;
    private bool _isSelectedForExport;
    private bool _isLoadingDetails = true;
    private bool _hasLoadedDetails;
    private string _svgContent = string.Empty;
    private DrawingImage? _previewImage;
    private string _detailStateLabel = "等待加载详情";
    private string _authorLabel = "作者：加载后显示";
    private string _licenseLabel = "许可证：加载后显示";
    private string _keywordLabel = "关键词：-";

    public IconSearchResultItemViewModel(IconSearchCandidate candidate)
    {
        Candidate = candidate;
        _resourceKey = $"{candidate.Prefix}/{candidate.Name}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IconSearchCandidate Candidate { get; }

    public Task? DetailsTask { get; set; }

    public string Id => Candidate.Id;

    public string Prefix => Candidate.Prefix;

    public string Name => Candidate.Name;

    public string CollectionName => Candidate.CollectionName;

    public string CollectionCaption => $"{Candidate.CollectionName} / {Candidate.Prefix}";

    public string ResourceKey
    {
        get => _resourceKey;
        set => SetField(ref _resourceKey, value);
    }

    public bool IsSelectedForExport
    {
        get => _isSelectedForExport;
        set => SetField(ref _isSelectedForExport, value);
    }

    public bool IsLoadingDetails
    {
        get => _isLoadingDetails;
        set => SetField(ref _isLoadingDetails, value);
    }

    public bool HasLoadedDetails
    {
        get => _hasLoadedDetails;
        set => SetField(ref _hasLoadedDetails, value);
    }

    public string SvgContent
    {
        get => _svgContent;
        set => SetField(ref _svgContent, value);
    }

    public DrawingImage? PreviewImage
    {
        get => _previewImage;
        set => SetField(ref _previewImage, value);
    }

    public string DetailStateLabel
    {
        get => _detailStateLabel;
        set => SetField(ref _detailStateLabel, value);
    }

    public string AuthorLabel
    {
        get => _authorLabel;
        set => SetField(ref _authorLabel, value);
    }

    public string LicenseLabel
    {
        get => _licenseLabel;
        set => SetField(ref _licenseLabel, value);
    }

    public string KeywordLabel
    {
        get => _keywordLabel;
        set => SetField(ref _keywordLabel, value);
    }

    public void ApplyDetail(IconDetail detail, DrawingImage preview)
    {
        SvgContent = detail.SvgContent;
        PreviewImage = preview;
        HasLoadedDetails = true;
        IsLoadingDetails = false;
        DetailStateLabel = "SVG 与预览已就绪";
        AuthorLabel = string.IsNullOrWhiteSpace(detail.AuthorName)
            ? "作者：未提供"
            : $"作者：{detail.AuthorName}";
        LicenseLabel = string.IsNullOrWhiteSpace(detail.LicenseTitle)
            ? "许可证：未提供"
            : $"许可证：{detail.LicenseTitle}{(string.IsNullOrWhiteSpace(detail.LicenseSpdx) ? string.Empty : $" ({detail.LicenseSpdx})")}";
        KeywordLabel = detail.Keywords.Count == 0
            ? "关键词：-"
            : $"关键词：{string.Join(", ", detail.Keywords.Take(6))}";
    }

    public void ApplyLoadError(string message)
    {
        IsLoadingDetails = false;
        HasLoadedDetails = false;
        DetailStateLabel = message;
        AuthorLabel = "作者：加载失败";
        LicenseLabel = "许可证：加载失败";
        KeywordLabel = "关键词：-";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed record ExportModeOption(string Label, string Description, ExportMode Mode);

internal sealed record CollectionFilterChoice(string Key, string Label)
{
    public static CollectionFilterChoice CreateAll()
    {
        return new CollectionFilterChoice(AllKey, "全部图标库");
    }

    public static string AllKey => "__all__";
}
