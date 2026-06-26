using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Gallery.ViewModels.Pages.Plc;

namespace Wpf.Ui.Gallery.Controls.Plc;

public partial class AlarmDetailPanel : UserControl
{
    public AlarmDetailPanel()
    {
        InitializeComponent();
    }

    private AlarmViewModel ViewModel => (AlarmViewModel)DataContext;

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedAlarm = null;
    }

    private void OnBackdropMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender || e.OriginalSource is Border)
        {
            ViewModel.SelectedAlarm = null;
        }
    }
}
