using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TestWpf.Models;
using TestWpf.Controls;
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
    private bool _qWriteMode = false;
    private bool _mWriteMode = false;

    // ─── 自动轮询 ───
    private readonly PollingScheduler _scheduler = new();
    private readonly ObservableCollection<DbPollItem> _dbItems = [];
    private readonly ObservableCollection<DbStructure> _importedDbs = [];
    private readonly ObservableCollection<UdtStructure> _importedUdts = [];
    private readonly System.Timers.Timer _liveRefreshTimer = new(200); // 200ms 刷新实时显示
    private readonly ObservableCollection<string> _fastLiveItems = [];
    private readonly ObservableCollection<string> _dbLiveItems = [];

    private readonly AppConfig _config = AppConfig.Load();

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
        listImportedDb.ItemsSource = _importedDbs;
        listImportedUdt.ItemsSource = _importedUdts;
        listFastLive.ItemsSource = _fastLiveItems;
        listDbLive.ItemsSource = _dbLiveItems;
        _liveRefreshTimer.Elapsed += (_, _) => Dispatcher.Invoke(RefreshLiveData);
        UpdateDbEmptyState();

        // Tab 切换
        tabControl.SelectionChanged += TabControl_SelectionChanged;

        // ===== 从配置恢复所有用户输入 =====
        RestoreFromConfig();
    }

    /// <summary>从配置文件恢复所有 UI 状态</summary>
    private void RestoreFromConfig()
    {
        // 连接
        txtIP.Text = _config.IP;
        txtPort.Text = _config.Port.ToString();
        txtRack.Text = _config.Rack.ToString();
        txtSlot.Text = _config.Slot.ToString();

        // 手动模式地址
        txtIAddress.Text = _config.ManualIAddress;
        txtQAddress.Text = _config.ManualQAddress;
        txtMAddress.Text = _config.ManualMAddress;

        // 自动轮询范围
        txtIStart.Text = _config.PollIStart.ToString();
        txtIEnd.Text = _config.PollIEnd.ToString();
        txtQStart.Text = _config.PollQStart.ToString();
        txtQEnd.Text = _config.PollQEnd.ToString();
        txtMStart.Text = _config.PollMStart.ToString();
        txtMEnd.Text = _config.PollMEnd.ToString();
        chkI.IsChecked = _config.PollEnableI;
        chkQ.IsChecked = _config.PollEnableQ;
        chkM.IsChecked = _config.PollEnableM;
        txtPollInterval.Text = _config.PollIntervalMs.ToString();

        // DB 列表
        _dbItems.Clear();
        foreach (var item in _config.DbItems)
            _dbItems.Add(item);

        // 导入的 DB 结构
        _importedDbs.Clear();
        foreach (var info in _config.ImportedDbs)
        {
            var db = new DbStructure
            {
                DbNumber = info.DbNumber,
                DbName = info.DbName,
                SourceFile = info.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson) ?? []
            };
            _importedDbs.Add(db);
        }

        // 导入的 UDT 结构
        _importedUdts.Clear();
        foreach (var info in _config.ImportedUdts)
        {
            var udt = new UdtStructure
            {
                UdtName = info.UdtName,
                SourceFile = info.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson) ?? []
            };
            _importedUdts.Add(udt);
        }
        UpdateDbEmptyState();

        // 主题
        if (_config.ThemeMode == "Light")
        {
            ThemeManager.Apply(TestWpf.Services.AppThemeMode.Light);
            btnTheme.Content = "☀";
        }

        // 窗口状态
        if (_config.WindowLeft >= 0 && _config.WindowTop >= 0)
        {
            Left = _config.WindowLeft;
            Top = _config.WindowTop;
        }
        Width = _config.WindowWidth;
        Height = _config.WindowHeight;
        if (Enum.TryParse<WindowState>(_config.WindowState, out var ws))
            WindowState = ws;
    }

    /// <summary>把当前 UI 状态保存到配置文件</summary>
    private void SaveConfig()
    {
        _config.IP = txtIP.Text;
        _config.Port = TryParse(txtPort.Text, 102);
        _config.Rack = TryParse(txtRack.Text, 0);
        _config.Slot = TryParse(txtSlot.Text, 0);

        _config.ManualIAddress = txtIAddress.Text;
        _config.ManualQAddress = txtQAddress.Text;
        _config.ManualMAddress = txtMAddress.Text;

        _config.PollIStart = TryParse(txtIStart.Text, 0);
        _config.PollIEnd = TryParse(txtIEnd.Text, 2);
        _config.PollQStart = TryParse(txtQStart.Text, 0);
        _config.PollQEnd = TryParse(txtQEnd.Text, 1);
        _config.PollMStart = TryParse(txtMStart.Text, 0);
        _config.PollMEnd = TryParse(txtMEnd.Text, 10);
        _config.PollEnableI = chkI.IsChecked == true;
        _config.PollEnableQ = chkQ.IsChecked == true;
        _config.PollEnableM = chkM.IsChecked == true;
        _config.PollIntervalMs = TryParse(txtPollInterval.Text, 50);

        _config.DbItems = _dbItems.ToList();

        // 导入的 DB 结构
        _config.ImportedDbs = _importedDbs.Select(d => new ImportedDbInfo
        {
            DbNumber = d.DbNumber,
            DbName = d.DbName,
            SourceFile = d.SourceFile,
            VariablesJson = System.Text.Json.JsonSerializer.Serialize(d.Variables)
        }).ToList();

        // 导入的 UDT 结构
        _config.ImportedUdts = _importedUdts.Select(u => new ImportedUdtInfo
        {
            UdtName = u.UdtName,
            SourceFile = u.SourceFile,
            VariablesJson = System.Text.Json.JsonSerializer.Serialize(u.Variables)
        }).ToList();

        _config.ThemeMode = ThemeManager.Current == TestWpf.Services.AppThemeMode.Dark ? "Dark" : "Light";

        _config.WindowLeft = Left;
        _config.WindowTop = Top;
        _config.WindowWidth = Width;
        _config.WindowHeight = Height;
        _config.WindowState = WindowState.ToString();

        _config.Save();
    }

    // ====================== Tab 切换 ======================

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isAuto = tabControl.SelectedIndex == 1;
        manualPanel.Visibility = isAuto ? Visibility.Collapsed : Visibility.Visible;
        autoPanel.Visibility = isAuto ? Visibility.Visible : Visibility.Collapsed;
    }

    // ====================== 主题切换 ======================

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        bool isDark = ThemeManager.Current == TestWpf.Services.AppThemeMode.Dark;
        ThemeManager.Toggle();
        btnTheme.Content = isDark ? "☀" : "🌙";
        btnTheme.ToolTip = isDark ? "切换到暗色主题" : "切换到亮色主题";
        SaveConfig();
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
        SaveConfig();
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        StopPolling();
        _plc.Disconnect();
        SetDisconnected();
        UpdateConnectionUI();
        SaveConfig();
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
        btnQWriteMode.IsEnabled = conn;
        btnMRead.IsEnabled = conn;
        btnMWriteMode.IsEnabled = conn;
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

    private void BtnQWriteMode_Click(object sender, RoutedEventArgs e)
    {
        _qWriteMode = !_qWriteMode;
        btnQWriteMode.Content = _qWriteMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)";
        btnQWriteMode.Background = _qWriteMode
            ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))   // 红色-警示
            : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));  // 灰色
    }

    private void BtnMRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddrs(txtMAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址", "提示"); return; }
        _lastMBytes = _plc.ReadBytes(S7Service.AreaM, addrs);
        UpdateRows(_mRows, addrs, _lastMBytes, "M", false);
        UpdateEmptyState();
    }

    private void BtnMWriteMode_Click(object sender, RoutedEventArgs e)
    {
        _mWriteMode = !_mWriteMode;
        btnMWriteMode.Content = _mWriteMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)";
        btnMWriteMode.Background = _mWriteMode
            ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
            : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    }

    private void BitBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.DataContext is not BitViewModel bit) return;
        if (bit.Parent == null) return;

        // I 区：只读不响应点击；Q/M 区：仅在写入模式下才响应并立即写入
        bool canWrite = (bit.Parent.AreaLabel == "Q" && _qWriteMode)
                     || (bit.Parent.AreaLabel == "M" && _mWriteMode);
        if (!canWrite) return;

        bit.Toggle();
        // 立即写入 PLC
        int area = bit.Parent.AreaLabel == "Q" ? S7Service.AreaQ : S7Service.AreaM;
        _plc.WriteByte(area, bit.Parent.ByteAddress, bit.Parent.ToByte());
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
        int dbNum = TryParse(txtNewDbNumber.Text, 1);
        int offset = TryParse(txtNewDbOffset.Text, 0);
        int length = TryParse(txtNewDbLen.Text, 100);

        if (_dbItems.Any(d => d.DbNumber == dbNum && d.Offset == offset))
        {
            MessageBox.Show(this, $"DB{dbNum} @{offset} 已在列表中", "提示");
            return;
        }

        var item = new DbPollItem
        {
            DbNumber = dbNum,
            Offset = offset,
            Length = Math.Min(length, 222),
            Status = "待启动"
        };
        _dbItems.Add(item);
        UpdateDbEmptyState();
        txtNewDbNumber.Text = (dbNum + 1).ToString();
        SaveConfig();
    }

    private void BtnRemoveDb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DbPollItem item)
        {
            _dbItems.Remove(item);
            SaveConfig();
            UpdateDbEmptyState();
        }
    }

    private void UpdateDbEmptyState()
        => txtDbEmpty.Visibility = _dbItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    // ====================== 导入 DB/UDT ======================

    private void BtnImportDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DB 文件 (*.db)|*.db|所有文件 (*.*)|*.*",
            Title = "选择 TIA Portal 导出的 .db 文件",
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != true) return;

        var db = DbFileParser.Parse(dlg.FileName);
        if (db.HasUnknownType)
        {
            MessageBox.Show(this, $"解析失败: {db.ParseError}", "未知数据类型",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (db.ParseError != null)
        {
            MessageBox.Show(this, $"解析失败: {db.ParseError}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // 让用户输入 DB 号
        var inputDlg = new InputDialog($"请输入 DB{db.DbName} 的 DB 编号:", "1");
        if (inputDlg.ShowDialog() != true) return;
        if (!int.TryParse(inputDlg.InputText, out int dbNum) || dbNum <= 0)
        {
            MessageBox.Show(this, "无效的 DB 编号", "错误");
            return;
        }

        // 检查是否已存在
        if (_importedDbs.Any(d => d.DbNumber == dbNum))
        {
            MessageBox.Show(this, $"DB{dbNum} 已导入，请先删除再重新导入", "提示");
            return;
        }

        db.DbNumber = dbNum;
        _importedDbs.Add(db);
        SaveConfig();
    }

    private void BtnImportUdt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "UDT 文件 (*.udt)|*.udt|所有文件 (*.*)|*.*",
            Title = "选择 TIA Portal 导出的 .udt 文件",
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != true) return;

        var udt = UdtFileParser.Parse(dlg.FileName);
        if (udt.HasUnknownType)
        {
            MessageBox.Show(this, $"解析失败: {udt.ParseError}", "未知数据类型",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (udt.ParseError != null)
        {
            MessageBox.Show(this, $"解析失败: {udt.ParseError}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (_importedUdts.Any(u => u.UdtName == udt.UdtName))
        {
            MessageBox.Show(this, $"UDT \"{udt.UdtName}\" 已导入", "提示");
            return;
        }

        _importedUdts.Add(udt);
        SaveConfig();
    }

    private void BtnDeleteImportedDb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DbStructure db)
        {
            _importedDbs.Remove(db);
            SaveConfig();
        }
    }

    private void BtnDeleteImportedUdt_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is UdtStructure udt)
        {
            _importedUdts.Remove(udt);
            SaveConfig();
        }
    }

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
        SaveConfig();
        _scheduler.Dispose();
        _plc.Dispose();
        _liveRefreshTimer.Dispose();
        base.OnClosed(e);
    }
}
