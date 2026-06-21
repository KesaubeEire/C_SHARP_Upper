using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
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
    private bool _qWriteMode, _mWriteMode;

    // ─── 轮询 ───
    private readonly PollingScheduler _scheduler = new();

    // ─── 导入 DB/UDT ───
    private readonly ObservableCollection<DbStructure> _importedDbs = [];
    private readonly ObservableCollection<UdtStructure> _importedUdts = [];

    // ─── 趋势图 ───
    private readonly MockTrendService _mockTrend = new(100);
    private readonly ObservableCollection<TrendChannelConfig> _trendChannels = [];
    private readonly ObservableCollection<ISeries> _trendSeries = [];
    private readonly ObservableCollection<ISeries> _barSeriesColl = [];
    private readonly Dictionary<string, ObservableCollection<DateTimePoint>> _trendNormBuffers = [];

    private static readonly (string label, int windowMs)[] TimeRangeOptions = [
        ("1 分钟",   60_000), ("5 分钟",   300_000), ("30 分钟",  1_800_000),
        ("1 小时",   3_600_000), ("12 小时",  43_200_000), ("24 小时",  86_400_000),
    ];
    private int _trendTimeWindowMs = 60_000;
    private const int MaxBufferPoints = 864000;

    private readonly AppConfig _config = AppConfig.Load();
    private static readonly SKColor[] TrendColors = [SKColors.Crimson, SKColors.Cyan, SKColors.SeaGreen, SKColors.Gold, SKColors.DodgerBlue, SKColors.MediumPurple];

    public MainWindow()
    {
        InitializeComponent();
        InitTrendPanel();

        listIRows.ItemsSource = _iRows; listQRows.ItemsSource = _qRows; listMRows.ItemsSource = _mRows;
        UpdateEmptyState();
        listImportedDb.ItemsSource = _importedDbs; listImportedUdt.ItemsSource = _importedUdts;

        var adapters = NetworkAdapter.Enumerate();
        cmbAdapter.ItemsSource = adapters;

        _scheduler.DataUpdated += OnPollData;
        tabControl.SelectionChanged += TabControl_SelectionChanged;
        RestoreFromConfig();
    }

    // ====================== 趋势图 ======================

    private void InitTrendPanel()
    {
        var defs = new (string key, string label, string color, string unit, double min, double max)[]
        {
            ("ch_temp",   "Reactor Temp",   "#E24B4A", "°C",    60.0, 110.0),
            ("ch_press",  "Pressure",        "#37D3E0", "bar",    0.0,  16.0),
            ("ch_flow",   "Feed Flow",       "#1D9E75", "m³/h",   0.0,  50.0),
            ("ch_level",  "Tank Level",      "#F4D03F", "%",      0.0, 100.0),
            ("ch_servo",  "Servo Pos",       "#3498DB", "mm",   -10.0,  90.0),
            ("ch_current","Motor Current",   "#9B59B6", "A",      0.0,  25.0),
        };
        int idx = 0;
        foreach (var (key, label, color, unit, min, max) in defs)
        {
            var buf = new ObservableCollection<DateTimePoint>();
            _trendNormBuffers[key] = buf;
            _trendChannels.Add(new TrendChannelConfig { Key = key, Label = label, Color = color, Unit = unit, Min = min, Max = max, Variable = key });
            double range = max - min;
            _trendSeries.Add(new LineSeries<DateTimePoint> { Values = buf, Stroke = new SolidColorPaint(TrendColors[idx++ % TrendColors.Length]) { StrokeThickness = 2 }, Fill = null, GeometrySize = 0, LineSmoothness = 0.3, Name = label });
        }
        listTrendChannels.ItemsSource = _trendChannels;
        cmbTrendTimeRange.ItemsSource = TimeRangeOptions.Select(o => o.label).ToList();
        cmbTrendTimeRange.SelectedIndex = 0;
        _trendTimeWindowMs = TimeRangeOptions[0].windowMs;
        cartesianTrend.Series = _trendSeries;
        cartesianTrend.YAxes = [new Axis { MinLimit = 0, MaxLimit = 100, Labeler = _ => "" }];
        cartesianTrend.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;
        UpdateTrendXAxis();

        var barVals = new ObservableCollection<double> { 0, 0, 0, 0, 0, 0 };
        _barSeriesColl.Add(new ColumnSeries<double> { Values = barVals, Fill = new SolidColorPaint(SKColor.Parse("#3498DB")), Padding = 2, MaxBarWidth = 40 });
        cartesianBars.Series = _barSeriesColl;
        cartesianBars.XAxes = [new Axis { Labels = ["Temp", "Press", "Flow", "Level", "Servo", "Curr."], LabelsRotation = 45 }];
        cartesianBars.YAxes = [new Axis { MinLimit = 0 }];

        _mockTrend.SampleGenerated += OnTrendSample;
        // 刻度等趋势Tab首次选中时再画（此时canvas才有尺寸）
        _gaugeDrawn = false;
    }
    private bool _gaugeDrawn;

    private void OnTrendSample(string key, double val, DateTime ts)
    {
        Dispatcher.Invoke(() =>
        {
            var cfg = _trendChannels.FirstOrDefault(c => c.Key == key);
            if (cfg == null) return;
            double range = cfg.Max - cfg.Min;
            double norm = range > 0 ? Math.Clamp((val - cfg.Min) / range * 100.0, 0, 100) : 50;
            if (!_trendNormBuffers.TryGetValue(key, out var buf)) return;
            buf.Add(new DateTimePoint(ts, norm));
            while (buf.Count > MaxBufferPoints) buf.RemoveAt(0);
            cfg.CurrentValue = val;
            SlideTrendXAxis();

            var col = (ColumnSeries<double>)_barSeriesColl[0];
            var vals = (ObservableCollection<double>)col.Values!;
            int bi = key switch { "ch_temp" => 0, "ch_press" => 1, "ch_flow" => 2, "ch_level" => 3, "ch_servo" => 4, "ch_current" => 5, _ => -1 };
            if (bi >= 0) vals[bi] = val;
            if (key == "ch_servo") UpdateGaugeNeedle(val);
        });
    }

    private void BtnTrendMock_Click(object sender, RoutedEventArgs e)
    {
        if (_mockTrend.IsRunning) { _mockTrend.Stop(); btnTrendMock.Content = "▶ 启动 Mock"; }
        else { _mockTrend.Start(); btnTrendMock.Content = "■ 停止 Mock"; }
    }

    private void CanvasGauge_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_gaugeDrawn && e.NewSize.Width >= 10)
        { _gaugeDrawn = true; DrawGaugeScale(45.0); }
    }

    private void CmbTrendTimeRange_Changed(object _, SelectionChangedEventArgs e)
    {
        int idx = cmbTrendTimeRange.SelectedIndex;
        if (idx < 0 || idx >= TimeRangeOptions.Length) return;
        _trendTimeWindowMs = TimeRangeOptions[idx].windowMs;
        UpdateTrendXAxis();
    }

    private void UpdateTrendXAxis() { double n = DateTime.Now.Ticks; cartesianTrend.XAxes = [MakeTrendAxis(n - _trendTimeWindowMs * 10_000, n)]; }
    private void SlideTrendXAxis() { double n = DateTime.Now.Ticks; cartesianTrend.XAxes = [MakeTrendAxis(n - _trendTimeWindowMs * 10_000, n)]; }
    private static Axis MakeTrendAxis(double min, double max) => new Axis { MinLimit = min, MaxLimit = max, Labeler = v => v <= 0 || v > 1e18 ? "" : new DateTime((long)v).ToLocalTime().ToString("HH:mm:ss") };

    /// <summary>画刻度尺（仅画一次，由 InitTrendPanel 调用）</summary>
    private void DrawGaugeScale(double initialPos)
    {
        var c = canvasGauge; if (c.ActualWidth < 10) return;
        double w = c.ActualWidth, h = c.ActualHeight;
        double left = double.TryParse(txtGaugeLeft.Text, out var l) ? l : 0;
        double right = double.TryParse(txtGaugeRight.Text, out var r) ? r : 100;
        int ticks = int.TryParse(txtGaugeTicks.Text, out var tc) ? tc : 10;
        if (ticks < 2) ticks = 2;
        c.Children.Clear();
        double pad = 20, drawW = w - pad * 2, range = right - left;
        if (range <= 0) range = 100;
        // 刻度线 + 标签（静态，只画一次）
        for (int i = 0; i <= ticks; i++)
        {
            double frac = (double)i / ticks, x = pad + drawW * frac;
            c.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = h * 0.35, Stroke = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), StrokeThickness = 1 });
            if (ticks <= 10 || i % 2 == 0)
            {
                var lbl = new TextBlock { Text = $"{left + range * frac:F0}", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)) };
                Canvas.SetLeft(lbl, x - 12); Canvas.SetTop(lbl, h * 0.38); c.Children.Add(lbl);
            }
        }
        // 保存布局参数到 Tag，供 UpdateGaugeNeedle 使用
        c.Tag = (pad, drawW, range, left, h);
        UpdateGaugeNeedle(initialPos);
    }

    /// <summary>仅更新指针位置和数值标签（每次数据到达时调用，不重画刻度）</summary>
    private void UpdateGaugeNeedle(double pos)
    {
        var c = canvasGauge;
        if (c.Tag is not (double pad, double drawW, double range, double left, double h)) return;
        txtGaugePos.Text = $"{pos:F1} mm";
        // 移除旧指针元素（从索引3开始，前面3个是刻度线+标签）
        while (c.Children.Count > 3) c.Children.RemoveAt(c.Children.Count - 1);
        double nFrac = Math.Clamp((pos - left) / range, 0, 1), nx = pad + drawW * nFrac;
        c.Children.Add(new Rectangle { Width = 3, Height = h * 0.55, Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), RadiusX = 2, RadiusY = 2 });
        Canvas.SetTop(c.Children[^1], h * 0.4); Canvas.SetLeft(c.Children[^1], nx - 1.5);
        c.Children.Add(new Polygon { Points = new PointCollection { new(nx - 5, h * 0.4 + 10), new(nx + 5, h * 0.4 + 10), new(nx, h * 0.4 - 2) }, Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) });
        var vl = new TextBlock { Text = $"{pos:F1}", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) };
        Canvas.SetLeft(vl, Math.Clamp(nx - 15, 0, c.ActualWidth - 35)); Canvas.SetTop(vl, 0); c.Children.Add(vl);
    }

    // ====================== Tab 切换 ======================

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isTrend = tabControl.SelectedIndex == 1;
        manualPanel.Visibility = isTrend ? Visibility.Collapsed : Visibility.Visible;
        trendPanel.Visibility = isTrend ? Visibility.Visible : Visibility.Collapsed;
        if (isTrend && !_gaugeDrawn && canvasGauge.ActualWidth >= 10)
        { _gaugeDrawn = true; DrawGaugeScale(45.0); }
    }

    // ====================== 主题 ======================

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        bool d = ThemeManager.Current == AppThemeMode.Dark;
        ThemeManager.Toggle();
        btnTheme.Content = d ? "☀" : "🌙";
        SaveConfig();
    }

    // ====================== 连接 ======================

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        string ip = txtIP.Text.Trim();
        if (!int.TryParse(txtPort.Text.Trim(), out int p)) p = 102;
        if (!int.TryParse(txtRack.Text.Trim(), out int r)) r = 0;
        if (!int.TryParse(txtSlot.Text.Trim(), out int s)) s = 0;
        string localIp = cmbAdapter.SelectedItem is NetworkAdapter na ? na.Ip : "";
