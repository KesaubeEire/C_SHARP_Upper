using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TestWpf.Models;
using TestWpf.Services;

namespace TestWpf;

public partial class MainWindow : Window
{
    private readonly S7Service _plc = new();
    private readonly ObservableCollection<ByteRowViewModel> _iRows = [];
    private readonly ObservableCollection<ByteRowViewModel> _qRows = [];
    private readonly ObservableCollection<ByteRowViewModel> _mRows = [];
    private Dictionary<int, byte> _lastIBytes = [];
    private Dictionary<int, byte> _lastQBytes = [];
    private Dictionary<int, byte> _lastMBytes = [];

    public MainWindow()
    {
        InitializeComponent();
        listIRows.ItemsSource = _iRows;
        listQRows.ItemsSource = _qRows;
        listMRows.ItemsSource = _mRows;
        UpdateEmptyState();
    }

    // ===== 连接管理 =====

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        string ip = txtIP.Text.Trim();
        if (!int.TryParse(txtPort.Text.Trim(), out int port)) port = 102;
        if (!int.TryParse(txtRack.Text.Trim(), out int rack)) rack = 0;
        if (!int.TryParse(txtSlot.Text.Trim(), out int slot)) slot = 0;

        int result = _plc.Connect(ip, port, rack, slot);
        if (result != 0)
        {
            MessageBox.Show(this, $"连接失败:\n{_plc.LastError ?? "错误码: " + result}",
                "连接错误", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateConnectionUI();
            return;
        }

        txtStatus.Text = $"已连接 {ip}:{port}";
        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        indicator.Fill = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        UpdateConnectionUI();
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        _plc.Disconnect();
        txtStatus.Text = "未连接";
        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        indicator.Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        UpdateConnectionUI();
    }

    private void UpdateConnectionUI()
    {
        bool conn = _plc.IsConnected;
        btnConnect.IsEnabled = !conn;
        btnDisconnect.IsEnabled = conn;
        btnIRead.IsEnabled = conn;
        btnQRead.IsEnabled = conn;
        btnQWrite.IsEnabled = conn;
        btnMRead.IsEnabled = conn;
        btnMWrite.IsEnabled = conn;
    }

    // ===== 地址解析 =====

    private static int[] ParseAddresses(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split(',', '，', ';', '；', ' ')
            .Select(s => s.Trim())
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .Distinct()
            .OrderBy(a => a)
            .ToArray();
    }

    // ===== I 区（只读） =====

    private void BtnIRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddresses(txtIAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址，如: 0,1,8", "提示"); return; }
        _lastIBytes = _plc.ReadBytes(S7Service.AreaI, addrs);
        UpdateRows(_iRows, addrs, _lastIBytes, "I", isReadOnly: true);
        UpdateEmptyState();
    }

    // ===== Q 区（读写） =====

    private void BtnQRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddresses(txtQAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址，如: 0,1", "提示"); return; }
        _lastQBytes = _plc.ReadBytes(S7Service.AreaQ, addrs);
        UpdateRows(_qRows, addrs, _lastQBytes, "Q", isReadOnly: false);
        UpdateEmptyState();
    }

    private void BtnQWrite_Click(object sender, RoutedEventArgs e)
    {
        WriteAreaChanges(S7Service.AreaQ, _qRows, "Q");
    }

    // ===== M 区（读写） =====

    private void BtnMRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddresses(txtMAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址，如: 0,1", "提示"); return; }
        _lastMBytes = _plc.ReadBytes(S7Service.AreaM, addrs);
        UpdateRows(_mRows, addrs, _lastMBytes, "M", isReadOnly: false);
        UpdateEmptyState();
    }

    private void BtnMWrite_Click(object sender, RoutedEventArgs e)
    {
        WriteAreaChanges(S7Service.AreaM, _mRows, "M");
    }

    // ===== 写入通用 =====

    private void WriteAreaChanges(int area, ObservableCollection<ByteRowViewModel> rows, string label)
    {
        if (rows.Count == 0) return;
        int ok = 0, fail = 0;
        foreach (var row in rows)
        {
            if (!row.HasChanges) continue;
            if (_plc.WriteByte(area, row.ByteAddress, row.ToByte()))
            { row.HasChanges = false; row.Value = row.ToByte(); ok++; }
            else { fail++; }
        }
        if (fail > 0)
            MessageBox.Show(this, $"{label}区写入: {ok} 成功, {fail} 失败\n{_plc.LastError}", "结果");
        else if (ok > 0)
            txtStatus.Text = $"{label}区写入成功 ({ok} 字节)";
        else
            txtStatus.Text = $"{label}区无更改需要写入";
    }

    // ===== Bit 点击切换 =====

    private void BitBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is BitViewModel bit)
            bit.Toggle();
    }

    // ===== 更新 UI =====

    private void UpdateRows(ObservableCollection<ByteRowViewModel> rows, int[] addresses,
                            Dictionary<int, byte> data, string label, bool isReadOnly)
    {
        rows.Clear();
        foreach (int addr in addresses)
            rows.Add(new ByteRowViewModel(addr, label, isReadOnly)
                { Value = data.GetValueOrDefault(addr, (byte)0) });
    }

    private void UpdateEmptyState()
    {
        txtIEmpty.Visibility = _iRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtQEmpty.Visibility = _qRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtMEmpty.Visibility = _mRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnClosed(EventArgs e)
    {
        _plc.Dispose();
        base.OnClosed(e);
    }
}
