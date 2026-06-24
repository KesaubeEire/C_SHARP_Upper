using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wpf.Ui.Gallery.Controls.Plc;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.ViewModels.Plc;

public partial class PpeConnectionSectionViewModel : ObservableObject
{
    private readonly S7Service _s7;
    private readonly AppConfigService _config;
    private readonly PollingScheduler _scheduler;

    public PpeConnectionSectionViewModel(S7Service s7, PollingScheduler scheduler, AppConfigService config)
    {
        _s7 = s7;
        _scheduler = scheduler;
        _config = config;
    }

    // ===== Observable Properties =====

    [ObservableProperty]
    private string _statusText = "未连接";

    [ObservableProperty]
    private LedQuality _connectionQuality = LedQuality.Disabled;

    [ObservableProperty]
    private string _pollStatusText = "就绪";

    [ObservableProperty]
    private LedQuality _pollQuality = LedQuality.Disabled;

    [ObservableProperty]
    private string _latencyText = "--";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isPolling;

    [ObservableProperty]
    private bool _showStatusBar;

    // ===== Network adapters =====

    public ObservableCollection<NetworkAdapter> Adapters { get; } = [];

    private NetworkAdapter? _selectedAdapter;
    public NetworkAdapter? SelectedAdapter
    {
        get => _selectedAdapter;
        set => SetProperty(ref _selectedAdapter, value);
    }

    // ===== Connection params =====

    private string _ipAddress = "192.168.0.1";
    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    private string _port = "102";
    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private string _rack = "0";
    public string Rack
    {
        get => _rack;
        set => SetProperty(ref _rack, value);
    }

    private string _slot = "1";
    public string Slot
    {
        get => _slot;
        set => SetProperty(ref _slot, value);
    }

    private string _pollInterval = "500";
    public string PollInterval
    {
        get => _pollInterval;
        set => SetProperty(ref _pollInterval, value);
    }

    // ===== Import lists =====

    public ObservableCollection<DbStructure> ImportedDbs { get; } = [];
    public ObservableCollection<UdtStructure> ImportedUdts { get; } = [];

    public event EventHandler? ListChanged;

    // ===== Initialize =====

    public void OnLoaded()
    {
        LoadAdapters();
        RestoreImports();
    }

    private void LoadAdapters()
    {
        Adapters.Clear();
        foreach (var adapter in NetworkAdapter.Enumerate())
            Adapters.Add(adapter);
        if (Adapters.Count > 0)
            SelectedAdapter = Adapters[0];
    }

    private void RestoreImports()
    {
        foreach (var d in _config.ImportedDbs)
        {
            ImportedDbs.Add(new DbStructure
            {
                DbNumber = d.DbNumber,
                DbName = d.DbName,
                SourceFile = d.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(d.VariablesJson) ?? []
            });
        }
        foreach (var u in _config.ImportedUdts)
        {
            ImportedUdts.Add(new UdtStructure
            {
                UdtName = u.UdtName,
                SourceFile = u.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(u.VariablesJson) ?? []
            });
        }
    }

    // ===== Commands =====

    [RelayCommand]
    private void Connect()
    {
        string localIp = SelectedAdapter?.Ip ?? "";
        string ip = IpAddress.Trim();
        int port = int.TryParse(Port, out int p) ? p : 102;
        int rack = int.TryParse(Rack, out int r) ? r : 0;
        int slot = int.TryParse(Slot, out int s) ? s : 1;

        int result = _s7.Connect(localIp, ip, port, rack, slot);
        IsConnected = result == 0;
        ShowStatusBar = true;

        if (result == 0)
        {
            StatusText = "已连接";
            ConnectionQuality = LedQuality.Good;
        }
        else
        {
            StatusText = $"连接失败: {_s7.LastError ?? "未知错误"}";
            ConnectionQuality = LedQuality.Bad;
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _s7.Disconnect();
        IsConnected = false;
        ConnectionQuality = LedQuality.Disabled;
        StatusText = "已断开";
    }

    [RelayCommand]
    private async Task ImportDb()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DB 文件|*.db;*.txt|All files|*.*",
            Title = "导入 DB 结构"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var db = DbFileParser.Parse(dialog.FileName);
            db.DbNumber = 1;
            ImportedDbs.Add(db);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "错误", Content = $"导入失败: {ex.Message}" }.ShowDialogAsync();
        }
    }

    [RelayCommand]
    private async Task ImportUdt()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "UDT 文件|*.udt;*.txt|All files|*.*",
            Title = "导入 UDT 结构"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var udt = UdtFileParser.Parse(dialog.FileName);
            ImportedUdts.Add(udt);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "错误", Content = $"导入失败: {ex.Message}" }.ShowDialogAsync();
        }
    }

    private void OnDeleteDb(DbStructure db)
    {
        ImportedDbs.Remove(db);
        ListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteUdt(UdtStructure udt)
    {
        ImportedUdts.Remove(udt);
        ListChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task StartPolling()
    {
        if (!_s7.IsConnected)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "提示", Content = "请先连接 PLC" }.ShowDialogAsync();
            return;
        }

        if (!int.TryParse(PollInterval, out int interval))
            interval = 500;

        _scheduler.Config.FastInterval = interval;
        _scheduler.Config.Fast.PollIAddr = "0,1,8";
        _scheduler.Config.Fast.PollQAddr = "0";
        _scheduler.Config.Fast.PollMAddr = "0";

        _scheduler.DataUpdated += OnPollDataUpdated;
        _scheduler.Start(_s7);
        IsPolling = _scheduler.IsConnected;

        if (IsPolling)
        {
            PollStatusText = "轮询运行中";
            PollQuality = LedQuality.Good;
        }
        else
        {
            PollStatusText = "轮询启动失败";
            PollQuality = LedQuality.Bad;
        }
    }

    [RelayCommand]
    private void StopPolling()
    {
        _scheduler.DataUpdated -= OnPollDataUpdated;
        _scheduler.Stop();
        IsPolling = false;
        PollStatusText = "已停止";
        PollQuality = LedQuality.Disabled;
    }

    private void OnPollDataUpdated(HashSet<string> _)
    {
        UpdateLatency(_scheduler.LatencyMs);
    }

    public void UpdateLatency(long ms)
    {
        LatencyText = $"{ms} ms";
    }
}
