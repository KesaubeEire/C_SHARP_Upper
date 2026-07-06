using System.Windows.Controls;
using WpfScada.ControlsLookup;
using WpfScada.ViewModels.Pages.Plc;

namespace WpfScada.Views.Pages.Plc;

[GalleryPage("运动控制", Wpf.Ui.Controls.SymbolRegular.Board24)]
public partial class MotionPage : Page
{
    public MotionPage(MotionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
