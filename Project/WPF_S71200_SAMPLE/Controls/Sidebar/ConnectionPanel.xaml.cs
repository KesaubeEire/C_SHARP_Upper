using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TestWpf.Models;
using TestWpf.Services;

namespace TestWpf.Controls.Sidebar;

/// <summary>
/// 连接配置面板 — 网卡选择、IP/端口/机架/槽位、连接/断开、状态指示
/// </summary>
public partial class ConnectionPanel : UserControl
{
    private S7Service? _s7;

    public ConnectionPanel()
    {
        InitializeComponent();
        var adapters = NetworkAdapter.Enumerate();
        cmbAdapter.ItemsSource = adapters;
    }

    public void Init(S7Service s7) => _s7 = s7;

    public string IP => txtIP.Text.Trim();
    public int Port => int.TryParse(txtPort.Text.Trim(), out int p) ? p : 102;
    public int Rack => int.TryParse(txtRack.Text.Trim(), out int r) ? r : 0;
    public int Slot => int.TryParse(txtSlot.Text.Trim(), out int s) ? s : 0;
    public string LocalIP => cmbAdapter.SelectedItem is NetworkAdapter na ? na.Ip : "";
    public bool IsConnected => _s7?.IsConnected ?? false;

    /// <summary>连接状态变化时通知外部（用于更新其他面板的启用状态）</summary>
    public event EventHandler<bool>? ConnectionChanged;

    public void RestoreConfig(string ip, int port, int rack, int slot, string localIp)
    {
        txtIP.Text = ip;
        txtPort.Text = port.ToString();
        txtRack.Text = rack.ToString();
        txtSlot.Text = slot.ToString();
        if (cmbAdapter.ItemsSource is List<NetworkAdapter> list)
        {
            var idx = list.FindIndex(a => a.Ip == localIp);
            if (idx >= 0) cmbAdapter.SelectedIndex = idx;
        }
    }

    private void OnConnect(object sender, RoutedEventArgs e)
    {
        if (_s7 == null) return;
        var ip = txtIP.Text.Trim();
        if (_s7.Connect(LocalIP, ip, Port, Rack, Slot) != 0)
        {
            MessageBox.Show($"连接失败: {_s7.LastError}", "错误");
            UpdateStatus(false);
            return;
        }
        UpdateStatus(true);
        ConnectionChanged?.Invoke(this, true);
    }

    private void OnDisconnect(object sender, RoutedEventArgs e)
    {
        _s7?.Disconnect();
        UpdateStatus(false);
        ConnectionChanged?.Invoke(this, false);
    }

    public void UpdateStatus(bool connected)
    {
        if (connected)
        {
            txtStatus.Text = $"已连接 {txtIP.Text}:{txtPort.Text}";
            txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
            indicator.Fill = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        }
        else
        {
            txtStatus.Text = "未连接";
            txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            indicator.Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        }
        btnConnect.IsEnabled = !connected;
        btnDisconnect.IsEnabled = connected;
    }
}
