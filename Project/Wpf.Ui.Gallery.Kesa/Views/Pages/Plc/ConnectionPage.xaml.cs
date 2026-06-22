using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class ConnectionPage : Page
{
    public S7Service S7Service { get; }
    public AppConfigService Config { get; }

    public ConnectionPage(S7Service s7Service, AppConfigService config)
    {
        S7Service = s7Service;
        Config = config;
        InitializeComponent();
        LoadAdapters();
    }

    private void LoadAdapters()
    {
        adapterCombo.ItemsSource = NetworkAdapter.Enumerate();
        if (adapterCombo.Items.Count > 0) adapterCombo.SelectedIndex = 0;
    }

    private void OnConnect(object sender, RoutedEventArgs e)
    {
        string localIp = adapterCombo.SelectedItem is NetworkAdapter na ? na.Ip : "";
        string ip = ipInput.Text.Trim();
        int port = int.TryParse(portInput.Text, out int p) ? p : 102;
        int rack = int.TryParse(rackInput.Text, out int r) ? r : 0;
        int slot = int.TryParse(slotInput.Text, out int s) ? s : 1;

        int result = S7Service.Connect(localIp, ip, port, rack, slot);
        if (result == 0)
        {
            statusBar.Visibility = Visibility.Visible;
            statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            statusText.Text = "已连接";
            btnConnect.IsEnabled = false;
            btnDisconnect.IsEnabled = true;
        }
        else
        {
            statusBar.Visibility = Visibility.Visible;
            statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            statusText.Text = $"连接失败: {S7Service.LastError ?? "未知错误"}";
        }
    }

    private void OnDisconnect(object sender, RoutedEventArgs e)
    {
        S7Service.Disconnect();
        statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        statusText.Text = "已断开";
        btnConnect.IsEnabled = true;
        btnDisconnect.IsEnabled = false;
    }
}
