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
    /// 阻止鼠标事件继续向子元素隧道传递，
    /// 辅助 MainWindow 层的高级拦截（NavigationView.PreviewMouseLeftButtonDown）。
    /// </summary>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled)
            return;

        var source = e.OriginalSource as DependencyObject;
        while (source != null)
        {
            if (source is ButtonBase or TextBox or ComboBox or ToggleButton
                or ListBoxItem or ScrollBar or Wpf.Ui.Controls.TextBox)
                return;

            source = VisualTreeHelper.GetParent(source);
        }

        e.Handled = true;
    }
}
