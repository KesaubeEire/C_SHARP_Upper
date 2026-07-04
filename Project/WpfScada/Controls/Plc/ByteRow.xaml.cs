using System.Windows;
using System.Windows.Controls;
using WpfScada.Models.Plc;

namespace WpfScada.Controls.Plc;

public partial class ByteRow : UserControl
{
    public ByteRow() => InitializeComponent();

    public event EventHandler<BitToggledEventArgs>? BitToggled;

    private void OnBitBlockToggled(object sender, BitToggledEventArgs e)
    {
        BitToggled?.Invoke(this, e);
    }
}
