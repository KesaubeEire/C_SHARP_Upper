using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Controls.Plc;

public partial class ByteRow : UserControl
{
    public ByteRow() => InitializeComponent();

    public event EventHandler<BitToggledEventArgs>? BitToggled;

    private void OnBitBlockToggled(object sender, BitToggledEventArgs e)
    {
        BitToggled?.Invoke(this, e);
    }
}
