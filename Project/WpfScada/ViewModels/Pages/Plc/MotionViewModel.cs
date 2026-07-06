using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfScada.Services.Motion;

namespace WpfScada.ViewModels.Pages.Plc;

public partial class MotionViewModel : ViewModel
{
    private readonly IMotionController _motion;
    private readonly System.Timers.Timer _pollTimer;

    public MotionViewModel(IMotionController motion)
    {
        _motion = motion;

        // 轴状态列表（UI 绑定）
        for (int i = 1; i <= 4; i++)
            Axes.Add(new AxisViewModel(i) { Controller = motion });

        _pollTimer = new System.Timers.Timer(100);
        _pollTimer.Elapsed += OnPollTick;
        _pollTimer.AutoReset = true;
    }

    // ==================== 连接 ====================

    [ObservableProperty] private string _address = "192.168.0.100";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionStatus = "未连接";

    [RelayCommand]
    private void Connect()
    {
        if (_motion.Connect(Address))
        {
            IsConnected = true;
            ConnectionStatus = $"已连接 — {Address}";
            _pollTimer.Start();
        }
        else
        {
            ConnectionStatus = $"连接失败: {_motion.GetLastError()}";
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _pollTimer.Stop();
        _motion.Disconnect();
        IsConnected = false;
        ConnectionStatus = "已断开";
    }

    // ==================== 轴数据 ====================

    public ObservableCollection<AxisViewModel> Axes { get; } = [];

    // ==================== 急停 ====================

    [RelayCommand]
    private void EmergencyStop()
    {
        _motion.EmergencyStop();
    }

    // ==================== IO ====================

    [ObservableProperty] private ObservableCollection<IoPointViewModel> _diPoints = [];
    [ObservableProperty] private ObservableCollection<IoPointViewModel> _doPoints = [];

    private void RefreshIo()
    {
        var di = new ObservableCollection<IoPointViewModel>();
        var dout = new ObservableCollection<IoPointViewModel>();
        for (int i = 1; i <= 8; i++)
        {
            di.Add(new IoPointViewModel { Index = i, Value = _motion.ReadDI(i) });
            dout.Add(new IoPointViewModel { Index = i, Value = false });
        }
        DiPoints = di;
        DoPoints = dout;
    }

    [RelayCommand]
    private void ToggleDo(int index)
    {
        bool current = _motion.ReadDI(index);
        _motion.WriteDO(index, !current);
        RefreshIo();
    }

    // ==================== 轮询 ====================

    private void OnPollTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        foreach (var axis in Axes)
        {
            axis.CommandPos = _motion.GetCommandPosition(axis.Index);
            axis.EncoderPos = _motion.GetEncoderPosition(axis.Index);
            axis.StatusValue = _motion.GetAxisStatus(axis.Index);
            axis.IsMoving = _motion.IsMoving(axis.Index);
            axis.IsServoOn = _motion.IsServoOn(axis.Index);
        }
    }

    // ==================== 清理 ====================

    public void Cleanup()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        if (_motion is IDisposable d) d.Dispose();
    }
}

/// <summary>
/// 单轴 ViewModel
/// </summary>
public partial class AxisViewModel : ObservableObject
{
    public AxisViewModel(int index) => Index = index;

    public int Index { get; }

    [ObservableProperty] private double _commandPos;
    [ObservableProperty] private double _encoderPos;
    [ObservableProperty] private int _statusValue;
    [ObservableProperty] private bool _isMoving;
    [ObservableProperty] private bool _isServoOn;

    // ======= 运动参数 =======

    [ObservableProperty] private double _targetPos;
    [ObservableProperty] private double _moveDistance = 1000;
    [ObservableProperty] private double _velocity = 5000;
    [ObservableProperty] private double _jogVelocity = 2000;
    [ObservableProperty] private int _homeMode = 3;

    // ======= 状态文本 =======

    public string StatusText => StatusValue switch
    {
        0 => "空闲",
        1 => "运行中",
        2 => "报警",
        3 => "回零中",
        4 => "急停",
        _ => "未知",
    };

    // ======= 命令 =======

    public IMotionController? Controller { get; set; }

    [RelayCommand]
    private void DoServoOn() => Controller?.ServoOn(Index);

    [RelayCommand]
    private void DoServoOff() => Controller?.ServoOff(Index);

    [RelayCommand]
    private void DoMoveAbs() => Controller?.MoveAbs(Index, TargetPos, Velocity);

    [RelayCommand]
    private void DoMoveRel() => Controller?.MoveRel(Index, MoveDistance, Velocity);

    [RelayCommand]
    private void DoJogFwd() => Controller?.Jog(Index, JogVelocity);

    [RelayCommand]
    private void DoJogRev() => Controller?.Jog(Index, -JogVelocity);

    [RelayCommand]
    private void DoJogStop() => Controller?.JogStop(Index);

    [RelayCommand]
    private void DoHome() => Controller?.Home(Index, HomeMode);

    [RelayCommand]
    private void DoStop() => Controller?.Halt(Index);

    [RelayCommand]
    private void DoClearAlarm() => Controller?.ClearAlarm(Index);
}

/// <summary>
/// IO 点 ViewModel
/// </summary>
public partial class IoPointViewModel : ObservableObject
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private bool _value;

    public string Label => $"DI{Index}";
}
