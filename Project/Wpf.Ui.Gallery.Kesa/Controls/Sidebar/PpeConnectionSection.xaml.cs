using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Ui.Gallery.Services.Plc;
using Wpf.Ui.Gallery.ViewModels.Plc;

namespace Wpf.Ui.Gallery.Controls.Sidebar;

public partial class PpeConnectionSection : UserControl
{
    public PpeConnectionSectionViewModel ViewModel { get; }

    public PpeConnectionSection(S7Service s7, AppConfigService config, PollingScheduler scheduler)
    {
        ViewModel = new PpeConnectionSectionViewModel(s7, scheduler, config);
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) => ViewModel.OnLoaded();
    }

    /// <summary>
    /// 阻止鼠标事件冒泡到父级 NavigationViewItem，
    /// 避免点击本控件的非交互区域触发侧边栏菜单折叠。
    /// </summary>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is ButtonBase or TextBox or ComboBox or ToggleButton)
            return;
        e.Handled = true;
    }
}
