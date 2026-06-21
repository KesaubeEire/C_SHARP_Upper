using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TestWpf.Models;

namespace TestWpf.Controls.Common;

/// <summary>
/// 位块组件 — 显示 0/1，支持点击切换，触发 Toggled 事件交由父级处理写入
/// </summary>
public partial class BitBlock : UserControl
{
    public BitBlock() => InitializeComponent();

    /// <summary>
    /// 点击位时触发，事件参数携带 BitViewModel
    /// </summary>
    public event EventHandler<BitToggledEventArgs>? Toggled;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not BitViewModel bit || bit.IsReadOnly) return;
        bit.Toggle();
        Toggled?.Invoke(this, new BitToggledEventArgs(bit));
    }
}

public class BitToggledEventArgs : EventArgs
{
    public BitViewModel Bit { get; }
    public BitToggledEventArgs(BitViewModel bit) => Bit = bit;
}
