using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class PollingPage : Page
{
    private readonly S7Service _s7;
    private readonly PollingScheduler _scheduler;
    private readonly IoMonitorPage _ioMonitor;
    private readonly ConnectionPage _connection;

    public int Interval => int.TryParse(intervalInput.Text, out int v) ? v : 500;

    public PollingPage(S7Service s7, PollingScheduler scheduler, IoMonitorPage ioMonitor, ConnectionPage connection)
    {
        _s7 = s7;
        _scheduler = scheduler;
        _ioMonitor = ioMonitor;
        _connection = connection;
        InitializeComponent();
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        if (!_s7.IsConnected)
        {
            MessageBox.Show("请先连接 PLC", "提示");
            return;
        }

        _scheduler.Config.FastInterval = Interval;
        _connection.Config.ManualIAddress = "0,1,8";

        _scheduler.Start(_s7);
        if (_scheduler.IsConnected)
        {
            SetRunning(true);
        }
        else
        {
            statusText.Text = "轮询启动失败";
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        _scheduler.Stop();
        SetRunning(false);
    }

    public void SetRunning(bool running)
    {
        btnStart.IsEnabled = !running;
        btnStop.IsEnabled = running;
        statusIndicator.Fill = running
            ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
            : new SolidColorBrush(Color.FromRgb(102, 102, 102));
        statusText.Text = running ? "轮询运行中" : "已停止";
    }

    public void SetReady()
    {
        SetRunning(false);
    }

    public void UpdateLatency(long ms)
    {
        Dispatcher.InvokeAsync(() => latencyText.Text = $"{ms} ms");
    }
}