if (_plc.Connect(localIp, ip, p, r, s) != 0) { MessageBox.Show(this, $"连接失败: {_plc.LastError}", "错误"); UpdateUI(); return; }
        txtStatus.Text = $"已连接 {ip}:{p}";
        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        indicator.Fill = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        UpdateUI(); SaveConfig();
    }

    private void BtnDisconnect_Click(object _, RoutedEventArgs _2) { _plc.Disconnect(); txtStatus.Text = "未连接"; txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); indicator.Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); UpdateUI(); SaveConfig(); }

    private void UpdateUI() { bool c = _plc.IsConnected; bool p = _scheduler.IsRunning; btnConnect.IsEnabled = !c; btnDisconnect.IsEnabled = c; btnIRead.IsEnabled = c && !p; btnQRead.IsEnabled = c && !p; btnQWriteMode.IsEnabled = c && !p; btnMRead.IsEnabled = c && !p; btnMWriteMode.IsEnabled = c && !p; btnStartPoll.IsEnabled = c && !p; btnStopPoll.IsEnabled = c && p; }

    // ====================== 手动读写 ======================

    private static int[] ParseAddrs(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split(',', '，', ';', '；', ' ').Select(s => s.Trim()).Where(s => int.TryParse(s, out _)).Select(int.Parse).Distinct().OrderBy(a => a).ToArray();
    }

    private void BtnIRead_Click(object _, RoutedEventArgs _2) { var a = ParseAddrs(txtIAddress.Text); if (a.Length == 0) return; _lastIBytes = _plc.ReadBytes(S7Service.AreaI, a); _iRows.Clear(); foreach (int i in a) _iRows.Add(new ByteRowViewModel(i, "I", true) { Value = _lastIBytes.GetValueOrDefault(i) }); UpdateEmptyState(); }
    private void BtnQRead_Click(object _, RoutedEventArgs _2) { var a = ParseAddrs(txtQAddress.Text); if (a.Length == 0) return; _lastQBytes = _plc.ReadBytes(S7Service.AreaQ, a); _qRows.Clear(); foreach (int i in a) _qRows.Add(new ByteRowViewModel(i, "Q", false) { Value = _lastQBytes.GetValueOrDefault(i) }); UpdateEmptyState(); }
    private void BtnMRead_Click(object _, RoutedEventArgs _2) { var a = ParseAddrs(txtMAddress.Text); if (a.Length == 0) return; _lastMBytes = _plc.ReadBytes(S7Service.AreaM, a); _mRows.Clear(); foreach (int i in a) _mRows.Add(new ByteRowViewModel(i, "M", false) { Value = _lastMBytes.GetValueOrDefault(i) }); UpdateEmptyState(); }

    private void BtnQWriteMode_Click(object sender, RoutedEventArgs e) { _qWriteMode = !_qWriteMode; btnQWriteMode.Content = _qWriteMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)"; btnQWriteMode.Background = _qWriteMode ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); }
    private void BtnMWriteMode_Click(object sender, RoutedEventArgs e) { _mWriteMode = !_mWriteMode; btnMWriteMode.Content = _mWriteMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)"; btnMWriteMode.Background = _mWriteMode ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); }

    private void BitBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.DataContext is not BitViewModel bit || bit.Parent == null) return;
        if (!((bit.Parent.AreaLabel == "Q" && _qWriteMode) || (bit.Parent.AreaLabel == "M" && _mWriteMode))) return;
        bit.Toggle();
        _plc.WriteByte(bit.Parent.AreaLabel == "Q" ? S7Service.AreaQ : S7Service.AreaM, bit.Parent.ByteAddress, bit.Parent.ToByte());
    }

    private void UpdateEmptyState() { txtIEmpty.Visibility = _iRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed; txtQEmpty.Visibility = _qRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed; txtMEmpty.Visibility = _mRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed; }

    // ====================== 导入 DB/UDT ======================

    private void BtnImportDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "DB 文件 (*.db)|*.db|所有文件 (*.*)|*.*", Title = "选择 TIA Portal 导出的 .db 文件", Multiselect = false };
        if (dlg.ShowDialog(this) != true) return;
        var db = DbFileParser.Parse(dlg.FileName);
        if (db.HasUnknownType) { MessageBox.Show(this, $"解析失败: {db.ParseError}", "未知数据类型", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (db.ParseError != null) { MessageBox.Show(this, $"解析失败: {db.ParseError}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        var inputDlg = new InputDialog($"请输入 DB{db.DbName} 的 DB 编号:", "1");
        if (inputDlg.ShowDialog() != true) return;
        if (!int.TryParse(inputDlg.InputText, out int dbNum) || dbNum <= 0) { MessageBox.Show(this, "无效的 DB 编号", "错误"); return; }
        if (_importedDbs.Any(d => d.DbNumber == dbNum)) { MessageBox.Show(this, $"DB{dbNum} 已导入，请先删除再重新导入", "提示"); return; }
        db.DbNumber = dbNum; _importedDbs.Add(db); SaveConfig();
    }

    private void BtnImportUdt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "UDT 文件 (*.udt)|*.udt|所有文件 (*.*)|*.*", Title = "选择 TIA Portal 导出的 .udt 文件", Multiselect = false };
        if (dlg.ShowDialog(this) != true) return;
        var udt = UdtFileParser.Parse(dlg.FileName);
        if (udt.HasUnknownType) { MessageBox.Show(this, $"解析失败: {udt.ParseError}", "未知数据类型", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (udt.ParseError != null) { MessageBox.Show(this, $"解析失败: {udt.ParseError}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        if (_importedUdts.Any(u => u.UdtName == udt.UdtName)) { MessageBox.Show(this, $"UDT \"{udt.UdtName}\" 已导入", "提示"); return; }
        _importedUdts.Add(udt); SaveConfig();
    }

    private void BtnDeleteImportedDb_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.Tag is DbStructure db) { _importedDbs.Remove(db); SaveConfig(); } }
    private void BtnDeleteImportedUdt_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.Tag is UdtStructure udt) { _importedUdts.Remove(udt); SaveConfig(); } }

    // ====================== 轮询 ======================

    private void BtnStartPoll_Click(object sender, RoutedEventArgs e)
    {
        if (!_plc.IsConnected) { MessageBox.Show(this, "请先连接 PLC", "提示"); return; }
        int port = TryParse(txtPort.Text, 102), rack = TryParse(txtRack.Text, 0), slot = TryParse(txtSlot.Text, 0);
        int interval = TryParse(txtPollInterval.Text, 50);
        var cfg = _scheduler.Config;
        cfg.Fast.PollIAddr = txtIAddress.Text; cfg.Fast.PollQAddr = txtQAddress.Text; cfg.Fast.PollMAddr = txtMAddress.Text;
        cfg.FastInterval = interval;
        cfg.DbIp = txtIP.Text.Trim(); cfg.DbRack = rack; cfg.DbSlot = slot;
        _scheduler.Start(_plc, port);
        if (!_scheduler.IsConnected) { MessageBox.Show(this, $"轮询连接失败:\n{_scheduler.LastError}", "错误"); return; }
        txtPollStatus.Text = "● 轮询中"; txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        UpdateUI();
    }

    private void BtnStopPoll_Click(object sender, RoutedEventArgs e) { StopPolling(); }
    private void StopPolling() { _scheduler.Stop(); txtPollStatus.Text = "■ 已停止"; txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)); UpdateUI(); }

    /// <summary>轮询数据直接写入 I/Q/M 行（无中间缓存）</summary>
    private void OnPollData(HashSet<string> updated)
    {
        Dispatcher.Invoke(() =>
        {
            void UpdateRows(ObservableCollection<ByteRowViewModel> rows)
            {
                foreach (var row in rows)
                {
                    string key = $"{row.AreaLabel}{row.ByteAddress}";
                    if (updated.Contains(key) && _scheduler.GetValue(key) is byte val && val != row.Value)
                        row.Value = val;
                }
            }
            UpdateRows(_iRows); UpdateRows(_qRows); UpdateRows(_mRows);
        });
    }

    // ====================== 工具 + 关闭 ======================

    private static int TryParse(string s, int def) => int.TryParse(s?.Trim(), out int r) ? r : def;
    protected override void OnClosed(EventArgs e) { _mockTrend.SampleGenerated -= OnTrendSample; _scheduler.Dispose(); SaveConfig(); _plc.Dispose(); _mockTrend.Dispose(); base.OnClosed(e); }

    // ====================== 配置持久化 ======================

    private void RestoreFromConfig()
    {
        txtIP.Text = _config.IP; txtPort.Text = _config.Port.ToString();
        txtRack.Text = _config.Rack.ToString(); txtSlot.Text = _config.Slot.ToString();
        txtIAddress.Text = _config.ManualIAddress; txtQAddress.Text = _config.ManualQAddress; txtMAddress.Text = _config.ManualMAddress;
        if (cmbAdapter.ItemsSource is List<NetworkAdapter> list)
        {
            var idx = list.FindIndex(a => a.Ip == _config.LocalIP);
            if (idx >= 0) cmbAdapter.SelectedIndex = idx;
        }
        _importedDbs.Clear();
        foreach (var info in _config.ImportedDbs)
            _importedDbs.Add(new DbStructure { DbNumber = info.DbNumber, DbName = info.DbName, SourceFile = info.SourceFile, Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson) ?? [] });
        _importedUdts.Clear();
        foreach (var info in _config.ImportedUdts)
            _importedUdts.Add(new UdtStructure { UdtName = info.UdtName, SourceFile = info.SourceFile, Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson) ?? [] });
        if (_config.ThemeMode == "Light") { ThemeManager.Apply(AppThemeMode.Light); btnTheme.Content = "☀"; }
        if (_config.WindowLeft >= 0 && _config.WindowTop >= 0) { Left = _config.WindowLeft; Top = _config.WindowTop; }
        Width = _config.WindowWidth; Height = _config.WindowHeight;
        if (Enum.TryParse<WindowState>(_config.WindowState, out var ws)) WindowState = ws;
    }

    private void SaveConfig()
    {
        _config.IP = txtIP.Text; _config.Port = TryParse(txtPort.Text, 102); _config.Rack = TryParse(txtRack.Text, 0); _config.Slot = TryParse(txtSlot.Text, 0);
        _config.LocalIP = cmbAdapter.SelectedItem is NetworkAdapter a ? a.Ip : "";
        _config.ManualIAddress = txtIAddress.Text; _config.ManualQAddress = txtQAddress.Text; _config.ManualMAddress = txtMAddress.Text;
        _config.ImportedDbs = _importedDbs.Select(d => new ImportedDbInfo { DbNumber = d.DbNumber, DbName = d.DbName, SourceFile = d.SourceFile, VariablesJson = System.Text.Json.JsonSerializer.Serialize(d.Variables) }).ToList();
        _config.ImportedUdts = _importedUdts.Select(u => new ImportedUdtInfo { UdtName = u.UdtName, SourceFile = u.SourceFile, VariablesJson = System.Text.Json.JsonSerializer.Serialize(u.Variables) }).ToList();
        _config.ThemeMode = ThemeManager.Current == AppThemeMode.Dark ? "Dark" : "Light";
        _config.WindowLeft = Left; _config.WindowTop = Top; _config.WindowWidth = Width; _config.WindowHeight = Height; _config.WindowState = WindowState.ToString();
        _config.Save();
    }
}
