using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;

namespace ColorfulSvg.IconSearcher;

public partial class IconSearchView : UserControl
{
    public event RoutedEventHandler? SearchRequested;
    public event RoutedEventHandler? BrowserBackRequested;
    public event RoutedEventHandler? BrowserForwardRequested;
    public event RoutedEventHandler? BrowserRefreshRequested;
    public event RoutedEventHandler? WorkspaceToggleRequested;
    public event RoutedEventHandler? ClearSelectedIconsRequested;
    public event RoutedEventHandler? ImportCurrentIconRequested;
    public event RoutedEventHandler? ImportVisibleIconsRequested;
    public event RoutedEventHandler? ImportLocalSvgFilesRequested;
    public event RoutedEventHandler? ApplyResourceKeyPrefixRequested;
    public event RoutedEventHandler? BrowseOutputPathRequested;
    public event RoutedEventHandler? ExportRequested;
    public event RoutedEventHandler? ExportSvgZipRequested;
    internal event EventHandler<IconSearchItemEventArgs>? RemoveSelectedIconRequested;
    public event MouseButtonEventHandler? BrowserPreviewMouseLeftButtonDownRequested;

    public IconSearchView()
    {
        InitializeComponent();
    }

    public TextBox SearchQueryTextBoxControl => SearchQueryTextBox;
    public WebView2 BrowserViewControl => BrowserView;
    public Button BrowserBackButtonControl => BrowserBackButton;
    public Button BrowserForwardButtonControl => BrowserForwardButton;
    public Button WorkspaceToggleButtonControl => WorkspaceToggleButton;
    public Button WorkspacePeekButtonControl => WorkspacePeekButton;
    public GridSplitter WorkspaceSplitterControl => WorkspaceSplitter;
    public Border WorkspacePanelControl => WorkspacePanel;
    public ColumnDefinition WorkspaceSpacerColumnDefinition => WorkspaceSpacerColumn;
    public ColumnDefinition WorkspaceColumnDefinition => WorkspaceColumn;

    private void SearchButton_Click(object sender, RoutedEventArgs e) => SearchRequested?.Invoke(this, e);
    private void BrowserBackButton_Click(object sender, RoutedEventArgs e) => BrowserBackRequested?.Invoke(this, e);
    private void BrowserForwardButton_Click(object sender, RoutedEventArgs e) => BrowserForwardRequested?.Invoke(this, e);
    private void BrowserRefreshButton_Click(object sender, RoutedEventArgs e) => BrowserRefreshRequested?.Invoke(this, e);
    private void WorkspaceToggleButton_Click(object sender, RoutedEventArgs e) => WorkspaceToggleRequested?.Invoke(this, e);
    private void WorkspacePeekButton_Click(object sender, RoutedEventArgs e) => WorkspaceToggleRequested?.Invoke(this, e);
    private void ClearSelectedIconsButton_Click(object sender, RoutedEventArgs e) => ClearSelectedIconsRequested?.Invoke(this, e);
    private void CollapseWorkspaceButton_Click(object sender, RoutedEventArgs e) => WorkspaceToggleRequested?.Invoke(this, e);
    private void ImportCurrentIconButton_Click(object sender, RoutedEventArgs e) => ImportCurrentIconRequested?.Invoke(this, e);
    private void ImportVisibleIconsButton_Click(object sender, RoutedEventArgs e) => ImportVisibleIconsRequested?.Invoke(this, e);
    private void ImportLocalSvgFilesButton_Click(object sender, RoutedEventArgs e) => ImportLocalSvgFilesRequested?.Invoke(this, e);
    private void ApplyResourceKeyPrefixButton_Click(object sender, RoutedEventArgs e) => ApplyResourceKeyPrefixRequested?.Invoke(this, e);
    private void BrowseOutputPathButton_Click(object sender, RoutedEventArgs e) => BrowseOutputPathRequested?.Invoke(this, e);
    private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportRequested?.Invoke(this, e);
    private void ExportSvgZipButton_Click(object sender, RoutedEventArgs e) => ExportSvgZipRequested?.Invoke(this, e);

    private void SearchQueryTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        SearchRequested?.Invoke(this, new RoutedEventArgs());
    }

    private void BrowserView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BrowserPreviewMouseLeftButtonDownRequested?.Invoke(this, e);
    }

    private void RemoveSelectedIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: IconSearchResultItemViewModel item })
        {
            return;
        }

        RemoveSelectedIconRequested?.Invoke(this, new IconSearchItemEventArgs(item));
    }
}

internal sealed class IconSearchItemEventArgs(IconSearchResultItemViewModel item) : EventArgs
{
    public IconSearchResultItemViewModel Item { get; } = item;
}
