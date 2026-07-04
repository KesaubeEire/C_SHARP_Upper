namespace TEST_101;

/// <summary>
/// 设备状态管理 — 追踪单台 Modbus 从站的在线状态、失败计数和退避策略。
///
/// 退避策略（指数退避）：
///   第 1 次失败 → 等 1 秒
///   第 2 次失败 → 等 2 秒
///   第 3 次失败 → 等 4 秒
///   第 4 次失败 → 等 8 秒
///   第 N 次失败 → 等 min(2^(N-1), 30) 秒
///   一旦成功 → 立即重置计数器
/// </summary>
public class DeviceState
{
    public byte Address { get; }
    public bool IsOnline { get; private set; } = true;
    public int ConsecutiveFailures { get; private set; }
    public DateTime? LastSuccessTime { get; private set; }
    public DateTime? LastAttemptTime { get; private set; }

    /// <summary>最大退避间隔（秒）</summary>
    private const int MaxBackoffSeconds = 30;

    public DeviceState(byte address)
    {
        Address = address;
    }

    /// <summary>
    /// 当前是否应该跳过此设备的轮询。
    /// 设备离线时退避期间返回 true。
    /// </summary>
    public bool ShouldSkip()
    {
        if (ConsecutiveFailures < 3)
            return false;

        double secondsSinceLastSuccess = (DateTime.Now - (LastSuccessTime ?? DateTime.MinValue)).TotalSeconds;
        return secondsSinceLastSuccess < GetBackoffSeconds();
    }

    /// <summary>距离下次可重试还有多少秒（用于 UI 显示）</summary>
    public double SecondsUntilRetry()
    {
        if (ConsecutiveFailures < 3)
            return 0;

        double elapsed = (DateTime.Now - (LastSuccessTime ?? DateTime.MinValue)).TotalSeconds;
        double backoff = GetBackoffSeconds();
        return Math.Max(0, backoff - elapsed);
    }

    /// <summary>记录一次成功响应</summary>
    public void RecordSuccess()
    {
        IsOnline = true;
        ConsecutiveFailures = 0;
        LastSuccessTime = DateTime.Now;
    }

    /// <summary>记录一次失败（超时或异常）</summary>
    public void RecordFailure()
    {
        ConsecutiveFailures++;
        LastAttemptTime = DateTime.Now;

        if (ConsecutiveFailures >= 3)
            IsOnline = false;
    }

    /// <summary>指数退避：2^(failures-1) 秒，上限 30 秒</summary>
    private double GetBackoffSeconds()
    {
        if (ConsecutiveFailures < 3)
            return 0;

        // failures=3 → 4s, failures=4 → 8s, ..., 上限 30s
        int shift = ConsecutiveFailures - 1;
        if (shift >= 31) return MaxBackoffSeconds; // 防止 int 溢出
        long seconds = 1L << shift; // 2^(failures-1)
        return Math.Min(seconds, MaxBackoffSeconds);
    }
}
