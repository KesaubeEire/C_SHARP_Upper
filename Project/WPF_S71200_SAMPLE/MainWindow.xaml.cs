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

    // ─── 自动轮询 ───
    private readonly PollingScheduler _scheduler = new();
    private readonly ObservableCollection<DbPollItem> _dbItems = [];
    private readonly ObservableCollection<DbStructure> _importedDbs = [];
    private readonly ObservableCollection<UdtStructure> _importedUdts = [];
    private readonly System.Timers.Timer _liveRefreshTimer = new(200);
    private readonly ObservableCollection<string> _fastLiveItems = [];
    private readonly ObservableCollection<string> _dbLiveItems = [];

    // ─── 趋势图 ───
    private readonly MockTrendService _mockTrend = new(100);
    private readonly ObservableCollection<TrendChannelConfig> _trendChannels = [];
    private readonly ObservableCollection<ISeries> _trendSeries = [];
    private readonly ObservableCollection<ISeries> _barSeriesColl = [];
    private readonly Dictionary<string, ObservableCollection<DateTimePoint>> _trendBuffers = [];
    private const int TrendMaxPoints = 300;

    private readonly AppConfig _config = AppConfig.Load();
    private static readonly SKColor[] TrendColors = [SKColors.Crimson, SKColors.Cyan, SKColors.SeaGreen, SKColors.Gold, SKColors.DodgerBlue, SKColors.MediumPurple];

    // ====================== Startup ======================

    public MainWindow()
    {
        InitializeComponent();
        InitTrendPanel();

        listIRows.ItemsSource = _iRows;
        listQRows.ItemsSource = _qRows;
        listMRows.ItemsSource = _mRows;
        UpdateEmptyState();

        listDbItems.ItemsSource = _dbItems;
        listImportedDb.ItemsSource = _importedDbs;
        listImportedUdt.ItemsSource = _importedUdts;
        listFastLive.ItemsSource = _fastLiveItems;
        listDbLive.ItemsSource = _dbLiveItems;
        _liveRefreshTimer.Elapsed += (_, _) => Dispatcher.Invoke(RefreshLiveData);
        UpdateDbEmptyState();

        tabControl.SelectionChanged += TabControl_SelectionChanged;
        RestoreFromConfig();
    }

    // ====================== 趋势图 ======================

    private void InitTrendPanel()
    {
        var defs = new (string key, string label, string color, string unit, double min, double max)[]
        {
            ("ch_temp",  "Reactor Temp",  "#E24B4A", "°C",   60.0, 110.0),
            ("ch_press", "Pressure",      "#37D3E0", "bar",    0.0,  16.0),
            ("ch_flow",  "Feed Flow",     "#1D9E75", "m³/h",  0.0,  50.0),
            ("ch_level", "Tank Level",    "#F4D03F", "%",     0.0, 100.0),
        };

        int idx = 0;
        foreach (var (key, label, color, unit, min, max) in defs)
        {
            var buf = new ObservableCollection<DateTimePoint>();
            _trendBuffers[key] = buf;
            _trendChannels.Add(new TrendChannelConfig { Key = key, Label = label, Color = color, Unit = unit, Min = min, Max = max, Variable = key });
            _trendSeries.Add(new LineSeries<DateTimePoint>
            {
                Values = buf,
                Stroke = new SolidColorPaint(TrendColors[idx % TrendColors.Length]) { StrokeThickness = 2 },
                Fill = null, GeometrySize = 0, LineSmoothness = 0.3, Name = label,
            });
            idx++;
        }
        _trendBuffers["ch_servo"] = new ObservableCollection<DateTimePoint>();
        _trendChannels.Add(new TrendChannelConfig { Key = "ch_servo", Label = "Servo Pos", Color = "#3498DB", Unit = "mm", Min = -10, Max = 90, Variable = "ch_servo" });
        _trendBuffers["ch_current"] = new ObservableCollection<DateTimePoint>();
        _trendChannels.Add(new TrendChannelConfig { Key = "ch_current", Label = "Motor Current", Color = "#9B59B6", Unit = "A", Min = 0, Max = 25, Variable = "ch_current" });

        listTrendChannels.ItemsSource = _trendChannels;

        // X 轴 Labeler：防御 LiveCharts 传入越界/NaN/Inf ticks
        static string SafeTimeLabel(double v)
        {
            if (v is >= 1e18 or <= 0 or double.NaN or double.NegativeInfinity or double.PositiveInfinity)
                return "";
            long ticks = (long)v;
            try { return new DateTime(ticks).ToString("HH:mm:ss"); }
            catch { return ""; }
        }

        cartesianTrend.Series = _trendSeries;
        cartesianTrend.XAxes = [new Axis { Labeler = SafeTimeLabel }];
        cartesianTrend.YAxes = [new Axis()];
        cartesianTrend.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;

        var barVals = new ObservableCollection<double> { 0, 0, 0, 0, 0, 0 };
        _barSeriesColl.Add(new ColumnSeries<double> { Values = barVals, Fill = new SolidColorPaint(SKColor.Parse("#3498DB")), Padding = 2, MaxBarWidth = 40 });
        cartesianBars.Series = _barSeriesColl;
        cartesianBars.XAxes = [new Axis { Labels = ["Temp", "Press", "Flow", "Level", "Servo", "Curr."], LabelsRotation = 45 }];
        cartesianBars.YAxes = [new Axis { MinLimit = 0 }];

        _mockTrend.SampleGenerated += OnTrendSample;
        DrawGaugeScale(45.0);
    }

    private void OnTrendSample(string key, double val, DateTime ts)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_trendBuffers.TryGetValue(key, out var buf)) return;
            buf.Add(new DateTimePoint(ts, val));
            while (buf.Count > TrendMaxPoints) buf.RemoveAt(0);

            var col = (ColumnSeries<double>)_barSeriesColl[0];
            var vals = (ObservableCollection<double>)col.Values!;
            int bi = key switch { "ch_temp" => 0, "ch_press" => 1, "ch_flow" => 2, "ch_level" => 3, "ch_servo" => 4, "ch_current" => 5, _ => -1 };
            if (bi >= 0) vals[bi] = val;
            if (key == "ch_servo") DrawGaugeScale(val);
        });
    }

    private void BtnTrendMock_Click(object sender, RoutedEventArgs e)
    {
        if (_mockTrend.IsRunning) { _mockTrend.Stop(); btnTrendMock.Content = "▶ 启动 Mock"; }
        else { _mockTrend.Start(); btnTrendMock.Content = "■ 停止 Mock"; }
    }

    private void DrawGaugeScale(double needlePos)
    {
        var canvas = canvasGauge;
        if (canvas.ActualWidth < 10) return;
        double w = canvas.ActualWidth, h = canvas.ActualHeight;
        double left = double.TryParse(txtGaugeLeft.Text, out var l) ? l : 0;
        double right = double.TryParse(txtGaugeRight.Text, out var r) ? r : 100;
        int ticks = int.TryParse(txtGaugeTicks.Text, out var tc) ? tc : 10;
        if (ticks < 2) ticks = 2;
        txtGaugePos.Text = $"{needlePos:F1} mm";
        canvas.Children.Clear();

        double pad = 20, drawW = w - pad * 2, range = right - left;
        if (range <= 0) range = 100;

        for (int i = 0; i <= ticks; i++)
        {
            double frac = (double)i / ticks, x = pad + drawW * frac;
            var tick = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = h * 0.35, Stroke = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), StrokeThickness = 1 };
            canvas.Children.Add(tick);
            if (ticks <= 10 || i % 2 == 0)
            {
                double val = left + range * frac;
                var lbl = new TextBlock { Text = $"{val:F0}", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)) };
                Canvas.SetLeft(lbl, x - 12); Canvas.SetTop(lbl, h * 0.38);
                canvas.Children.Add(lbl);
            }
        }

        double nFrac = Math.Clamp((needlePos - left) / range, 0, 1), nx = pad + drawW * nFrac;

        var needle = new System.Windows.Shapes.Rectangle { Width = 3, Height = h * 0.55, Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), RadiusX = 2, RadiusY = 2 };
        Canvas.SetLeft(needle, nx - 1.5); Canvas.SetTop(needle, h * 0.4);
        canvas.Children.Add(needle);

        var tri = new Polygon { Points = new PointCollection { new(nx - 5, h * 0.4 + 10), new(nx + 5, h * 0.4 + 10), new(nx, h * 0.4 - 2) }, Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) };
        canvas.Children.Add(tri);

        var valLbl = new TextBlock { Text = $"{needlePos:F1}", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) };
        Canvas.SetLeft(valLbl, Math.Clamp(nx - 15, 0, w - 35)); Canvas.SetTop(valLbl, 0);
        canvas.Children.Add(valLbl);
    }

    // ====================== Tab 切换 ======================

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isAuto = tabControl.SelectedIndex == 1;
        bool isTrend = tabControl.SelectedIndex == 2;
        manualPanel.Visibility = (isAuto || isTrend) ? Visibility.Collapsed : Visibility.Visible;
        autoPanel.Visibility = isAuto ? Visibility.Visible : Visibility.Collapsed;
        trendPanel.Visibility = isTrend ? Visibility.Visible : Visibility.Collapsed;
    }

    // ====================== 主题切换 ======================

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        bool isDark = ThemeManager.Current == AppThemeMode.Dark;
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
        if (result != 0) { MessageBox.Show(this, $"连接失败:\n{_plc.LastError ?? "错误码: " + result}", "连接错误"); UpdateConnectionUI(); return; }
        SetConnected(ip, port); UpdateConnectionUI(); SaveConfig();
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e) { StopPolling(); _plc.Disconnect(); SetDisconnected(); UpdateConnectionUI(); SaveConfig(); }

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
        btnConnect.IsEnabled = !conn; btnDisconnect.IsEnabled = conn;
        btnIRead.IsEnabled = conn; btnQRead.IsEnabled = conn; btnQWriteMode.IsEnabled = conn;
        btnMRead.IsEnabled = conn; btnMWriteMode.IsEnabled = conn;
        btnStartPoll.IsEnabled = conn && !_scheduler.IsRunning;
        btnStopPoll.IsEnabled = conn && _scheduler.IsRunning;
    }

    // ====================== 手动：地址解析 + 读写 ======================

    private static int[] ParseAddrs(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split(',', '，', ';', '；', ' ')
            .Select(s => s.Trim()).Where(s => int.TryParse(s, out _))
            .Select(int.Parse).Distinct().OrderBy(a => a).ToArray();
    }

    private void BtnIRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddrs(txtIAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址", "提示"); return; }
        _lastIBytes = _plc.ReadBytes(S7Service.AreaI, addrs);
        UpdateRows(_iRows, addrs, _lastIBytes, "I", true); UpdateEmptyState();
    }

    private void BtnQRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddrs(txtQAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址", "提示"); return; }
        _lastQBytes = _plc.ReadBytes(S7Service.AreaQ, addrs);
        UpdateRows(_qRows, addrs, _lastQBytes, "Q", false); UpdateEmptyState();
    }

    private void BtnQWriteMode_Click(object sender, RoutedEventArgs e)
    {
        _qWriteMode = !_qWriteMode;
        btnQWriteMode.Content = _qWriteMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)";
        btnQWriteMode.Background = _qWriteMode ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    }

    private void BtnMRead_Click(object sender, RoutedEventArgs e)
    {
        int[] addrs = ParseAddrs(txtMAddress.Text);
        if (addrs.Length == 0) { MessageBox.Show(this, "请输入有效的字节地址", "提示"); return; }
        _lastMBytes = _plc.ReadBytes(S7Service.AreaM, addrs);
        UpdateRows(_mRows, addrs, _lastMBytes, "M", false); UpdateEmptyState();
    }

    private void BtnMWriteMode_Click(object sender, RoutedEventArgs e)
    {
        _mWriteMode = !_mWriteMode;
        btnMWriteMode.Content = _mWriteMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)";
        btnMWriteMode.Background = _mWriteMode ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    }

    private void BitBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.DataContext is not BitViewModel bit || bit.Parent == null) return;
        bool canWrite = (bit.Parent.AreaLabel == "Q" && _qWriteMode) || (bit.Parent.AreaLabel == "M" && _mWriteMode);
        if (!canWrite) return;
        bit.Toggle();
        _plc.WriteByte(bit.Parent.AreaLabel == "Q" ? S7Service.AreaQ : S7Service.AreaM, bit.Parent.ByteAddress, bit.Parent.ToByte());
    }

    private void UpdateRows(ObservableCollection<ByteRowViewModel> rows, int[] addrs, Dictionary<int, byte> data, string label, bool ro)
    {
        rows.Clear();
        foreach (int a in addrs) rows.Add(new ByteRowViewModel(a, label, ro) { Value = data.GetValueOrDefault(a, (byte)0) });
    }

    private void UpdateEmptyState()
    {
        txtIEmpty.Visibility = _iRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtQEmpty.Visibility = _qRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtMEmpty.Visibility = _mRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ====================== 自动轮询 ======================

    private void BtnAddDb_Click(object sender, RoutedEventArgs e)
    {
        int dbNum = TryParse(txtNewDbNumber.Text, 1), offset = TryParse(txtNewDbOffset.Text, 0), length = TryParse(txtNewDbLen.Text, 100);
        if (_dbItems.Any(d => d.DbNumber == dbNum && d.Offset == offset)) { MessageBox.Show(this, $"DB{dbNum} @{offset} 已在列表中", "提示"); return; }
        _dbItems.Add(new DbPollItem { DbNumber = dbNum, Offset = offset, Length = Math.Min(length, 222), Status = "待启动" });
        UpdateDbEmptyState();
        txtNewDbNumber.Text = (dbNum + 1).ToString(); SaveConfig();
    }

    private void BtnRemoveDb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DbPollItem item) { _dbItems.Remove(item); SaveConfig(); UpdateDbEmptyState(); }
    }

    private void UpdateDbEmptyState() => txtDbEmpty.Visibility = _dbItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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

    private void BtnStartPoll_Click(object sender, RoutedEventArgs e)
    {
        if (!_plc.IsConnected) { MessageBox.Show(this, "请先连接 PLC", "提示"); return; }
        int iS = TryParse(txtIStart.Text, 0), iE = TryParse(txtIEnd.Text, 2), qS = TryParse(txtQStart.Text, 0), qE = TryParse(txtQEnd.Text, 1), mS = TryParse(txtMStart.Text, 0), mE = TryParse(txtMEnd.Text, 10);
        var cfg = _scheduler.Config;
        cfg.Fast.IStart = iS; cfg.Fast.IEnd = iE; cfg.Fast.EnableI = chkI.IsChecked == true;
        cfg.Fast.QStart = qS; cfg.Fast.QEnd = qE; cfg.Fast.EnableQ = chkQ.IsChecked == true;
        cfg.Fast.MStart = mS; cfg.Fast.MEnd = mE; cfg.Fast.EnableM = chkM.IsChecked == true;
        cfg.DbItems.Clear();
        foreach (var item in _dbItems) cfg.DbItems.Add(item);
        _scheduler.Start(txtIP.Text.Trim(), TryParse(txtPort.Text, 102), TryParse(txtRack.Text, 0), TryParse(txtSlot.Text, 0));
        if (!_scheduler.IsConnected) { MessageBox.Show(this, $"轮询连接失败:\n{_scheduler.LastError}", "错误"); return; }
        _liveRefreshTimer.Start();
        txtPollStatus.Text = "● 轮询中"; txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        UpdateConnectionUI();
    }

    private void BtnStopPoll_Click(object sender, RoutedEventArgs e) { StopPolling(); }

    private void StopPolling()
    {
        _scheduler.Stop(); _liveRefreshTimer.Stop();
        txtPollStatus.Text = "■ 已停止"; txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
        UpdateConnectionUI();
    }

    private void RefreshLiveData()
    {
        var values = _scheduler.LastValues;
        if (values.Count == 0) { txtLiveEmpty.Visibility = Visibility.Visible; return; }
        txtLiveEmpty.Visibility = Visibility.Collapsed;
        _fastLiveItems.Clear();
        foreach (var key in values.Keys.Where(k => k[0] is 'I' or 'Q' or 'M').OrderBy(k => k).Take(50)) _fastLiveItems.Add($"{key}: 0x{values[key]:X2}");
        _dbLiveItems.Clear();
        foreach (var key in values.Keys.Where(k => k.StartsWith("DB")).OrderBy(k => k).Take(80)) _dbLiveItems.Add($"{key}: 0x{values[key]:X2}");
    }

    // ====================== 工具 + 关闭 ======================

    private static int TryParse(string s, int def) => int.TryParse(s?.Trim(), out int r) ? r : def;

    protected override void OnClosed(EventArgs e)
    {
        _mockTrend.SampleGenerated -= OnTrendSample;
        SaveConfig(); _scheduler.Dispose(); _plc.Dispose(); _liveRefreshTimer.Dispose(); _mockTrend.Dispose();
        base.OnClosed(e);
    }

    // ====================== 配置持久化 ======================

    private void RestoreFromConfig()
    {
        txtIP.Text = _config.IP; txtPort.Text = _config.Port.ToString();
        txtRack.Text = _config.Rack.ToString(); txtSlot.Text = _config.Slot.ToString();
        txtIAddress.Text = _config.ManualIAddress; txtQAddress.Text = _config.ManualQAddress; txtMAddress.Text = _config.ManualMAddress;
        txtIStart.Text = _config.PollIStart.ToString(); txtIEnd.Text = _config.PollIEnd.ToString();
        txtQStart.Text = _config.PollQStart.ToString(); txtQEnd.Text = _config.PollQEnd.ToString();
        txtMStart.Text = _config.PollMStart.ToString(); txtMEnd.Text = _config.PollMEnd.ToString();
        chkI.IsChecked = _config.PollEnableI; chkQ.IsChecked = _config.PollEnableQ; chkM.IsChecked = _config.PollEnableM;
        txtPollInterval.Text = _config.PollIntervalMs.ToString();
        _dbItems.Clear(); foreach (var item in _config.DbItems) _dbItems.Add(item);
        _importedDbs.Clear();
        foreach (var info in _config.ImportedDbs)
        {
            var db = new DbStructure { DbNumber = info.DbNumber, DbName = info.DbName, SourceFile = info.SourceFile, Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson) ?? [] };
            _importedDbs.Add(db);
        }
        _importedUdts.Clear();
        foreach (var info in _config.ImportedUdts)
        {
            var udt = new UdtStructure { UdtName = info.UdtName, SourceFile = info.SourceFile, Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson) ?? [] };
            _importedUdts.Add(udt);
        }
        UpdateDbEmptyState();
        if (_config.ThemeMode == "Light") { ThemeManager.Apply(AppThemeMode.Light); btnTheme.Content = "☀"; }
        if (_config.WindowLeft >= 0 && _config.WindowTop >= 0) { Left = _config.WindowLeft; Top = _config.WindowTop; }
        Width = _config.WindowWidth; Height = _config.WindowHeight;
        if (Enum.TryParse<WindowState>(_config.WindowState, out var ws)) WindowState = ws;
    }

    private void SaveConfig()
    {
        _config.IP = txtIP.Text; _config.Port = TryParse(txtPort.Text, 102); _config.Rack = TryParse(txtRack.Text, 0); _config.Slot = TryParse(txtSlot.Text, 0);
        _config.ManualIAddress = txtIAddress.Text; _config.ManualQAddress = txtQAddress.Text; _config.ManualMAddress = txtMAddress.Text;
        _config.PollIStart = TryParse(txtIStart.Text, 0); _config.PollIEnd = TryParse(txtIEnd.Text, 2);
        _config.PollQStart = TryParse(txtQStart.Text, 0); _config.PollQEnd = TryParse(txtQEnd.Text, 1);
        _config.PollMStart = TryParse(txtMStart.Text, 0); _config.PollMEnd = TryParse(txtMEnd.Text, 10);
        _config.PollEnableI = chkI.IsChecked == true; _config.PollEnableQ = chkQ.IsChecked == true; _config.PollEnableM = chkM.IsChecked == true;
        _config.PollIntervalMs = TryParse(txtPollInterval.Text, 50);
        _config.DbItems = _dbItems.ToList();
        _config.ImportedDbs = _importedDbs.Select(d => new ImportedDbInfo { DbNumber = d.DbNumber, DbName = d.DbName, SourceFile = d.SourceFile, VariablesJson = System.Text.Json.JsonSerializer.Serialize(d.Variables) }).ToList();
        _config.ImportedUdts = _importedUdts.Select(u => new ImportedUdtInfo { UdtName = u.UdtName, SourceFile = u.SourceFile, VariablesJson = System.Text.Json.JsonSerializer.Serialize(u.Variables) }).ToList();
        _config.ThemeMode = ThemeManager.Current == AppThemeMode.Dark ? "Dark" : "Light";
        _config.WindowLeft = Left; _config.WindowTop = Top; _config.WindowWidth = Width; _config.WindowHeight = Height; _config.WindowState = WindowState.ToString();
        _config.Save();
    }
}
