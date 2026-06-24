using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        // 构造函数：直接从配置恢复字段（不走属性 setter，避免触发 Save）
        LoadAdapters();
        RestoreConnectionParams();
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
        set
        {
            if (SetProperty(ref _selectedAdapter, value))
            {
                _config.LocalIP = value?.Ip ?? "";
                _config.Save();
            }
        }
    }

    // ===== Connection params =====

    private string _ipAddress = "192.168.0.1";
    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (SetProperty(ref _ipAddress, value))
            {
                _config.IP = value;
                _config.Save();
            }
        }
    }

    private string _port = "102";
    public string Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                if (int.TryParse(value, out int p)) _config.Port = p;
                _config.Save();
            }
        }
    }

    private string _rack = "0";
    public string Rack
    {
        get => _rack;
        set
        {
            if (SetProperty(ref _rack, value))
            {
                if (int.TryParse(value, out int r)) _config.Rack = r;
                _config.Save();
            }
        }
    }

    private string _slot = "1";
    public string Slot
    {
        get => _slot;
        set
        {
            if (SetProperty(ref _slot, value))
            {
                if (int.TryParse(value, out int s)) _config.Slot = s;
                _config.Save();
            }
        }
    }

    private string _pollInterval = "500";
    public string PollInterval
    {
        get => _pollInterval;
        set
        {
            if (SetProperty(ref _pollInterval, value))
            {
                if (int.TryParse(value, out int ms)) _config.PollInterval = ms;
                _config.Save();
            }
        }
    }

    // ===== Initialize =====

    private void LoadAdapters()
    {
        Adapters.Clear();
        foreach (var adapter in NetworkAdapter.Enumerate())
            Adapters.Add(adapter);

        // 恢复上次选择的本机网卡
        if (!string.IsNullOrEmpty(_config.LocalIP))
        {
            var matched = Adapters.FirstOrDefault(a =>
                string.Equals(a.Ip, _config.LocalIP, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                _selectedAdapter = matched;
                OnPropertyChanged(nameof(SelectedAdapter));
                return;
            }
        }

        if (Adapters.Count > 0)
        {
            _selectedAdapter = Adapters[0];
            OnPropertyChanged(nameof(SelectedAdapter));
        }
    }

    /// <summary>直接从配置恢复连接参数（写字段不走 setter，不触发 Save）</summary>
    private void RestoreConnectionParams()
    {
        SetField(ref _ipAddress, _config.IP, nameof(IpAddress));
        SetField(ref _port, _config.Port.ToString(), nameof(Port));
        SetField(ref _rack, _config.Rack.ToString(), nameof(Rack));
        SetField(ref _slot, _config.Slot.ToString(), nameof(Slot));
        SetField(ref _pollInterval, _config.PollInterval.ToString(), nameof(PollInterval));
    }

    /// <summary>更新字段并通知 UI，不触发额外逻辑</summary>
    private void SetField<T>(ref T field, T value, string propertyName)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
        }
    }

    // ===== Commands =====

    [RelayCommand]
    private async Task Connect()
    {
        string localIp = SelectedAdapter?.Ip ?? "";
        string ip = IpAddress.Trim();
        int port = int.TryParse(Port, out int p) ? p : 102;
        int rack = int.TryParse(Rack, out int r) ? r : 0;
        int slot = int.TryParse(Slot, out int s) ? s : 1;

        try
        {
            // 异步执行 TCP 连接，不阻塞 UI
            int result = await Task.Run(() => _s7.Connect(localIp, ip, port, rack, slot));

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
                await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "连接失败",
                    Content = _s7.LastError ?? "未知错误"
                }.ShowDialogAsync();
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ShowStatusBar = true;
            StatusText = $"连接异常: {ex.Message}";
            ConnectionQuality = LedQuality.Bad;
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "连接异常",
                Content = ex.Message
            }.ShowDialogAsync();
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
        _scheduler.Config.Fast.PollIAddr = _config.ManualIAddress;
        _scheduler.Config.Fast.PollQAddr = _config.ManualQAddress;
        _scheduler.Config.Fast.PollMAddr = _config.ManualMAddress;

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
