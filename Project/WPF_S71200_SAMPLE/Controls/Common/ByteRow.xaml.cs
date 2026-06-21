using System.Windows;
using System.Windows.Controls;
using TestWpf.Models;

namespace TestWpf.Controls.Common;

/// <summary>
/// 字节行组件 — 地址标签 + 8个 BitBlock + HEX 值 + 已修改状态
/// 转发 BitBlock.Toggled 事件给父级
/// </summary>
public partial class ByteRow : UserControl
{
    public ByteRow() => InitializeComponent();

    /// <summary>子 BitBlock 被点击时向上转发</summary>
    public event EventHandler<BitToggledEventArgs>? BitToggled;

    private void OnBitToggled(object sender, BitToggledEventArgs e)
    {
        // Parent 是 ItemsControl 的 ItemTemplate 容器，逐级冒泡到 AreaPanel
        BitToggled?.Invoke(this, e);
    }
}
