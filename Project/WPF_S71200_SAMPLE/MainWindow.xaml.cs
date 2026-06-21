using System.Windows;
using System.Windows.Controls;
using TestWpf.Models;
using TestWpf.Services;

namespace TestWpf;

public partial class MainWindow : Window
{
    private readonly S7Service _plc = new();
    private readonly PollingScheduler _scheduler = new();
    private readonly AppConfig _config = AppConfig.Load();

    public MainWindow()
    {
        InitializeComponent();

        // 注入 Service
        connectionPanel.Init(_plc);
        iPanel.Init(_plc);
        qPanel.Init(_plc);
        mPanel.Init(_plc);

        // 事件连线
        connectionPanel.ConnectionChanged += OnConnectionChanged;
        pollingPanel.StartRequested += OnPollStart;
        pollingPanel.StopRequested += OnPollStop;
        _scheduler.DataUpdated += OnPollData;
        tabControl.SelectionChanged += OnTabChanged;

        // 恢复配置
        RestoreFromConfig();
    }

    // ===== 连接状态变化 =====

    private void OnConnectionChanged(object? sender, bool connected)
    {
        pollingPanel.SetReady();
        _scheduler.Stop();
    }

    // ===== 轮询启动/停止 =====

    private void OnPollStart(object? sender, EventArgs e)
    {
        if (!_plc.IsConnected) { MessageBox.Show(this, "请先连接 PLC", "提示"); return; }

        int interval = pollingPanel.Interval;
        int port = connectionPanel.Port;
        int rack = connectionPanel.Rack;
        int slot = connectionPanel.Slot;

        var cfg = _scheduler.Config;
        cfg.Fast.PollIAddr = iPanel.AddressText;
        cfg.Fast.PollQAddr = qPanel.AddressText;
        cfg.Fast.PollMAddr = mPanel.AddressText;
        cfg.FastInterval = interval;
        cfg.DbIp = connectionPanel.IP;
        cfg.DbRack = rack;
        cfg.DbSlot = slot;

        _scheduler.Start(_plc, port);
        if (!_scheduler.IsConnected)
        {
            MessageBox.Show(this, $"轮询连接失败:\n{_scheduler.LastError}", "错误");
            return;
        }
        pollingPanel.SetRunning(true);
    }

    private void OnPollStop(object? sender, EventArgs e)
    {
        _scheduler.Stop();
        pollingPanel.SetReady();
    }

    private void OnPollData(HashSet<string> updated)
    {
        Dispatcher.InvokeAsync(() =>
        {
            pollingPanel.UpdateLatency(_scheduler.LatencyMs);
            iPanel.UpdateFromPoll(updated, _scheduler);
            qPanel.UpdateFromPoll(updated, _scheduler);
            mPanel.UpdateFromPoll(updated, _scheduler);
        });
    }

    // ===== Tab 切换 =====

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        bool isTab0 = tabControl.SelectedIndex == 0;
        bool isTab1 = tabControl.SelectedIndex == 1;
        bool isTab2 = tabControl.SelectedIndex == 2;

        ioPanel.Visibility = isTab0 ? Visibility.Visible : Visibility.Collapsed;
        trendPanel.Visibility = isTab1 ? Visibility.Visible : Visibility.Collapsed;
        gaugePanel.Visibility = isTab2 ? Visibility.Visible : Visibility.Collapsed;

        if (isTab1) trendPanel.NeedsGaugeDraw(45.0);
    }

    // ===== 主题切换 =====

    private void OnThemeToggle(object sender, RoutedEventArgs e)
    {
        bool d = ThemeManager.Current == AppThemeMode.Dark;
        ThemeManager.Toggle();
        btnTheme.Content = d ? "☀" : "🌙";
        SaveConfig();
    }

    // ===== 配置持久化 =====

    private void RestoreFromConfig()
    {
        connectionPanel.RestoreConfig(_config.IP, _config.Port, _config.Rack, _config.Slot, _config.LocalIP);

        importPanel.Restore(
            _config.ImportedDbs.Select(d => new DbStructure
            {
                DbNumber = d.DbNumber,
                DbName = d.DbName,
                SourceFile = d.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(d.VariablesJson) ?? []
            }),
            _config.ImportedUdts.Select(u => new UdtStructure
            {
                UdtName = u.UdtName,
                SourceFile = u.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(u.VariablesJson) ?? []
            })
        );

        if (_config.ThemeMode == "Light") { ThemeManager.Apply(AppThemeMode.Light); btnTheme.Content = "☀"; }
        if (_config.WindowLeft >= 0 && _config.WindowTop >= 0) { Left = _config.WindowLeft; Top = _config.WindowTop; }
        Width = _config.WindowWidth; Height = _config.WindowHeight;
        if (Enum.TryParse<WindowState>(_config.WindowState, out var ws)) WindowState = ws;
    }

    private void SaveConfig()
    {
        _config.IP = connectionPanel.IP;
        _config.Port = connectionPanel.Port;
        _config.Rack = connectionPanel.Rack;
        _config.Slot = connectionPanel.Slot;
        _config.LocalIP = connectionPanel.LocalIP;
        _config.ThemeMode = ThemeManager.Current == AppThemeMode.Dark ? "Dark" : "Light";
        _config.WindowLeft = Left; _config.WindowTop = Top;
        _config.WindowWidth = Width; _config.WindowHeight = Height;
        _config.WindowState = WindowState.ToString();

        _config.ImportedDbs = importPanel.ImportedDbs.Select(d => new ImportedDbInfo
        {
            DbNumber = d.DbNumber, DbName = d.DbName,
            SourceFile = d.SourceFile,
            VariablesJson = System.Text.Json.JsonSerializer.Serialize(d.Variables)
        }).ToList();
        _config.ImportedUdts = importPanel.ImportedUdts.Select(u => new ImportedUdtInfo
        {
            UdtName = u.UdtName, SourceFile = u.SourceFile,
            VariablesJson = System.Text.Json.JsonSerializer.Serialize(u.Variables)
        }).ToList();

        _config.Save();
    }

    protected override void OnClosed(EventArgs e)
    {
        trendPanel.Stop();
        _scheduler.Dispose();
        SaveConfig();
        _plc.Dispose();
        base.OnClosed(e);
    }
}
