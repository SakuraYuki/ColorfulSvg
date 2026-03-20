using System.Windows;

namespace ColorfulSvg.IconSearcher;

public partial class ConflictResolutionWindow : Window
{
    public ConflictResolutionWindow(ResourceConflict conflict)
    {
        InitializeComponent();
        DataContext = conflict;
    }

    public ConflictResolution Resolution { get; private set; } = ConflictResolution.Cancel;

    public static ConflictResolution Show(Window owner, ResourceConflict conflict)
    {
        var dialog = new ConflictResolutionWindow(conflict)
        {
            Owner = owner
        };

        dialog.ShowDialog();
        return dialog.Resolution;
    }

    private void Overwrite_Click(object sender, RoutedEventArgs e)
    {
        Resolution = ConflictResolution.Overwrite;
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Resolution = ConflictResolution.Skip;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Resolution = ConflictResolution.Cancel;
        Close();
    }

    private void OverwriteAll_Click(object sender, RoutedEventArgs e)
    {
        Resolution = ConflictResolution.OverwriteAll;
        Close();
    }

    private void SkipAll_Click(object sender, RoutedEventArgs e)
    {
        Resolution = ConflictResolution.SkipAll;
        Close();
    }
}
