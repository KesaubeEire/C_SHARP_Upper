using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Controls.Plc;

public partial class BitBlock : UserControl
{
    public BitBlock() => InitializeComponent();

    public event EventHandler<BitToggledEventArgs>? Toggled;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is BitViewModel bvm)
        {
            bvm.Toggle();
            Toggled?.Invoke(this, new BitToggledEventArgs(bvm));
        }
    }
}

public class BitToggledEventArgs(BitViewModel bit) : EventArgs
{
    public BitViewModel Bit { get; } = bit;
}
