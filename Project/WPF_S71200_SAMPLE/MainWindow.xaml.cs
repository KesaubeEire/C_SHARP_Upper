using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TestWpf.Models;
using TestWpf.Services;

namespace TestWpf;

public partial class MainWindow : Window
{
    // ─── 手动模式 ───
    private readonly S7Service _plc = new();
    private readonly ObservableCollection<ByteRowViewModel> _iRows = [];
    private readonly ObservableCollection<ByteRowViewModel> _qRows = [];
    private readonly ObservableCollection<ByteRowViewModel> _mRows = [];
    private Dictionary<int, byte> _lastIBytes = [], _lastQBytes = [], _lastMBytes = [];

    // ─── 自动轮询 ───
    private readonly PollingScheduler _scheduler = new();
    private readonly ObservableCollection<DbPollItem> _dbItems = [];
    private readonly System.Timers.Timer _liveRefreshTimer = new(200); // 200ms 刷新实时显示
    private readonly ObservableCollection<string> _fastLiveItems = [];
    private readonly ObservableCollection<string> _dbLiveItems = [];

    public MainWindow()
    {
        InitializeComponent();

        // 手动
        listIRows.ItemsSource = _iRows;
        listQRows.ItemsSource = _qRows;
        listMRows.ItemsSource = _mRows;
        UpdateEmptyState();

        // 自动轮询
        listDbItems.ItemsSource = _dbItems;
        listFastLive.ItemsSource = _fastLiveItems;
        listDbLive.ItemsSource = _dbLiveItems;
        _liveRefreshTimer.Elapsed += (_, _) => Dispatcher.Invoke(RefreshLiveData);
        UpdateDbEmptyState();

        // Tab 切换
        tabControl.SelectionChanged += TabControl_SelectionChanged;
    }

