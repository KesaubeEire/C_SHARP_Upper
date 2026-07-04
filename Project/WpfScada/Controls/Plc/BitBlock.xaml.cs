using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfScada.Models.Plc;

namespace WpfScada.Controls.Plc;

public partial class BitBlock : UserControl
{
    public BitBlock() => InitializeComponent();

    public event EventHandler<BitToggledEventArgs>? Toggled;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is BitViewModel bvm)
            Toggled?.Invoke(this, new BitToggledEventArgs(bvm));
    }
}

public class BitToggledEventArgs(BitViewModel bit) : EventArgs
{
    public BitViewModel Bit { get; } = bit;
}
