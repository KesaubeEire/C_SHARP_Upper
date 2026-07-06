using System.Timers;
using Timer = System.Timers.Timer;

namespace WpfScada.Services.Motion;

/// <summary>
/// 运动控制卡模拟器。不依赖硬件，随机数模拟位置/状态/IO。
/// 用于无硬件时的开发和测试。
/// </summary>
public sealed class MockMotionController : IMotionController, IDisposable
{
    private readonly Dictionary<int, AxisState> _axes = [];
    private readonly Dictionary<int, bool> _digitalOutputs = [];
    private readonly Dictionary<int, bool> _digitalInputs = [];
    private readonly Timer _simTimer;
    private readonly Random _rng = new();
    private bool _connected;
    private string _lastError = string.Empty;
    private string _address = string.Empty;

    private sealed class AxisState
    {
        public double CommandPos;
        public double EncoderPos;
        public double TargetPos;
        public double Velocity;
        public double TargetVelocity;
        public bool ServoOn;
        public bool Moving;
        public bool Jogging;
        public bool Homing;
        public int Status; // 0=空闲 1=运行中 2=报警 3=回零中 4=急停
        public int Alarms;
    }

    public MockMotionController()
    {
        for (int i = 1; i <= 4; i++)
        {
            _axes[i] = new AxisState();
            _digitalOutputs[i] = false;
            _digitalInputs[i] = i <= 4; // 前 4 个 DI 模拟限位开关常闭
        }

        _simTimer = new Timer(50); // 50ms 模拟周期
        _simTimer.Elapsed += OnSimTick;
        _simTimer.AutoReset = true;
    }

    public bool IsConnected => _connected;

    public bool Connect(string address)
    {
        _address = address;
        _connected = true;
        _simTimer.Start();
        return true;
    }

    public void Disconnect()
    {
        _simTimer.Stop();
        // 停止所有轴
        foreach (var axis in _axes.Values)
        {
            axis.Moving = false;
            axis.Jogging = false;
            axis.Homing = false;
            axis.Velocity = 0;
            axis.TargetVelocity = 0;
            axis.Status = 0;
        }
        _connected = false;
    }

    public void ServoOn(int axis)
    {
        if (!_axes.TryGetValue(axis, out var state)) return;
        state.ServoOn = true;
        state.Alarms = 0;
        state.Status = 0;
    }

    public void ServoOff(int axis)
    {
        if (!_axes.TryGetValue(axis, out var state)) return;
        state.ServoOn = false;
        state.Moving = false;
        state.Jogging = false;
        state.Homing = false;
        state.Velocity = 0;
        state.TargetVelocity = 0;
    }

    public bool IsServoOn(int axis) =>
        _axes.TryGetValue(axis, out var state) && state.ServoOn;

    public void MoveAbs(int axis, double position, double velocity)
    {
        if (!_axes.TryGetValue(axis, out var state) || !state.ServoOn)
        {
            _lastError = $"轴 {axis} 未使能";
            return;
        }
        state.TargetPos = position;
        state.Moving = true;
        state.Jogging = false;
        state.Homing = false;
        state.Velocity = velocity;
        state.TargetVelocity = velocity;
        state.Status = 1;
    }

    public void MoveRel(int axis, double distance, double velocity)
    {
        if (!_axes.TryGetValue(axis, out var state) || !state.ServoOn)
        {
            _lastError = $"轴 {axis} 未使能";
            return;
        }
        state.TargetPos = state.CommandPos + distance;
        state.Moving = true;
        state.Jogging = false;
        state.Homing = false;
        state.Velocity = velocity;
        state.TargetVelocity = velocity;
        state.Status = 1;
    }

    public void Halt(int axis)
    {
        if (!_axes.TryGetValue(axis, out var state)) return;
        state.Moving = false;
        state.Jogging = false;
        state.Homing = false;
        state.Velocity = 0;
        state.TargetVelocity = 0;
        state.TargetPos = state.CommandPos;
        state.Status = 0;
    }

    public void EmergencyStop()
    {
        foreach (var kvp in _axes)
        {
            kvp.Value.Moving = false;
            kvp.Value.Jogging = false;
            kvp.Value.Homing = false;
            kvp.Value.Velocity = 0;
            kvp.Value.TargetVelocity = 0;
            kvp.Value.Status = 4;
        }
    }

    public void Home(int axis, int homeMode = 3)
    {
        if (!_axes.TryGetValue(axis, out var state) || !state.ServoOn)
        {
            _lastError = $"轴 {axis} 未使能";
            return;
        }
        state.Homing = true;
        state.Moving = false;
        state.Jogging = false;
        state.Velocity = 2000;
        state.TargetVelocity = 2000;
        state.Status = 3;
    }