    // ====================== Tab 切换 ======================

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isAuto = tabControl.SelectedIndex == 1;
        manualPanel.Visibility = isAuto ? Visibility.Collapsed : Visibility.Visible;
        autoPanel.Visibility = isAuto ? Visibility.Visible : Visibility.Collapsed;
    }

    // ====================== 连接管理 ======================

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        string ip = txtIP.Text.Trim();
        if (!int.TryParse(txtPort.Text.Trim(), out int port)) port = 102;
        if (!int.TryParse(txtRack.Text.Trim(), out int rack)) rack = 0;
        if (!int.TryParse(txtSlot.Text.Trim(), out int slot)) slot = 0;

        int result = _plc.Connect(ip, port, rack, slot);
        if (result != 0)
        {
            MessageBox.Show(this, $"连接失败:\n{_plc.LastError ?? "错误码: " + result}", "连接错误");
            UpdateConnectionUI();
            return;
        }

        SetConnected(ip, port);
        UpdateConnectionUI();
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        StopPolling();
        _plc.Disconnect();
        SetDisconnected();
        UpdateConnectionUI();
    }

    private void SetConnected(string ip, int port)
    {
        txtStatus.Text = $"已连接 {ip}:{port}";
        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        indicator.Fill = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
    }

    private void SetDisconnected()
    {
        txtStatus.Text = "未连接";
        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        indicator.Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
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
        btnStartPoll.IsEnabled = conn && !_scheduler.IsRunning;
        btnStopPoll.IsEnabled = conn && _scheduler.IsRunning;
    }

    // ====================== 手动：地址解析 ======================

    private static int[] ParseAddrs(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split(',', '，', ';', '；', ' ')
            .Select(s => s.Trim()).Where(s => int.TryParse(s, out _))
            .Select(int.Parse).Distinct().OrderBy(a => a).ToArray();
    }

    // ====================== 手动：I 区 ======================

    private void BtnIRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddrs(txtIAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址", "提示"); return; }
        _lastIBytes = _plc.ReadBytes(S7Service.AreaI, addrs);
        UpdateRows(_iRows, addrs, _lastIBytes, "I", true);
        UpdateEmptyState();
    }

    private void BtnQRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddrs(txtQAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址", "提示"); return; }
        _lastQBytes = _plc.ReadBytes(S7Service.AreaQ, addrs);
        UpdateRows(_qRows, addrs, _lastQBytes, "Q", false);
        UpdateEmptyState();
    }

    private void BtnQWrite_Click(object sender, RoutedEventArgs e)
        => WriteAreaChanges(S7Service.AreaQ, _qRows, "Q");

    private void BtnMRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddrs(txtMAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址", "提示"); return; }
        _lastMBytes = _plc.ReadBytes(S7Service.AreaM, addrs);
        UpdateRows(_mRows, addrs, _lastMBytes, "M", false);
        UpdateEmptyState();
    }

    private void BtnMWrite_Click(object sender, RoutedEventArgs e)
        => WriteAreaChanges(S7Service.AreaM, _mRows, "M");

    private void WriteAreaChanges(int area, ObservableCollection<ByteRowViewModel> rows, string label)
    {
        if (rows.Count == 0) return;
        int ok = 0, fail = 0;
        foreach (var row in rows)
        {
            if (!row.HasChanges) continue;
            if (_plc.WriteByte(area, row.ByteAddress, row.ToByte()))
            { row.HasChanges = false; row.Value = row.ToByte(); ok++; }
            else fail++;
        }
        if (fail > 0) MessageBox.Show(this, $"{label}区写入: {ok} 成功, {fail} 失败\n{_plc.LastError}", "结果");
        else if (ok > 0) txtStatus.Text = $"{label}区写入成功 ({ok} 字节)";
        else txtStatus.Text = $"{label}区无更改";
    }

    private void BitBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.DataContext is BitViewModel bit) bit.Toggle();
    }

    private void UpdateRows(ObservableCollection<ByteRowViewModel> rows, int[] addrs,
                            Dictionary<int, byte> data, string label, bool ro)
    {
        rows.Clear();
        foreach (int a in addrs)
            rows.Add(new ByteRowViewModel(a, label, ro) { Value = data.GetValueOrDefault(a, (byte)0) });
    }

    private void UpdateEmptyState()
    {
        txtIEmpty.Visibility = _iRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtQEmpty.Visibility = _qRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtMEmpty.Visibility = _mRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ====================== 自动轮询：DB 增删 ======================

    private void BtnAddDb_Click(object sender, RoutedEventArgs e)
    {
        var item = new DbPollItem
        {
            DbNumber = _dbItems.Count > 0 ? _dbItems.Max(d => d.DbNumber) + 1 : 1,
            Offset = 0,
            Length = 100,
            Status = "待启动"
        };
        _dbItems.Add(item);
        UpdateDbEmptyState();
    }

    private void BtnRemoveDb_Click(object sender, RoutedEventArgs e)
    {
        var toRemove = _dbItems.Where(d => !d.Enabled).ToList(); // 未勾选的删除
        foreach (var item in toRemove) _dbItems.Remove(item);
        UpdateDbEmptyState();
    }

    private void UpdateDbEmptyState()
        => txtDbEmpty.Visibility = _dbItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    // ====================== 自动轮询：启动/停止 ======================

    private void BtnStartPoll_Click(object sender, RoutedEventArgs e)
    {
        if (!_plc.IsConnected)
        {
            MessageBox.Show(this, "请先连接 PLC", "提示");
            return;
        }

        // 读取 Fast Path 配置
        int iS = TryParse(txtIStart.Text, 0), iE = TryParse(txtIEnd.Text, 2);
        int qS = TryParse(txtQStart.Text, 0), qE = TryParse(txtQEnd.Text, 1);
        int mS = TryParse(txtMStart.Text, 0), mE = TryParse(txtMEnd.Text, 10);

        var cfg = _scheduler.Config;
        cfg.Fast.IStart = iS; cfg.Fast.IEnd = iE; cfg.Fast.EnableI = chkI.IsChecked == true;
        cfg.Fast.QStart = qS; cfg.Fast.QEnd = qE; cfg.Fast.EnableQ = chkQ.IsChecked == true;
        cfg.Fast.MStart = mS; cfg.Fast.MEnd = mE; cfg.Fast.EnableM = chkM.IsChecked == true;

        // 同步 DB 配置
        cfg.DbItems.Clear();
        foreach (var item in _dbItems)
            cfg.DbItems.Add(item);

        // 启动（通过已有连接的信息）
        string ip = txtIP.Text.Trim();
        int port = TryParse(txtPort.Text, 102);
        int rack = TryParse(txtRack.Text, 0);
        int slot = TryParse(txtSlot.Text, 0);

        _scheduler.Start(ip, port, rack, slot);
        if (!_scheduler.IsConnected)
        {
            MessageBox.Show(this, $"轮询连接失败:\n{_scheduler.LastError}", "错误");
            return;
        }

        // 启动实时显示刷新
        _liveRefreshTimer.Start();
        txtPollStatus.Text = "● 轮询中";
        txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        UpdateConnectionUI();
    }

    private void BtnStopPoll_Click(object sender, RoutedEventArgs e)
    {
        StopPolling();
    }

    private void StopPolling()
    {
        _scheduler.Stop();
        _liveRefreshTimer.Stop();
        txtPollStatus.Text = "■ 已停止";
        txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
        UpdateConnectionUI();
    }

    // ====================== 实时数据刷新 ======================

    private void RefreshLiveData()
    {
        var values = _scheduler.LastValues;
        if (values.Count == 0) { txtLiveEmpty.Visibility = Visibility.Visible; return; }
        txtLiveEmpty.Visibility = Visibility.Collapsed;

        // Fast Path (I/Q/M)
        _fastLiveItems.Clear();
        var fastKeys = values.Keys.Where(k => k.StartsWith('I') || k.StartsWith('Q') || k.StartsWith('M'))
                                  .OrderBy(k => k).Take(50);
        foreach (var key in fastKeys)
            _fastLiveItems.Add($"{key}: 0x{values[key]:X2}");

        // DB
        _dbLiveItems.Clear();
        var dbKeys = values.Keys.Where(k => k.StartsWith("DB")).OrderBy(k => k).Take(80);
        foreach (var key in dbKeys)
            _dbLiveItems.Add($"{key}: 0x{values[key]:X2}");
    }

    // ====================== 工具 ======================

    private static int TryParse(string s, int def) =>
        int.TryParse(s?.Trim(), out int r) ? r : def;

    // ====================== 窗口关闭 ======================

    protected override void OnClosed(EventArgs e)
    {
        _scheduler.Dispose();
        _plc.Dispose();
        _liveRefreshTimer.Dispose();
        base.OnClosed(e);
    }
}
