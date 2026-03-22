using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace ColorfulSvg.IconSearcher;

public partial class MainWindow : Window
{
    private static readonly string BrowserSessionStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ColorfulSvg",
        "IconSearcher",
        "browser-session.json");

    private static readonly string[] BlockedAdScriptFilterPatterns =
    [
        "https://cdn.wwads.cn/js/makemoney.js*",
        "https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js*"
    ];

    private static readonly string[] BlockedAdScriptUriPrefixes =
    [
        "https://cdn.wwads.cn/js/makemoney.js",
        "https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js"
    ];

    private static readonly JsonSerializerOptions BrowserJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string CtrlClickPreviewScript = """
        (() => {
          if (window.__colorfulSvgCtrlPreviewInstalled) {
            return;
          }

          window.__colorfulSvgCtrlPreviewInstalled = true;
          document.addEventListener('click', event => {
            if (!(event.ctrlKey || event.metaKey) || event.button !== 0) {
              return;
            }

            const target = event.target;
            if (!target || typeof target.closest !== 'function') {
              return;
            }

            const anchor = target.closest('a[href]');
            if (!anchor) {
              return;
            }

            const href = anchor.getAttribute('href') || '';
            if (!href.startsWith('/') || href.startsWith('/search') || href.startsWith('/api') || href.startsWith('/_nuxt') || href.startsWith('/favicon')) {
              return;
            }

            const parts = href.split('?')[0].split('#')[0].split('/').filter(Boolean);
            if (parts.length !== 2) {
              return;
            }

            event.preventDefault();
            event.stopPropagation();
            window.chrome.webview.postMessage(JSON.stringify({ type: 'preview-icon', href }));
          }, true);

          const originalSetItem = window.localStorage.setItem.bind(window.localStorage);
          window.localStorage.setItem = function(key, value) {
            originalSetItem(key, value);
            if (key === 'iconBox') {
              window.chrome.webview.postMessage(JSON.stringify({ type: 'iconbox-sync', raw: value }));
            }
          };

          window.__colorfulSvgSetPickMode = (enabled) => {
            const floatingButton = document.querySelector('div[data-tip][class*="bottom-[72px]"][class*="right-4"][class*="shadow-2xl"]');
            const drawer = Array.from(document.querySelectorAll('div[class*="fixed"][class*="bottom-0"][class*="bg-body"]'))
              .find(node => node.querySelector('[class*="material-symbols--download-rounded"], [class*="material-symbols--add"]'));

            const isOpen = !!drawer;
            if (floatingButton && isOpen !== enabled) {
              floatingButton.click();
            }

            document.documentElement.dataset.colorfulSvgPick = enabled ? '1' : '0';
          };

          window.__colorfulSvgHideAds = () => {
            const adContainerSelector = '.wwads-cn, .wwads-horizontal, .wwads-vertical';
            const adNodeSelector = 'ins.adsbygoogle, iframe[src*="googlesyndication"], iframe[id*="google_ads_iframe"], a.replace[href*="strongme.app"]';
            const detailAdSelector = `${adContainerSelector}, ${adNodeSelector}`;
            const adsByGoogleClassNames = ['adsbygoogle', 'relative', 'z-10'];
            const searchAdHostClassNames = ['absolute', 'top-6', 'right-6', 'z-10', 'w-[300px]'];

            const hide = (node) => {
              if (!node || !(node instanceof HTMLElement)) {
                return;
              }

              node.style.setProperty('display', 'none', 'important');
              node.style.setProperty('visibility', 'hidden', 'important');
              node.style.setProperty('opacity', '0', 'important');
              node.style.setProperty('height', '0', 'important');
              node.style.setProperty('min-height', '0', 'important');
              node.style.setProperty('max-height', '0', 'important');
              node.style.setProperty('width', '0', 'important');
              node.style.setProperty('min-width', '0', 'important');
              node.style.setProperty('max-width', '0', 'important');
              node.style.setProperty('margin', '0', 'important');
              node.style.setProperty('padding', '0', 'important');
              node.style.setProperty('border', '0', 'important');
              node.style.setProperty('overflow', 'hidden', 'important');
              node.style.setProperty('pointer-events', 'none', 'important');
              node.setAttribute('aria-hidden', 'true');
            };

            const hasAllClasses = (node, classNames) => {
              if (!node || !(node instanceof HTMLElement)) {
                return false;
              }

              return classNames.every(className => node.classList.contains(className));
            };

            const findClosestMatchingAncestor = (node, classNames) => {
              let current = node?.parentElement ?? null;
              while (current) {
                if (hasAllClasses(current, classNames)) {
                  return current;
                }

                current = current.parentElement;
              }

              return null;
            };

            document.querySelectorAll(adContainerSelector).forEach(hide);

            document.querySelectorAll(adNodeSelector).forEach(hide);

            document.querySelectorAll('ins.adsbygoogle').forEach(node => {
              if (!hasAllClasses(node, adsByGoogleClassNames)) {
                return;
              }

              const host = findClosestMatchingAncestor(node, searchAdHostClassNames);
              if (host) {
                hide(host);
              }
            });

            document.querySelectorAll('.light-box').forEach(node => {
              if (node.querySelector(detailAdSelector)) {
                hide(node);
              }
            });
          };

          window.__colorfulSvgHideAds();
          if (!window.__colorfulSvgAdObserverInstalled) {
            window.__colorfulSvgAdObserverInstalled = true;
            const observer = new MutationObserver(() => window.__colorfulSvgHideAds());
            observer.observe(document.documentElement, { childList: true, subtree: true });
          }

          if (!document.getElementById('colorfulsvg-host-style')) {
            const style = document.createElement('style');
            style.id = 'colorfulsvg-host-style';
            style.textContent = `
              .wwads-cn,
              .wwads-horizontal,
              .wwads-vertical,
              ins.adsbygoogle,
              iframe[src*="googlesyndication"],
              iframe[id*="google_ads_iframe"],
              a.replace[href*="strongme.app"] {
                display: none !important;
                visibility: hidden !important;
                opacity: 0 !important;
                pointer-events: none !important;
              }

              html[data-colorful-svg-pick="1"] div[data-tip][class*="bottom-[72px]"][class*="right-4"][class*="shadow-2xl"] {
                opacity: 0 !important;
                pointer-events: none !important;
              }

              html[data-colorful-svg-pick="1"] div[class*="fixed"][class*="bottom-0"][class*="bg-body"] {
                display: none !important;
              }
            `;
            document.head.appendChild(style);
          }
        })();
        """;

    private readonly IconSearcherViewModel _viewModel = new();
    private readonly YesIconClient _yesIconClient = new();
    private readonly IconExportService _exportService = new();
    private readonly SvgZipExportService _svgZipExportService = new();
    private readonly HashSet<string> _browserKnownPickedIconIds = new(StringComparer.Ordinal);
    private CancellationTokenSource? _browserNavigationCancellationTokenSource;
    private HelpWindow? _helpWindow;
    private double _workspacePanelWidth = 200;
    private bool _outputPathCustomized;
    private bool _browserInitialized;
    private bool _browserPickModeEnabled;
    private bool _workspacePanelVisible = true;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        WirePageEvents();
        SetWorkspacePanelVisibility(true);
        DashboardTab.IsSelected = true;
        Loaded += Window_Loaded;
        Deactivated += MainWindow_Deactivated;
    }

    private IconSearchView IconSearchPageView => IconSearchPage;

    private DashboardView DashboardPageView => DashboardPage;

    private Microsoft.Web.WebView2.Wpf.WebView2 BrowserView => IconSearchPageView.BrowserViewControl;

    private Button BrowserBackButton => IconSearchPageView.BrowserBackButtonControl;

    private Button BrowserForwardButton => IconSearchPageView.BrowserForwardButtonControl;

    private Button WorkspaceToggleButton => IconSearchPageView.WorkspaceToggleButtonControl;

    private Button WorkspacePeekButton => IconSearchPageView.WorkspacePeekButtonControl;

    private GridSplitter WorkspaceSplitter => IconSearchPageView.WorkspaceSplitterControl;

    private Border WorkspacePanel => IconSearchPageView.WorkspacePanelControl;

    private ColumnDefinition WorkspaceSpacerColumn => IconSearchPageView.WorkspaceSpacerColumnDefinition;

    private ColumnDefinition WorkspaceColumn => IconSearchPageView.WorkspaceColumnDefinition;

    private void WirePageEvents()
    {
        DashboardPageView.OpenIconSearchButtonControl.Click += OpenIconSearchTab_Click;
        DashboardPageView.OpenResourceBrowserButtonControl.Click += OpenResourceBrowser_Click;
        DashboardPageView.OpenSvgDirectoryBrowserButtonControl.Click += OpenSvgDirectoryBrowser_Click;
        DashboardPageView.ShowHelpButtonControl.Click += ShowHelp_Click;

        IconSearchPageView.SearchRequested += Search_Click;
        IconSearchPageView.BrowserBackRequested += BrowserBack_Click;
        IconSearchPageView.BrowserForwardRequested += BrowserForward_Click;
        IconSearchPageView.BrowserRefreshRequested += BrowserRefresh_Click;
        IconSearchPageView.WorkspaceToggleRequested += ToggleWorkspacePanel_Click;
        IconSearchPageView.ClearSelectedIconsRequested += ClearSelectedIcons_Click;
        IconSearchPageView.ImportCurrentIconRequested += ImportCurrentIcon_Click;
        IconSearchPageView.ImportVisibleIconsRequested += ImportVisibleIcons_Click;
        IconSearchPageView.ImportLocalSvgFilesRequested += ImportLocalSvgFiles_Click;
        IconSearchPageView.ApplyResourceKeyPrefixRequested += ApplyResourceKeyPrefix_Click;
        IconSearchPageView.BrowseOutputPathRequested += BrowseOutputPath_Click;
        IconSearchPageView.ExportRequested += Export_Click;
        IconSearchPageView.ExportSvgZipRequested += ExportSvgZip_Click;
        IconSearchPageView.BrowserPreviewMouseLeftButtonDownRequested += BrowserView_PreviewMouseLeftButtonDown;
        IconSearchPageView.RemoveSelectedIconRequested += IconSearchPage_RemoveSelectedIconRequested;
    }

    protected override void OnClosed(EventArgs e)
    {
        PersistBrowserSessionState();
        _browserNavigationCancellationTokenSource?.Cancel();
        _browserNavigationCancellationTokenSource?.Dispose();

        if (_helpWindow is not null)
        {
            _helpWindow.Close();
            _helpWindow = null;
        }

        base.OnClosed(e);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeBrowserAsync();
        if (_browserInitialized)
        {
            NavigateToInitialPage();
        }
    }

    private async void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        if (_browserPickModeEnabled)
        {
            await SetBrowserPickModeAsync(false);
        }
    }

    private async Task InitializeBrowserAsync()
    {
        if (_browserInitialized)
        {
            return;
        }

        try
        {
            await BrowserView.EnsureCoreWebView2Async();
            BrowserView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            BrowserView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            BrowserView.CoreWebView2.Settings.IsZoomControlEnabled = true;
            RegisterAdScriptBlocking(BrowserView.CoreWebView2);
            await BrowserView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(CtrlClickPreviewScript);
            BrowserView.NavigationCompleted += BrowserView_NavigationCompleted;
            BrowserView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
            BrowserView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
            BrowserView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
            BrowserView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            BrowserView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            _browserInitialized = true;
            UpdateBrowserButtons();
            SetInfoStatus("浏览器已就绪", "左侧已接入 yesicon Web 界面。按住 Ctrl 会自动进入快速选取模式，直接点图标即可同步到右侧。");
        }
        catch (Exception ex)
        {
            _viewModel.BrowserHint = $"WebView2 初始化失败：{ex.Message}";
            SetErrorStatus("浏览器初始化失败", "请确认系统已安装 WebView2 Runtime。");
        }
    }

    private static void RegisterAdScriptBlocking(CoreWebView2 coreWebView2)
    {
        foreach (var pattern in BlockedAdScriptFilterPatterns)
        {
            coreWebView2.AddWebResourceRequestedFilter(pattern, CoreWebView2WebResourceContext.Script);
        }
    }

    private static bool IsBlockedAdScriptRequest(string? requestUri)
    {
        if (string.IsNullOrWhiteSpace(requestUri))
        {
            return false;
        }

        foreach (var prefix in BlockedAdScriptUriPrefixes)
        {
            if (requestUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void NavigateToInitialPage()
    {
        if (TryRestoreBrowserSessionState(out var restoredUri))
        {
            BrowserView.Source = restoredUri;
            _viewModel.BrowserHint = $"已恢复上次浏览位置：{restoredUri}";
            SetInfoStatus("已恢复浏览位置", $"继续打开上次停留的页面：{restoredUri}");
            return;
        }

        NavigateToSearchPage();
    }

    private bool TryRestoreBrowserSessionState(out Uri restoredUri)
    {
        restoredUri = null!;

        try
        {
            if (!File.Exists(BrowserSessionStatePath))
            {
                return false;
            }

            var json = File.ReadAllText(BrowserSessionStatePath);
            var state = JsonSerializer.Deserialize<BrowserSessionState>(json, BrowserJsonOptions);
            if (state is null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(state.SearchQuery))
            {
                _viewModel.SearchQuery = state.SearchQuery.Trim();
                if (!_outputPathCustomized)
                {
                    _viewModel.OutputPath = IconSearcherViewModel.BuildSuggestedOutputPath(_viewModel.SearchQuery);
                }
            }

            if (!Uri.TryCreate(state.LastBrowserUri, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Host, "yesicon.app", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            restoredUri = uri;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void PersistBrowserSessionState()
    {
        try
        {
            var directory = Path.GetDirectoryName(BrowserSessionStatePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new BrowserSessionState
            {
                LastBrowserUri = GetCurrentBrowserUri()?.AbsoluteUri,
                SearchQuery = _viewModel.SearchQuery
            };

            File.WriteAllText(BrowserSessionStatePath, JsonSerializer.Serialize(state));
        }
        catch
        {
        }
    }

    private void OpenIconSearchTab_Click(object sender, RoutedEventArgs e)
    {
        EnsureIconSearchTabOpen();
    }

    private void CloseIconSearchTab_Click(object sender, RoutedEventArgs e)
    {
        SetWorkspacePanelVisibility(false);
        IconSearchTab.Visibility = Visibility.Collapsed;
        DashboardTab.IsSelected = true;
    }

    private void EnsureIconSearchTabOpen()
    {
        IconSearchTab.Visibility = Visibility.Visible;
        IconSearchTab.IsSelected = true;
    }

    private void ToggleWorkspacePanel_Click(object sender, RoutedEventArgs e)
    {
        EnsureIconSearchTabOpen();
        SetWorkspacePanelVisibility(!_workspacePanelVisible);
    }

    private void SetWorkspacePanelVisibility(bool visible)
    {
        if (!visible && WorkspaceColumn.Width.Value > 0)
        {
            _workspacePanelWidth = Math.Max(160, WorkspaceColumn.ActualWidth > 0 ? WorkspaceColumn.ActualWidth : WorkspaceColumn.Width.Value);
        }

        _workspacePanelVisible = visible;
        ApplyWorkspacePanelVisualState(visible);
        WorkspacePanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceSpacerColumn.Width = visible ? new GridLength(10) : new GridLength(0);
        WorkspaceColumn.Width = visible ? new GridLength(_workspacePanelWidth) : new GridLength(0);
    }

    private void WorkspacePanel_Opened(object? sender, EventArgs e)
    {
        ApplyWorkspacePanelVisualState(true);
    }

    private void WorkspacePanel_Closed(object? sender, EventArgs e)
    {
        ApplyWorkspacePanelVisualState(false);
    }

    private void ApplyWorkspacePanelVisualState(bool visible)
    {
        _workspacePanelVisible = visible;
        WorkspacePeekButton.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        WorkspaceMenuItem.Header = visible ? "隐藏工作区" : "打开工作区";

        if (WorkspaceToggleButton.Content is TextBlock buttonText)
        {
            buttonText.Text = visible ? "收起工作区" : "打开工作区";
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSearchPage();
    }

    private void ShowHelp_Click(object sender, RoutedEventArgs e)
    {
        if (_helpWindow is { IsLoaded: true })
        {
            _helpWindow.Activate();
            return;
        }

        _helpWindow = new HelpWindow
        {
            Owner = this
        };
        _helpWindow.Closed += (_, _) => _helpWindow = null;
        _helpWindow.Show();
    }

    private void OpenResourceBrowser_Click(object sender, RoutedEventArgs e)
    {
        ResourceBrowserTab.Visibility = Visibility.Visible;
        ResourceBrowserTab.IsSelected = true;

        if (!ResourceBrowserView.HasLoadedResourceFile)
        {
            ResourceBrowserView.PromptOpenResourceFile();
        }
    }

    private void OpenSvgDirectoryBrowser_Click(object sender, RoutedEventArgs e)
    {
        SvgDirectoryBrowserTab.Visibility = Visibility.Visible;
        SvgDirectoryBrowserTab.IsSelected = true;

        if (!SvgDirectoryBrowserView.HasLoadedSource)
        {
            SvgDirectoryBrowserView.PromptOpenDirectory();
        }
    }

    private void CloseResourceBrowserTab_Click(object sender, RoutedEventArgs e)
    {
        ResourceBrowserTab.Visibility = Visibility.Collapsed;
        DashboardTab.IsSelected = true;
    }

    private void CloseSvgDirectoryBrowserTab_Click(object sender, RoutedEventArgs e)
    {
        SvgDirectoryBrowserTab.Visibility = Visibility.Collapsed;
        DashboardTab.IsSelected = true;
    }

    private void IconSearchPage_RemoveSelectedIconRequested(object? sender, IconSearchItemEventArgs e)
    {
        _viewModel.RemoveSelectedItem(e.Item);
        if (ReferenceEquals(_viewModel.ActiveResult, e.Item))
        {
            _viewModel.ActiveResult = _viewModel.SelectedItems.FirstOrDefault();
        }

        SetInfoStatus("已移除图标", $"'{e.Item.Id}' 已从右侧导出列表移除。");
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && !_browserPickModeEnabled)
        {
            await SetBrowserPickModeAsync(true);
        }
    }

    private async void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) &&
            (Keyboard.Modifiers & ModifierKeys.Control) == 0 &&
            _browserPickModeEnabled)
        {
            await SetBrowserPickModeAsync(false);
        }
    }

    private void BrowserBack_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserView.CanGoBack)
        {
            BrowserView.GoBack();
        }
    }

    private void BrowserForward_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserView.CanGoForward)
        {
            BrowserView.GoForward();
        }
    }

    private void BrowserRefresh_Click(object sender, RoutedEventArgs e)
    {
        BrowserView.Reload();
    }

    private async void BrowserView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || !_browserInitialized)
        {
            return;
        }

        e.Handled = true;

        try
        {
            var point = e.GetPosition(BrowserView);
            var hit = await HitTestIconLinkAsync(point.X, point.Y);
            if (!string.IsNullOrWhiteSpace(hit?.Href) &&
                Uri.TryCreate(new Uri("https://yesicon.app"), hit.Href, out var iconUri))
            {
                await SyncCurrentBrowserIconAsync(iconUri);
                return;
            }

            SetInfoStatus("未命中图标", "Ctrl+左键点击的位置不是图标链接。请直接点在图标卡片或图标名称上。");
        }
        catch (Exception ex)
        {
            SetErrorStatus("快速预览失败", ex.Message);
        }
    }

    private async void ImportCurrentIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveResult is null)
        {
            SetErrorStatus("当前页面不可导入", "请先在左侧打开一个具体的图标详情页。");
            return;
        }

        try
        {
            _viewModel.IsBusy = true;
            await EnsureItemDetailsLoadedAsync(_viewModel.ActiveResult, CancellationToken.None);

            var existing = _viewModel.FindSelectedItem(_viewModel.ActiveResult.Id);
            if (existing is not null)
            {
                _viewModel.ActiveResult = existing;
                _viewModel.BrowserHint = "这个图标已经在导出列表中，可以直接编辑资源键或导出。";
                SetInfoStatus("图标已存在", $"'{existing.Id}' 已在导出列表中。");
                return;
            }

            ApplyConfiguredPrefixToItem(_viewModel.ActiveResult);
            _viewModel.AddSelectedItem(_viewModel.ActiveResult);
            _viewModel.BrowserHint = "当前图标已加入导出列表，可以继续浏览并添加更多图标。";
            SetSuccessStatus("已加入导出列表", $"'{_viewModel.ActiveResult.Id}' 已加入右侧导出列表。");
        }
        catch (Exception ex)
        {
            SetErrorStatus("添加图标失败", ex.Message);
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private async void ImportVisibleIcons_Click(object sender, RoutedEventArgs e)
    {
        if (!_browserInitialized || BrowserView.CoreWebView2 is null)
        {
            SetErrorStatus("浏览器尚未就绪", "请等待 yesicon 页面完成初始化后再尝试批量选取。");
            return;
        }

        try
        {
            _viewModel.IsBusy = true;

            var candidates = await GetCurrentPageIconCandidatesAsync();
            if (candidates.Count == 0)
            {
                if (TryCreateIconCandidate(GetCurrentBrowserUri(), out _))
                {
                    SetInfoStatus("当前是详情页", "这个按钮会批量加入当前结果页中的图标。若只想加入当前图标，请点击“添加当前图标”。");
                }
                else
                {
                    SetInfoStatus("当前页没有可选图标", "请先打开 yesicon 搜索结果页或图标库列表页，再使用“全选当前页”。");
                }

                return;
            }

            var newlyAddedItems = new List<IconSearchResultItemViewModel>();
            var skippedCount = 0;

            foreach (var candidate in candidates)
            {
                if (_viewModel.FindSelectedItem(candidate.Id) is not null)
                {
                    skippedCount++;
                    continue;
                }

                var item = GetOrCreateSelectableItem(candidate);
                ApplyConfiguredPrefixToItem(item);
                _viewModel.AddSelectedItem(item);
                newlyAddedItems.Add(item);
            }

            if (newlyAddedItems.Count == 0)
            {
                SetInfoStatus("当前页已全选", $"当前页识别到 {candidates.Count} 个图标，它们已经都在右侧导出列表中了。");
                return;
            }

            WarmUpItemDetails(newlyAddedItems);
            _viewModel.BrowserHint = $"当前页已批量加入 {newlyAddedItems.Count} 个图标。预览会在后台逐步补齐，你可以继续浏览或直接导出。";
            SetSuccessStatus(
                "批量选取完成",
                $"已将当前页 {newlyAddedItems.Count} 个图标加入右侧导出列表，跳过 {skippedCount} 个已存在图标。");
        }
        catch (Exception ex)
        {
            SetErrorStatus("批量选取失败", ex.Message);
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private void ApplyResourceKeyPrefix_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedItems.Count == 0)
        {
            SetInfoStatus("没有可更新的图标", "请先在右侧导出列表添加至少一个图标。");
            return;
        }

        var normalizedPrefix = NormalizeResourceKeyPrefix(_viewModel.ResourceKeyPrefix);
        _viewModel.ResourceKeyPrefix = FormatResourceKeyPrefixForInput(normalizedPrefix);

        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var updatedCount = 0;

        foreach (var item in _viewModel.SelectedItems)
        {
            var suffix = ExtractResourceKeySuffix(item.ResourceKey, item.Name);
            var targetKey = EnsureUniqueResourceKey(BuildResourceKey(normalizedPrefix, suffix), usedKeys);
            if (!string.Equals(item.ResourceKey, targetKey, StringComparison.Ordinal))
            {
                item.ResourceKey = targetKey;
                updatedCount++;
            }
        }

        var prefixSummary = string.IsNullOrWhiteSpace(normalizedPrefix)
            ? "已清空统一前缀"
            : $"当前统一前缀：{FormatResourceKeyPrefixForInput(normalizedPrefix)}";
        SetSuccessStatus("资源键前缀已更新", $"已更新 {updatedCount} 个图标的资源键。{prefixSummary}");
    }

    private void ImportLocalSvgFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SVG 文件 (*.svg)|*.svg|所有文件 (*.*)|*.*",
            Title = "选择一个或多个 SVG 文件",
            CheckFileExists = true,
            Multiselect = true,
            InitialDirectory = GetInitialDirectory(_viewModel.OutputPath)
        };

        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0)
        {
            return;
        }

        ImportLocalSvgFiles(dialog.FileNames);
    }

    private void ImportLocalSvgFiles(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
        {
            return;
        }

        _viewModel.IsBusy = true;
        SetInfoStatus("正在导入本地 SVG", $"准备读取 {filePaths.Count} 个本地 SVG 文件。");

        try
        {
            var newlyAddedItems = new List<IconSearchResultItemViewModel>();
            var skippedCount = 0;
            var issues = new List<string>();

            foreach (var rawPath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(rawPath);
                var existing = _viewModel.FindSelectedItemBySourceId(fullPath);
                if (existing is not null)
                {
                    _viewModel.ActiveResult = existing;
                    skippedCount++;
                    continue;
                }

                try
                {
                    var item = CreateLocalSvgItem(fullPath);
                    _viewModel.AddSelectedItem(item);
                    _viewModel.ActiveResult = item;
                    newlyAddedItems.Add(item);
                }
                catch (Exception ex)
                {
                    issues.Add($"{Path.GetFileName(fullPath)}: {ex.Message}");
                }
            }

            if (newlyAddedItems.Count == 0)
            {
                var issueSummary = issues.Count == 0
                    ? string.Empty
                    : $" 失败详情：{string.Join(" | ", issues.Take(3))}";

                SetInfoStatus(
                    "没有新增本地 SVG",
                    $"已跳过 {skippedCount} 个已存在文件。{issueSummary}".Trim());
                return;
            }

            _viewModel.BrowserHint = "本地 SVG 已加入右侧导出列表。你可以继续从 yesicon 挑选图标，也可以直接编辑资源键后导出。";
            var details = issues.Count == 0
                ? $"已导入 {newlyAddedItems.Count} 个本地 SVG，跳过 {skippedCount} 个已存在文件。右侧导出列表和预览已经更新。"
                : $"已导入 {newlyAddedItems.Count} 个本地 SVG，跳过 {skippedCount} 个已存在文件，失败 {issues.Count} 个。失败详情：{string.Join(" | ", issues.Take(3))}";

            SetSuccessStatus("本地 SVG 导入完成", details);
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private IconSearchResultItemViewModel CreateLocalSvgItem(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("未找到指定的 SVG 文件。", fullPath);
        }

        var svgContent = File.ReadAllText(fullPath);
        var displayName = Path.GetFileNameWithoutExtension(fullPath);
        var candidate = new IconSearchCandidate(
            BuildUniqueLocalItemId(fullPath),
            "local",
            string.IsNullOrWhiteSpace(displayName) ? "svg" : displayName,
            "本地 SVG");

        var item = new IconSearchResultItemViewModel(candidate)
        {
            SourceId = fullPath,
            ResourceKey = BuildUniqueLocalResourceKey(fullPath)
        };

        var preview = _exportService.CreatePreview(svgContent, item.ResourceKey);
        item.ApplyDetail(
            new IconDetail(
                candidate.Id,
                candidate.Prefix,
                candidate.Name,
                candidate.CollectionName,
                svgContent,
                null,
                null,
                null,
                null,
                null,
                []),
            preview);
        item.DetailStateLabel = "本地 SVG 已加载并生成预览";
        item.AuthorLabel = $"来源：{Path.GetFileName(fullPath)}";
        item.LicenseLabel = $"路径：{fullPath}";
        item.KeywordLabel = "关键词：本地导入";
        return item;
    }

    private string BuildUniqueLocalItemId(string filePath)
    {
        var baseName = SanitizeLocalSegment(Path.GetFileNameWithoutExtension(filePath), "svg");
        var parentName = SanitizeLocalSegment(Path.GetFileName(Path.GetDirectoryName(filePath)), "folder");

        var candidateId = $"local:{baseName}";
        if (_viewModel.FindSelectedItem(candidateId) is null)
        {
            return candidateId;
        }

        candidateId = $"local:{parentName}/{baseName}";
        if (_viewModel.FindSelectedItem(candidateId) is null)
        {
            return candidateId;
        }

        var suffix = 2;
        do
        {
            candidateId = $"local:{baseName}-{suffix}";
            suffix++;
        }
        while (_viewModel.FindSelectedItem(candidateId) is not null);

        return candidateId;
    }

    private string BuildUniqueLocalResourceKey(string filePath)
    {
        var normalizedPrefix = NormalizeResourceKeyPrefix(_viewModel.ResourceKeyPrefix);
        var prefix = string.IsNullOrWhiteSpace(normalizedPrefix) ? "local" : normalizedPrefix;
        var suffix = SanitizeLocalSegment(Path.GetFileNameWithoutExtension(filePath), "svg");
        var baseKey = BuildResourceKey(prefix, suffix);
        return EnsureUniqueResourceKey(
            baseKey,
            _viewModel.SelectedItems.Select(static item => item.ResourceKey));
    }

    private static string SanitizeLocalSegment(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidChar, '_');
        }

        candidate = candidate.Replace(' ', '-');
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
    }

    private void ApplyConfiguredPrefixToItem(IconSearchResultItemViewModel item)
    {
        var normalizedPrefix = NormalizeResourceKeyPrefix(_viewModel.ResourceKeyPrefix);
        if (string.IsNullOrWhiteSpace(normalizedPrefix))
        {
            return;
        }

        var suffix = ExtractResourceKeySuffix(item.ResourceKey, item.Name);
        item.ResourceKey = EnsureUniqueResourceKey(
            BuildResourceKey(normalizedPrefix, suffix),
            _viewModel.SelectedItems
                .Where(existing => !ReferenceEquals(existing, item))
                .Select(static existing => existing.ResourceKey));
    }

    private static string NormalizeResourceKeyPrefix(string? prefix)
    {
        return NormalizeResourceKeyPath(prefix);
    }

    private static string FormatResourceKeyPrefixForInput(string normalizedPrefix)
    {
        return string.IsNullOrWhiteSpace(normalizedPrefix)
            ? string.Empty
            : $"{normalizedPrefix}/";
    }

    private static string ExtractResourceKeySuffix(string? resourceKey, string? fallbackName)
    {
        var normalizedKey = NormalizeResourceKeyPath(resourceKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return SanitizeLocalSegment(fallbackName, "icon");
        }

        var segments = normalizedKey.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length <= 1)
        {
            return normalizedKey;
        }

        return string.Join('/', segments.Skip(1));
    }

    private static string BuildResourceKey(string prefix, string suffix)
    {
        var normalizedPrefix = NormalizeResourceKeyPath(prefix);
        var normalizedSuffix = NormalizeResourceKeyPath(suffix);
        if (string.IsNullOrWhiteSpace(normalizedSuffix))
        {
            normalizedSuffix = "icon";
        }

        return string.IsNullOrWhiteSpace(normalizedPrefix)
            ? normalizedSuffix
            : $"{normalizedPrefix}/{normalizedSuffix}";
    }

    private static string NormalizeResourceKeyPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            '/',
            value.Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string EnsureUniqueResourceKey(string baseKey, IEnumerable<string> existingKeys)
    {
        var usedKeys = new HashSet<string>(
            existingKeys
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .Select(static key => NormalizeResourceKeyPath(key)),
            StringComparer.OrdinalIgnoreCase);

        return EnsureUniqueResourceKey(baseKey, usedKeys);
    }

    private static string EnsureUniqueResourceKey(string baseKey, ISet<string> usedKeys)
    {
        var normalizedBaseKey = NormalizeResourceKeyPath(baseKey);
        if (string.IsNullOrWhiteSpace(normalizedBaseKey))
        {
            normalizedBaseKey = "icon";
        }

        if (usedKeys.Add(normalizedBaseKey))
        {
            return normalizedBaseKey;
        }

        var suffix = 2;
        while (true)
        {
            var candidateKey = $"{normalizedBaseKey}-{suffix}";
            if (usedKeys.Add(candidateKey))
            {
                return candidateKey;
            }

            suffix++;
        }
    }

    private void ClearSelectedIcons_Click(object sender, RoutedEventArgs e)
    {
        var selectedCount = _viewModel.SelectedItems.Count;
        if (selectedCount == 0)
        {
            SetInfoStatus("没有可清空的图标", "右侧导出列表当前已经是空的。");
            return;
        }

        _viewModel.ClearSelectedItems();
        _viewModel.BrowserHint = "右侧导出列表已清空，可以继续从当前页面重新挑选图标。";
        SetInfoStatus("已清空导出列表", $"已从右侧导出列表移除 {selectedCount} 个图标。");
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedItems.Count == 0)
        {
            SetErrorStatus("没有可导出的图标", "请先至少添加一个图标到导出列表。");
            return;
        }

        var duplicateKeys = _viewModel.SelectedItems
            .Select(static item => item.ResourceKey.Trim())
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(static key => key, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicateKeys.Length > 0)
        {
            SetErrorStatus("存在重复资源键", $"请先处理重复键：{string.Join(", ", duplicateKeys)}");
            return;
        }

        foreach (var item in _viewModel.SelectedItems)
        {
            if (string.IsNullOrWhiteSpace(item.ResourceKey))
            {
                SetErrorStatus("资源键为空", $"请先为图标 '{item.Id}' 填写资源键。");
                return;
            }
        }

        var outputPath = GetResolvedOutputPath();
        _viewModel.IsBusy = true;
        SetInfoStatus("正在导出", "正在补齐详情并生成 ResourceDictionary。");

        try
        {
            using var exportCancellation = new CancellationTokenSource();
            foreach (var item in _viewModel.SelectedItems)
            {
                await EnsureItemDetailsLoadedAsync(item, exportCancellation.Token);
            }

            var exportItems = _viewModel.SelectedItems
                .Select(item => new IconExportItem(item.ResourceKey.Trim(), item.SourceId, item.SvgContent))
                .ToArray();

            var summary = _exportService.Export(
                exportItems,
                outputPath,
                _viewModel.SelectedExportModeOption.Mode,
                conflict => ConflictResolutionWindow.Show(this, conflict));

            if (summary.WasCanceled)
            {
                SetInfoStatus("导出已取消", "资源键冲突处理被中止，未写入任何文件。");
                return;
            }

            var issueSummary = summary.Issues.Count == 0
                ? string.Empty
                : $" 失败详情：{string.Join(" | ", summary.Issues.Take(3))}";

            SetSuccessStatus(
                "导出完成",
                $"新增 {summary.Added} 项，覆盖 {summary.Overwritten} 项，跳过 {summary.Skipped} 项，失败 {summary.Failed} 项。输出：{Path.GetFullPath(outputPath)}。{issueSummary}".Trim());
        }
        catch (Exception ex)
        {
            SetErrorStatus("导出失败", ex.Message);
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private async void ExportSvgZip_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedItems.Count == 0)
        {
            SetErrorStatus("没有可下载的图标", "请先至少添加一个图标到导出列表。");
            return;
        }

        var zipOutputPath = BuildZipOutputPath(GetResolvedOutputPath());
        var dialog = new SaveFileDialog
        {
            Filter = "ZIP 压缩文件 (*.zip)|*.zip|所有文件 (*.*)|*.*",
            Title = "选择 SVG ZIP 路径",
            FileName = Path.GetFileName(zipOutputPath),
            InitialDirectory = GetInitialDirectory(zipOutputPath),
            DefaultExt = ".zip",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            SetInfoStatus("下载已取消", "没有生成 SVG ZIP 文件。");
            return;
        }

        _viewModel.IsBusy = true;
        SetInfoStatus("正在打包", "正在补齐详情并打包 SVG 文件 ZIP。");

        try
        {
            using var exportCancellation = new CancellationTokenSource();
            foreach (var item in _viewModel.SelectedItems)
            {
                await EnsureItemDetailsLoadedAsync(item, exportCancellation.Token);
            }

            var exportItems = _viewModel.SelectedItems
                .Select(item => new SvgZipExportItem(item.ResourceKey.Trim(), item.SourceId, item.SvgContent))
                .ToArray();

            var summary = _svgZipExportService.Export(exportItems, dialog.FileName);
            var renameSummary = summary.RenamedCount == 0
                ? string.Empty
                : $" 自动重命名 {summary.RenamedCount} 项。";

            SetSuccessStatus(
                "ZIP 已生成",
                $"已打包 {summary.EntryCount} 个 SVG 文件。输出：{summary.OutputPath}。{renameSummary}".Trim());
        }
        catch (Exception ex)
        {
            SetErrorStatus("打包 ZIP 失败", ex.Message);
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private void RemoveSelectedIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { CommandParameter: IconSearchResultItemViewModel item })
        {
            return;
        }

        _viewModel.RemoveSelectedItem(item);
        if (ReferenceEquals(_viewModel.ActiveResult, item))
        {
            _viewModel.ActiveResult = _viewModel.SelectedItems.FirstOrDefault();
        }

        SetInfoStatus("已移除图标", $"'{item.Id}' 已从右侧导出列表移除。");
    }

    private void BrowseOutputPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "XAML 资源字典 (*.xaml)|*.xaml|所有文件 (*.*)|*.*",
            Title = "选择输出 XAML 路径",
            FileName = Path.GetFileName(_viewModel.OutputPath),
            InitialDirectory = GetInitialDirectory(_viewModel.OutputPath)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _outputPathCustomized = true;
        _viewModel.OutputPath = dialog.FileName;
        SetInfoStatus("输出路径已更新", "后续导出会写入你刚刚选择的 XAML 文件。");
    }

    private void SearchQueryTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        NavigateToSearchPage();
    }

    private async void BrowserView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        UpdateBrowserButtons();

        if (!e.IsSuccess)
        {
            _viewModel.ActiveResult = null;
            _viewModel.CanImportCurrentIcon = false;
            _viewModel.BrowserHint = $"页面加载失败：{e.WebErrorStatus}";
            SetErrorStatus("页面加载失败", $"yesicon 页面未能正常加载：{e.WebErrorStatus}");
            return;
        }

        await SyncCurrentBrowserIconAsync(GetCurrentBrowserUri());
        if (BrowserView.CoreWebView2 is not null)
        {
            await BrowserView.ExecuteScriptAsync("window.__colorfulSvgHideAds && window.__colorfulSvgHideAds();");
        }

        if (_browserPickModeEnabled)
        {
            await SetBrowserPickModeAsync(true);
        }
    }

    private void CoreWebView2_HistoryChanged(object? sender, object e)
    {
        UpdateBrowserButtons();
        _ = SyncCurrentBrowserIconAsync(GetCurrentBrowserUri());
    }

    private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        UpdateBrowserButtons();
        PersistBrowserSessionState();
        _ = SyncCurrentBrowserIconAsync(GetCurrentBrowserUri());
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var rawMessage = e.TryGetWebMessageAsString();
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<BrowserMessage>(rawMessage, BrowserJsonOptions);
            if (string.Equals(payload?.Type, "iconbox-sync", StringComparison.Ordinal))
            {
                var raw = payload?.Raw;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    SyncPickedIconsFromIconBox(raw);
                }

                return;
            }

            var href = payload?.Href;
            if (!string.Equals(payload?.Type, "preview-icon", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(href))
            {
                return;
            }

            if (Uri.TryCreate(new Uri("https://yesicon.app"), href, out var iconUri))
            {
                _ = SyncCurrentBrowserIconAsync(iconUri);
            }
        }
        catch
        {
        }
    }

    private void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (BrowserView.CoreWebView2 is null ||
            e.ResourceContext != CoreWebView2WebResourceContext.Script ||
            !IsBlockedAdScriptRequest(e.Request.Uri))
        {
            return;
        }

        var response = BrowserView.CoreWebView2.Environment.CreateWebResourceResponse(
            new MemoryStream(Array.Empty<byte>()),
            200,
            "OK",
            "Content-Type: application/javascript; charset=utf-8");

        e.Response = response;
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Uri))
        {
            return;
        }

        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (!TryCreateIconCandidate(uri, out _))
        {
            return;
        }

        e.Handled = true;
        _ = SyncCurrentBrowserIconAsync(uri);
        SetInfoStatus("快速预览", "已拦截 Ctrl+点击，正在右侧预览该图标。");
    }

    private void NavigateToSearchPage()
    {
        if (!_browserInitialized)
        {
            return;
        }

        var query = string.IsNullOrWhiteSpace(_viewModel.SearchQuery) ? "folder" : _viewModel.SearchQuery.Trim();
        if (!_outputPathCustomized)
        {
            _viewModel.OutputPath = IconSearcherViewModel.BuildSuggestedOutputPath(query);
        }

        BrowserView.Source = new Uri($"https://yesicon.app/search?query={Uri.EscapeDataString(query)}&lang=en");
        _viewModel.BrowserHint = $"正在打开 yesicon 搜索页：{query}。普通点击查看详情，按住 Ctrl 可直接选图并同步到右侧。";
        SetInfoStatus("正在打开搜索页", $"浏览器已导航到 yesicon，关键词：{query}。");
    }

    private async Task SyncCurrentBrowserIconAsync(Uri? uri)
    {
        _browserNavigationCancellationTokenSource?.Cancel();
        _browserNavigationCancellationTokenSource?.Dispose();
        _browserNavigationCancellationTokenSource = new CancellationTokenSource();

        if (!TryCreateIconCandidate(uri, out var candidate))
        {
            _viewModel.ActiveResult = null;
            _viewModel.CanImportCurrentIcon = false;
            _viewModel.BrowserHint = "当前页面是搜索页或图标库页。普通点击进入详情，按住 Ctrl 可直接点选图标并同步到右侧。";
            return;
        }

        var currentItem = _viewModel.FindSelectedItem(candidate.Id) ?? new IconSearchResultItemViewModel(candidate);
        _viewModel.ActiveResult = currentItem;
        _viewModel.CanImportCurrentIcon = false;
        _viewModel.BrowserHint = $"已定位图标 {candidate.Id}，正在加载 SVG 和预览。";

        try
        {
            await EnsureItemDetailsLoadedAsync(currentItem, _browserNavigationCancellationTokenSource.Token);
            _viewModel.CanImportCurrentIcon = true;
            _viewModel.BrowserHint = $"当前预览图标：{currentItem.Id}。可以添加到导出列表，或继续在左侧浏览其他图标。";
            SetSuccessStatus("图标详情已就绪", $"当前页面图标为 '{currentItem.Id}'，预览和导出数据已准备完成。");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _viewModel.CanImportCurrentIcon = false;
            _viewModel.BrowserHint = $"当前图标详情加载失败：{ex.Message}";
            SetErrorStatus("图标详情加载失败", ex.Message);
        }
    }

    private Uri? GetCurrentBrowserUri()
    {
        var source = BrowserView.CoreWebView2?.Source;
        if (string.IsNullOrWhiteSpace(source))
        {
            return BrowserView.Source;
        }

        return Uri.TryCreate(source, UriKind.Absolute, out var uri) ? uri : BrowserView.Source;
    }

    private async Task<IReadOnlyList<IconSearchCandidate>> GetCurrentPageIconCandidatesAsync()
    {
        if (BrowserView.CoreWebView2 is null)
        {
            return [];
        }

        const string script = """
            (() => {
              const ignoredPrefixes = new Set(['search', 'api', '_nuxt', 'favicon']);
              const ids = [];
              const seen = new Set();

              const isVisible = (element) => {
                if (!(element instanceof HTMLElement)) {
                  return false;
                }

                const rect = element.getBoundingClientRect();
                if (rect.width <= 0 || rect.height <= 0) {
                  return false;
                }

                const style = window.getComputedStyle(element);
                return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0';
              };

              for (const anchor of document.querySelectorAll('a[href]')) {
                if (!isVisible(anchor)) {
                  continue;
                }

                let url;
                try {
                  url = new URL(anchor.getAttribute('href') || '', window.location.origin);
                } catch {
                  continue;
                }

                if (url.origin !== window.location.origin) {
                  continue;
                }

                const parts = url.pathname
                  .split('/')
                  .filter(Boolean)
                  .map(part => decodeURIComponent(part));

                if (parts.length !== 2) {
                  continue;
                }

                const [prefix, name] = parts;
                if (!prefix || !name || ignoredPrefixes.has(prefix)) {
                  continue;
                }

                const id = `${prefix}:${name}`;
                if (seen.has(id)) {
                  continue;
                }

                seen.add(id);
                ids.push(id);
              }

              return ids;
            })();
            """;

        var raw = await BrowserView.ExecuteScriptAsync(script);
        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var iconIds = JsonSerializer.Deserialize<List<string>>(raw, BrowserJsonOptions) ?? [];
        return iconIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(CreateCandidateFromId)
            .ToArray();
    }

    private IconSearchResultItemViewModel GetOrCreateSelectableItem(IconSearchCandidate candidate)
    {
        var selectedItem = _viewModel.FindSelectedItem(candidate.Id);
        if (selectedItem is not null)
        {
            return selectedItem;
        }

        if (_viewModel.ActiveResult is { } activeResult &&
            string.Equals(activeResult.Id, candidate.Id, StringComparison.Ordinal))
        {
            return activeResult;
        }

        return new IconSearchResultItemViewModel(candidate);
    }

    private void WarmUpItemDetails(IEnumerable<IconSearchResultItemViewModel> items)
    {
        foreach (var item in items)
        {
            if (item.HasLoadedDetails || item.DetailsTask is { IsCompleted: false })
            {
                continue;
            }

            _ = WarmUpItemDetailsAsync(item);
        }
    }

    private async Task WarmUpItemDetailsAsync(IconSearchResultItemViewModel item)
    {
        try
        {
            await EnsureItemDetailsLoadedAsync(item, CancellationToken.None);
        }
        catch
        {
        }
    }

    private async Task<BrowserHitTestResult?> HitTestIconLinkAsync(double x, double y)
    {
        if (BrowserView.CoreWebView2 is null)
        {
            return null;
        }

        var script = $$"""
            (() => {
              const x = {{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
              const y = {{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
              const node = document.elementFromPoint(x, y);
              if (!node) {
                return null;
              }

              let current = node;
              while (current) {
                if (current instanceof HTMLAnchorElement && current.getAttribute('href')) {
                  return JSON.stringify({
                    href: current.getAttribute('href'),
                    text: (current.textContent || '').trim()
                  });
                }

                if (typeof current.getAttribute === 'function') {
                  const href = current.getAttribute('href') || current.getAttribute('data-href');
                  if (href) {
                    return JSON.stringify({
                      href,
                      text: (current.textContent || '').trim()
                    });
                  }
                }

                current = current.parentElement;
              }

              return null;
            })();
            """;

        var raw = await BrowserView.ExecuteScriptAsync(script);
        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var json = JsonSerializer.Deserialize<string>(raw, BrowserJsonOptions);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<BrowserHitTestResult>(json, BrowserJsonOptions);
    }

    private async Task SetBrowserPickModeAsync(bool enabled)
    {
        if (!_browserInitialized || BrowserView.CoreWebView2 is null)
        {
            return;
        }

        if (enabled)
        {
            var snapshot = await BrowserView.ExecuteScriptAsync("window.localStorage.getItem('iconBox');");
            InitializeKnownPickedIcons(snapshot);
        }

        await BrowserView.ExecuteScriptAsync($"window.__colorfulSvgSetPickMode({(enabled ? "true" : "false")});");
        _browserPickModeEnabled = enabled;
        _viewModel.BrowserHint = enabled
            ? "快速选取模式已开启。保持按住 Ctrl，直接左键点图标即可同步到右侧。"
            : "快速选取模式已关闭。普通点击进入详情页，按住 Ctrl 可临时开启快速选取。";
    }

    private void InitializeKnownPickedIcons(string? rawValue)
    {
        var decoded = System.Text.Json.JsonSerializer.Deserialize<string>(rawValue ?? "null");
        if (string.IsNullOrWhiteSpace(decoded))
        {
            return;
        }

        try
        {
            var boxes = JsonSerializer.Deserialize<List<BrowserIconBox>>(decoded, BrowserJsonOptions);
            _browserKnownPickedIconIds.Clear();
            foreach (var box in boxes ?? [])
            {
                foreach (var icon in box.Icons ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(icon.Id))
                    {
                        _browserKnownPickedIconIds.Add(icon.Id);
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void SyncPickedIconsFromIconBox(string rawValue)
    {
        try
        {
            var boxes = JsonSerializer.Deserialize<List<BrowserIconBox>>(rawValue, BrowserJsonOptions);
            if (boxes is null)
            {
                return;
            }

            foreach (var box in boxes)
            {
                foreach (var icon in box.Icons ?? [])
                {
                    if (string.IsNullOrWhiteSpace(icon.Id) ||
                        string.IsNullOrWhiteSpace(icon.Svg) ||
                        !_browserKnownPickedIconIds.Add(icon.Id))
                    {
                        continue;
                    }

                    var candidate = CreateCandidateFromId(icon.Id);
                    var item = _viewModel.FindSelectedItem(icon.Id) ?? new IconSearchResultItemViewModel(candidate);
                    ApplyConfiguredPrefixToItem(item);
                    var preview = _exportService.CreatePreview(icon.Svg, item.ResourceKey);
                    item.ApplyDetail(
                        new IconDetail(
                            candidate.Id,
                            candidate.Prefix,
                            candidate.Name,
                            candidate.CollectionName,
                            icon.Svg,
                            null,
                            null,
                            null,
                            null,
                            null,
                            []),
                        preview);

                    _viewModel.ActiveResult = item;
                    _viewModel.AddSelectedItem(item);
                    SetSuccessStatus("已选中图标", $"'{item.Id}' 已通过快速选取加入右侧导出列表。");
                }
            }
        }
        catch (Exception ex)
        {
            SetErrorStatus("同步选取结果失败", ex.Message);
        }
    }

    private async Task EnsureItemDetailsLoadedAsync(IconSearchResultItemViewModel item, CancellationToken cancellationToken)
    {
        if (item.HasLoadedDetails && !string.IsNullOrWhiteSpace(item.SvgContent))
        {
            return;
        }

        if (item.DetailsTask is { IsCompleted: false } existingTask)
        {
            await existingTask;
        }
        else
        {
            item.DetailsTask = LoadItemDetailsCoreAsync(item, cancellationToken);
            await item.DetailsTask;
        }

        if (!item.HasLoadedDetails || string.IsNullOrWhiteSpace(item.SvgContent))
        {
            throw new InvalidOperationException($"图标 '{item.Id}' 的详情尚未成功加载。");
        }
    }

    private async Task LoadItemDetailsCoreAsync(IconSearchResultItemViewModel item, CancellationToken cancellationToken)
    {
        item.IsLoadingDetails = true;
        item.DetailStateLabel = "正在加载 SVG 与预览...";

        try
        {
            var detail = await _yesIconClient.GetIconDetailAsync(item.Candidate, cancellationToken);
            var preview = _exportService.CreatePreview(detail.SvgContent, item.ResourceKey);
            item.ApplyDetail(detail, preview);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            item.ApplyLoadError($"详情加载失败：{ex.Message}");
        }
    }

    private void UpdateBrowserButtons()
    {
        if (!_browserInitialized)
        {
            BrowserBackButton.IsEnabled = false;
            BrowserForwardButton.IsEnabled = false;
            return;
        }

        BrowserBackButton.IsEnabled = BrowserView.CanGoBack;
        BrowserForwardButton.IsEnabled = BrowserView.CanGoForward;
    }

    private static bool TryCreateIconCandidate(Uri? uri, out IconSearchCandidate candidate)
    {
        candidate = null!;
        if (uri is null || !string.Equals(uri.Host, "yesicon.app", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        var prefix = Uri.UnescapeDataString(segments[0]);
        var name = Uri.UnescapeDataString(segments[1]);
        if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (string.Equals(prefix, "search", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(prefix, "api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        candidate = new IconSearchCandidate($"{prefix}:{name}", prefix, name, prefix);
        return true;
    }

    private static IconSearchCandidate CreateCandidateFromId(string iconId)
    {
        var separatorIndex = iconId.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= iconId.Length - 1)
        {
            return new IconSearchCandidate(iconId, "icon", iconId, "icon");
        }

        var prefix = iconId[..separatorIndex];
        var name = iconId[(separatorIndex + 1)..];
        return new IconSearchCandidate(iconId, prefix, name, prefix);
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

    private string GetResolvedOutputPath()
    {
        var outputPath = string.IsNullOrWhiteSpace(_viewModel.OutputPath)
            ? IconSearcherViewModel.BuildSuggestedOutputPath(_viewModel.SearchQuery)
            : _viewModel.OutputPath;

        _viewModel.OutputPath = outputPath;
        return outputPath;
    }

    private static string BuildZipOutputPath(string outputPath)
    {
        return Path.ChangeExtension(Path.GetFullPath(outputPath), ".zip");
    }

    private void SetInfoStatus(string title, string message)
    {
        ApplyStatus(title, message, IconSearcherViewModel.InfoBrush, IconSearcherViewModel.InfoBackgroundBrush);
    }

    private void SetSuccessStatus(string title, string message)
    {
        ApplyStatus(title, message, IconSearcherViewModel.SuccessBrush, IconSearcherViewModel.SuccessBackgroundBrush);
    }

    private void SetErrorStatus(string title, string message)
    {
        ApplyStatus(title, message, IconSearcherViewModel.ErrorBrush, IconSearcherViewModel.ErrorBackgroundBrush);
    }

    private void ApplyStatus(string title, string message, Brush foreground, Brush background)
    {
        _viewModel.StatusTitle = title;
        _viewModel.StatusMessage = message;
        _viewModel.StatusBrush = foreground;
        _viewModel.StatusBackground = background;
    }

    private sealed class BrowserMessage
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("href")]
        public string? Href { get; init; }

        [JsonPropertyName("raw")]
        public string? Raw { get; init; }
    }

    private sealed class BrowserHitTestResult
    {
        [JsonPropertyName("href")]
        public string? Href { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class BrowserIconBox
    {
        [JsonPropertyName("icons")]
        public List<BrowserPickedIcon>? Icons { get; init; }
    }

    private sealed class BrowserPickedIcon
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("svg")]
        public string? Svg { get; init; }
    }

    private sealed class BrowserSessionState
    {
        [JsonPropertyName("lastBrowserUri")]
        public string? LastBrowserUri { get; init; }

        [JsonPropertyName("searchQuery")]
        public string? SearchQuery { get; init; }
    }
}
