using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using WpfScada.ControlsLookup;
using WpfScada.Models.Plc;
using WpfScada.ViewModels.Pages.Plc;

namespace WpfScada.Views.Pages.Plc;

/// <summary>
/// Interaction logic for AlarmPage.xaml
/// </summary>
[GalleryPage("报警管理", SymbolRegular.Alert24)]
public partial class AlarmPage : Page
{
    public AlarmPage(AlarmViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnOpenConfirmFlyout(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AlarmItem item })
            item.IsConfirmFlyoutOpen = true;
    }

    private void OnOpenShelveFlyout(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AlarmItem item })
            item.IsShelveFlyoutOpen = true;
    }

    private void OnCloseConfirmFlyout(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AlarmItem item })
            item.IsConfirmFlyoutOpen = false;
    }

    private void OnCloseShelveFlyout(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AlarmItem item })
            item.IsShelveFlyoutOpen = false;
    }
}
