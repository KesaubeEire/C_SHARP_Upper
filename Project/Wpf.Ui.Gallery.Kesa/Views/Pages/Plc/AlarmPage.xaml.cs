using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

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

    private AlarmViewModel ViewModel => (AlarmViewModel)DataContext;

    /// <summary>
    /// 点击详情面板的关闭按钮 → 取消选中报警
    /// </summary>
    private void OnCloseDetailClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedAlarm = null;
    }

    /// <summary>
    /// 点击遮罩背景 → 取消选中报警（关闭面板）
    /// </summary>
    private void OnDetailBackdropMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 只响应直接点击遮罩本身，不响应点击面板内部冒泡上来的事件
        if (e.OriginalSource == sender || e.OriginalSource is Border)
        {
            ViewModel.SelectedAlarm = null;
            e.Handled = true;
        }
    }
}
