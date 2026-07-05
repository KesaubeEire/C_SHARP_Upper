using System.Windows.Controls;
using WpfScada.ControlsLookup;
using WpfScada.ViewModels.Pages.Plc;

namespace WpfScada.Views.Pages.Plc;

[GalleryPage("Modbus 调试", Wpf.Ui.Controls.SymbolRegular.PlugDisconnected24)]
public partial class ModbusPage : Page
{
    private readonly ModbusViewModel _vm;

    public ModbusPage(ModbusViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;
    }

    private void OnFuncCodeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item
            && item.Tag is string tag && byte.TryParse(tag, out byte code))
        {
            _vm.FuncCode = code;
        }
    }
}
