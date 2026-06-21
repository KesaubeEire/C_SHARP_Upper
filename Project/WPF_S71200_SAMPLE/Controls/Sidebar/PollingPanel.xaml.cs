using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TestWpf.Services;

namespace TestWpf.Controls.Sidebar;

/// <summary>
/// 轮询配置面板 — 间隔输入、启停按钮、延迟显示、状态
/// </summary>
public partial class PollingPanel : UserControl
{
    public PollingPanel() => InitializeComponent();

    public int Interval => int.TryParse(txtInterval.Text.Trim(), out int v) ? v : 50;

    /// <summary>启动轮询请求（MainWindow 处理实际的地址收集和启动）</summary>
    public event EventHandler? StartRequested;
    /// <summary>停止轮询请求</summary>
    public event EventHandler? StopRequested;

    private void OnStartPoll(object sender, RoutedEventArgs e)
    {
        StartRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnStopPoll(object sender, RoutedEventArgs e)
    {
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetRunning(bool running)
    {
        btnStartPoll.IsEnabled = !running;
        btnStopPoll.IsEnabled = running;
        txtPollStatus.Text = running ? "● 轮询中" : "■ 已停止";
        txtPollStatus.Foreground = new SolidColorBrush(
            running ? Color.FromRgb(0x27, 0xAE, 0x60) : Color.FromRgb(0xE7, 0x4C, 0x3C));
    }

    public void SetReady()
    {
        btnStartPoll.IsEnabled = true;
        btnStopPoll.IsEnabled = false;
        txtPollStatus.Text = "就绪";
        txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    }

    public void UpdateLatency(long ms)
    {
        txtLatency.Text = $"{ms}ms";
    }
}
