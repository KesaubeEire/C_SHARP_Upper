using System.Windows;
using System.Windows.Controls;
using WpfScada.ViewModels.Pages.Plc;

namespace WpfScada.Controls.Plc;

public partial class AlarmToolbar : UserControl
{
    public AlarmToolbar()
    {
        InitializeComponent();
    }

    private AlarmViewModel ViewModel => (AlarmViewModel)DataContext;

    private void OnOpenAckAllFlyout(object sender, RoutedEventArgs e)
    {
        ViewModel.IsAckAllFlyoutOpen = true;
    }

    private void OnOpenShelveAllFlyout(object sender, RoutedEventArgs e)
    {
        ViewModel.IsShelveAllFlyoutOpen = true;
    }

    private void OnCloseAckAllFlyout(object sender, RoutedEventArgs e)
    {
        ViewModel.IsAckAllFlyoutOpen = false;
    }

    private void OnCloseShelveAllFlyout(object sender, RoutedEventArgs e)
    {
        ViewModel.IsShelveAllFlyoutOpen = false;
    }
}