    public void Jog(int axis, double velocity)
    {
        if (!_axes.TryGetValue(axis, out var state) || !state.ServoOn) return;
        state.Jogging = true;
        state.Moving = false;
        state.Homing = false;
        state.TargetVelocity = velocity;
        state.Velocity = velocity;
        state.Status = 1;
    }

    public void JogStop(int axis)
    {
        if (!_axes.TryGetValue(axis, out var state)) return;
        state.Jogging = false;
        state.Velocity = 0;
        state.TargetVelocity = 0;
        state.Status = 0;
    }

    public void LineInterpolation(int[] axes, double[] distances, double velocity)
    {
        for (int i = 0; i < axes.Length && i < distances.Length; i++)
        {
            if (_axes.TryGetValue(axes[i], out var state) && state.ServoOn)
            {
                state.TargetPos = state.CommandPos + distances[i];
                state.Moving = true;
                state.Velocity = velocity;
                state.TargetVelocity = velocity;
                state.Status = 1;
            }
        }
    }

    public void ArcInterpolation(int plane, double cx, double cy, double angle, double velocity)
    {
        // 模拟：在 XY 平面画圆弧，简化实现
        if (_axes.TryGetValue(1, out var x) && x.ServoOn)
        {
            x.TargetPos = x.CommandPos + cx + 100;
            x.Moving = true;
            x.Velocity = velocity;
            x.Status = 1;
        }
        if (_axes.TryGetValue(2, out var y) && y.ServoOn)
        {
            y.TargetPos = y.CommandPos + cy + 100;
            y.Moving = true;
            y.Velocity = velocity;
            y.Status = 1;
        }
    }

    public bool IsMoving(int axis) =>
        _axes.TryGetValue(axis, out var state) && (state.Moving || state.Jogging || state.Homing);

    public double GetCommandPosition(int axis) =>
        _axes.TryGetValue(axis, out var state) ? state.CommandPos : 0;

    public double GetEncoderPosition(int axis) =>
        _axes.TryGetValue(axis, out var state) ? state.EncoderPos : 0;

    public int GetAxisStatus(int axis) =>
        _axes.TryGetValue(axis, out var state) ? state.Status : -1;

    public bool ReadDI(int index) =>
        _digitalInputs.TryGetValue(index, out var val) && val;

    public void WriteDO(int index, bool value)
    {
        _digitalOutputs[index] = value;
    }

    public string GetLastError() => _lastError;

    public void ClearAlarm(int axis)
    {
        if (_axes.TryGetValue(axis, out var state))
        {
            state.Alarms = 0;
            if (state.Status == 2) state.Status = 0;
        }
    }

    private void OnSimTick(object? sender, ElapsedEventArgs e)
    {
        foreach (var kvp in _axes)
        {
            var state = kvp.Value;
            if (!state.ServoOn || state.Status == 4) continue;

            // 模拟回零
            if (state.Homing)
            {
                state.CommandPos += state.Velocity * 0.05;
                state.EncoderPos = state.CommandPos;
                if (state.CommandPos >= 5000) // 模拟回零完成
                {
                    state.Homing = false;
                    state.CommandPos = 0;
                    state.EncoderPos = 0;
                    state.Velocity = 0;
                    state.TargetVelocity = 0;
                    state.Status = 0;
                    _digitalInputs[5] = true; // 原点信号
                }
                continue;
            }

            // 模拟点位运动 / JOG
            if (state.Moving && state.ServoOn)
            {
                double step = state.Velocity * 0.05;
                double remaining = state.TargetPos - state.CommandPos;

                if (Math.Abs(remaining) <= Math.Abs(step))
                {
                    state.CommandPos = state.TargetPos;
                    state.EncoderPos = state.TargetPos;
                    state.Moving = false;
                    state.Velocity = 0;
                    state.TargetVelocity = 0;
                    state.Status = 0;

                    // 随机模拟到位报警
                    if (_rng.NextDouble() < 0.01)
                    {
                        state.Alarms |= 1;
                        state.Status = 2;
                        _lastError = $"轴 {kvp.Key} 跟随误差超限";
                    }
                }
                else
                {
                    state.CommandPos += Math.Sign(remaining) * Math.Abs(step);
                    state.EncoderPos = state.CommandPos + (_rng.NextDouble() - 0.5) * 2;
                }
            }

            // 模拟 JOG
            if (state.Jogging && state.ServoOn)
            {
                state.CommandPos += state.Velocity * 0.05;
                state.EncoderPos = state.CommandPos;
            }

            // 随机模拟 DI 变化（限位、急停等）
            if (_rng.NextDouble() < 0.001)
            {
                int di = _rng.Next(1, 9);
                _digitalInputs[di] = !_digitalInputs.GetValueOrDefault(di);
            }
        }
    }

    public void Dispose()
    {
        _simTimer?.Stop();
        _simTimer?.Dispose();
    }
}
