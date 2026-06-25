using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Wpf.Ui.Gallery.Controls.Plc;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.ViewModels.Plc;

public partial class PpeConnectionSectionViewModel : ObservableObject
{
    private readonly S7Service _s7;
    private readonly AppConfigService _config;
    private readonly PollingScheduler _scheduler;
    private readonly IContentDialogService _contentDialog;

    public PpeConnectionSectionViewModel(S7Service s7, PollingScheduler scheduler, AppConfigService config,
        IContentDialogService contentDialog)
    {
        _s7 = s7;
        _scheduler = scheduler;
        _config = config;
        _contentDialog = contentDialog;

        // 构造函数：直接从配置恢复字段（不走属性 setter，避免触发 Save）
        LoadAdapters();
        RestoreConnectionParams();

        // 始终监听轮询状态事件（无论从哪启动/停止）
        _scheduler.DataUpdated += OnPollDataUpdated;
        _scheduler.PollingStarted += OnPollingStarted;
        _scheduler.PollingStopped += OnPollingStopped;
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
    [NotifyPropertyChangedFor(nameof(LatencyColor))]
    private string _latencyText = "--";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isPolling;

    [ObservableProperty]
    private bool _showStatusBar;

    public Brush LatencyColor
    {
        get
        {
            if (long.TryParse(LatencyText?.Replace(" ms", ""), out var ms))
            {
                if (ms < 100)
                    return (Application.Current.TryFindResource("SystemFillColorSuccessBrush")
                        ?? Application.Current.TryFindResource("SystemFillColorAttentionBrush")
                        ?? new SolidColorBrush(Colors.Green)) as Brush ?? new SolidColorBrush(Colors.Green);
                if (ms < 250)
                    return (Application.Current.TryFindResource("SystemFillColorCautionBrush")
                        ?? new SolidColorBrush(Colors.Orange)) as Brush ?? new SolidColorBrush(Colors.Orange);
                return (Application.Current.TryFindResource("SystemFillColorCriticalBrush")
                    ?? new SolidColorBrush(Colors.Red)) as Brush ?? new SolidColorBrush(Colors.Red);
            }
            return (Application.Current.TryFindResource("TextFillColorSecondaryBrush")
                ?? new SolidColorBrush(Colors.Gray)) as Brush ?? new SolidColorBrush(Colors.Gray);
        }
    }

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
                await _contentDialog.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = "连接失败",
                        Content = _s7.LastError ?? "未知错误",
                        CloseButtonText = "确定",
                    });
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ShowStatusBar = true;
            StatusText = $"连接异常: {ex.Message}";
            ConnectionQuality = LedQuality.Bad;
            await _contentDialog.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "连接异常",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                });
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
            await _contentDialog.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "提示",
                    Content = "请先连接 PLC",
                    CloseButtonText = "确定",
                });
            return;
        }

        if (!int.TryParse(PollInterval, out int interval))
            interval = 500;

        _scheduler.Config.FastInterval = interval;
        _scheduler.Config.DbIp = IpAddress.Trim();
        _scheduler.Config.DbRack = int.TryParse(Rack, out int rack) ? rack : 0;
        _scheduler.Config.DbSlot = int.TryParse(Slot, out int slot) ? slot : 1;
        _scheduler.Config.Fast.PollIAddr = _config.ManualIAddress;
        _scheduler.Config.Fast.PollQAddr = _config.ManualQAddress;
        _scheduler.Config.Fast.PollMAddr = _config.ManualMAddress;

        _scheduler.Start(_s7);
        // 状态更新由 OnPollingStarted 事件处理
    }

    [RelayCommand]
    private void StopPolling()
    {
        _scheduler.Stop();
        // 状态更新由 OnPollingStopped 事件处理
    }

    private void OnPollDataUpdated(HashSet<string> _)
    {
        UpdateLatency(_scheduler.LatencyMs);
    }

    private void OnPollingStarted()
    {
        IsPolling = _scheduler.IsRunning;
        PollStatusText = _scheduler.IsRunning ? "轮询运行中" : "轮询启动失败";
        PollQuality = _scheduler.IsRunning ? LedQuality.Good : LedQuality.Bad;
    }

    private void OnPollingStopped()
    {
        IsPolling = false;
        PollStatusText = "已停止";
        PollQuality = LedQuality.Disabled;
    }

    public void UpdateLatency(long ms)
    {
        LatencyText = $"{ms} ms";
    }
}
