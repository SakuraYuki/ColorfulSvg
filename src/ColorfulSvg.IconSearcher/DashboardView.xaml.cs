using System.Windows.Controls;

namespace ColorfulSvg.IconSearcher;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    public Button OpenIconSearchButtonControl => OpenIconSearchButton;
    public Button OpenResourceBrowserButtonControl => OpenResourceBrowserButton;
    public Button OpenSvgDirectoryBrowserButtonControl => OpenSvgDirectoryBrowserButton;
    public Button ShowHelpButtonControl => ShowHelpButton;
}
