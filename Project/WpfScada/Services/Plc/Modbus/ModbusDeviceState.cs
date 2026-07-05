namespace WpfScada.Services.Plc.Modbus;

public class ModbusDeviceState
{
    public byte Address { get; }
    public bool IsOnline { get; private set; } = true;
    public int ConsecutiveFailures { get; private set; }
    public DateTime? LastSuccessTime { get; private set; }
    public DateTime? LastAttemptTime { get; private set; }

    private const int MaxBackoffSeconds = 30;

    public ModbusDeviceState(byte address) { Address = address; }

    public bool ShouldSkip()
    {
        if (ConsecutiveFailures < 3) return false;
        return ElapsedSinceLastSuccess.TotalSeconds < GetBackoffSeconds();
    }

    public double SecondsUntilRetry()
    {
        if (ConsecutiveFailures < 3) return 0;
        return Math.Max(0, GetBackoffSeconds() - ElapsedSinceLastSuccess.TotalSeconds);
    }

#pragma warning disable S6561
    private TimeSpan ElapsedSinceLastSuccess =>
        DateTime.Now - (LastSuccessTime ?? DateTime.MinValue);
#pragma warning restore S6561

    public void RecordSuccess()
    {
        IsOnline = true;
        ConsecutiveFailures = 0;
        LastSuccessTime = DateTime.Now;
    }

    public void RecordFailure()
    {
        ConsecutiveFailures++;
        LastAttemptTime = DateTime.Now;
        if (ConsecutiveFailures >= 3) IsOnline = false;
    }

    private double GetBackoffSeconds()
    {
        if (ConsecutiveFailures < 3) return 0;
        int shift = ConsecutiveFailures - 1;
        if (shift >= 31) return MaxBackoffSeconds;
        return Math.Min(1L << shift, MaxBackoffSeconds);
    }
}
