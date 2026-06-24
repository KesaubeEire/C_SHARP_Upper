using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
    }

    /// <summary>
    /// 阻止鼠标事件冒泡到父级 NavigationViewItem，
    /// 避免点击本控件的非交互区域触发侧边栏菜单折叠。
    /// 从 e.OriginalSource 沿视觉树向上查找交互控件，
    /// 防止误拦截按钮文字/图标等子元素的点击。
    /// </summary>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 沿视觉树向上查找，点击落在交互控件内部时放行
        var source = e.OriginalSource as DependencyObject;
        while (source != null)
        {
            if (source is ButtonBase or TextBox or ComboBox or ToggleButton
                or ListBoxItem or ScrollBar)
                return;

            source = VisualTreeHelper.GetParent(source);
        }

        // 点击的是空白区域或纯布局容器 → 阻止冒泡
        e.Handled = true;
    }
}
