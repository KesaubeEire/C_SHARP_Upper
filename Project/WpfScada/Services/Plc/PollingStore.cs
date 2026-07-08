using CommunityToolkit.Mvvm.ComponentModel;
using WpfScada.Controls.Plc;

namespace WpfScada.Services.Plc;

public partial class PollingStore : ObservableObject
{
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private long _latencyMs;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private LedQuality _quality = LedQuality.Disabled;

    // ── 诊断统计（供 UI / 日志使用） ──

    [ObservableProperty]
    private DateTime? _lastStartedAt;

    [ObservableProperty]
    private DateTime? _lastCompletedAt;

    [ObservableProperty]
    private long _lastDurationMs;

    [ObservableProperty]
    private long _totalTicks;

    [ObservableProperty]
    private long _longCycleCount;

    [ObservableProperty]
    private int _consecutiveFailures;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private DateTime? _lastSuccessAt;

    [ObservableProperty]
    private long _missedTicks;

    public void ResetDiagnostics()
    {
        LastStartedAt = null;
        LastCompletedAt = null;
        LastDurationMs = 0;
        TotalTicks = 0;
        LongCycleCount = 0;
        ConsecutiveFailures = 0;
        LastError = null;
        LastSuccessAt = null;
        MissedTicks = 0;
    }
}
