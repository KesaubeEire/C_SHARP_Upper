namespace WpfScada.Services.Motion;

/// <summary>
/// 运动控制卡抽象接口。
/// 支持多品牌兼容：雷赛 DMC / 固高 GTS / 正运动 ZMotion / Mock 模拟。
/// </summary>
public interface IMotionController
{
    // ======= 连接管理 =======
    bool IsConnected { get; }
    bool Connect(string address);
    void Disconnect();

    // ======= 轴使能 =======
    void ServoOn(int axis);
    void ServoOff(int axis);
    bool IsServoOn(int axis);

    // ======= 点位运动 =======
    void MoveAbs(int axis, double position, double velocity);
    void MoveRel(int axis, double distance, double velocity);
    void Halt(int axis);
    void EmergencyStop();

    // ======= 回零 =======
    void Home(int axis, int homeMode = 3);

    // ======= JOG =======
    void Jog(int axis, double velocity);
    void JogStop(int axis);

    // ======= 多轴插补 =======
    void LineInterpolation(int[] axes, double[] distances, double velocity);
    void ArcInterpolation(int plane, double cx, double cy, double angle, double velocity);

    // ======= 状态查询 =======
    bool IsMoving(int axis);
    double GetCommandPosition(int axis);
    double GetEncoderPosition(int axis);
    int GetAxisStatus(int axis); // 0=空闲, 1=运行中, 2=报警, 3=回零中

    // ======= IO =======
    bool ReadDI(int index);
    void WriteDO(int index, bool value);

    // ======= 报警 =======
    string GetLastError();
    void ClearAlarm(int axis);
}
