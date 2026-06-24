using CommunityToolkit.Mvvm.ComponentModel;

namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>
/// Severity level of an alarm (ISA 18.2 / EEMUA 191 priority classification).
/// </summary>
public enum AlarmSeverity
{
    /// <summary>提示信息，无需操作员响应。</summary>
    Info,

    /// <summary>警告，操作员需要知情。</summary>
    Warning,

    /// <summary>严重，需在数分钟内响应。</summary>
    Critical,

    /// <summary>紧急，需在一分钟内响应。</summary>
    Emergency,
}

/// <summary>
/// Type/condition of the alarm limit (HI / HIHI / LO / LOLO / etc.).
/// </summary>
public enum AlarmConditionType
{
    /// <summary>高限 (value > threshold)</summary>
    High,

    /// <summary>高高限 (value >> threshold, more severe)</summary>
    HighHigh,

    /// <summary>低限 (value &lt; threshold)</summary>
    Low,

    /// <summary>低低限 (value &lt;&lt; threshold)</summary>
    LowLow,

    /// <summary>不等于指定值 (数字量故障)</summary>
    NotEqual,

    /// <summary>变化率超限 (|dv/dt| > threshold)</summary>
    RateOfChange,

    /// <summary>数字量状态 (布尔位 = 触发值)</summary>
    Digital,
}

/// <summary>
/// Represents a single alarm event in the industrial alarm system.
/// </summary>
public partial class AlarmItem : ObservableObject
{
    // ========== 不可变属性 (Init-only) ==========

    /// <summary>报警产生时间戳。</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>严重度级别。</summary>
    public AlarmSeverity Severity { get; init; }

    /// <summary>报警条件类型 (HI / HIHI / LO / LOLO / etc.)。</summary>
    public AlarmConditionType AlarmType { get; init; } = AlarmConditionType.High;

    /// <summary>触发报警的 PLC 变量名 (如 I0、Q5、M10、DB100[42])。</summary>
    public string VariableName { get; init; } = string.Empty;

    /// <summary>报警描述文本。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>所属区域/工段/设备组。</summary>
    public string Area { get; init; } = string.Empty;

    /// <summary>触发时的变量值。</summary>
    public double CurrentValue { get; init; }

    /// <summary>报警阈值。</summary>
    public double? Threshold { get; init; }

    /// <summary>死区宽度，用于防止报警抖动 (hysteresis)。</summary>
    public double Deadband { get; init; }

    // ========== 可变属性 (ObservableProperty) ==========

    /// <summary>报警是否处于活动状态。</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>操作员是否已确认此报警。</summary>
    [ObservableProperty]
    private bool _isAcknowledged;

    /// <summary>报警是否被搁置 (暂时隐藏，到期恢复)。</summary>
    [ObservableProperty]
    private bool _isShelved;

    /// <summary>确认人。</summary>
    [ObservableProperty]
    private string? _acknowledgedBy;

    /// <summary>搁置人。</summary>
    [ObservableProperty]
    private string? _shelvedBy;

    /// <summary>确认时间。</summary>
    [ObservableProperty]
    private DateTime? _acknowledgedAt;

    /// <summary>搁置到期时间 (null 表示永久搁置)。</summary>
    [ObservableProperty]
    private DateTime? _shelvedUntil;

    /// <summary>操作员备注。</summary>
    [ObservableProperty]
    private string? _comment;

    // ========== 计算属性 ==========

    /// <summary>短时间格式 (HH:mm:ss)。</summary>
    public string TimeText => Timestamp.ToString("HH:mm:ss");

    /// <summary>完整日期时间格式。</summary>
    public string DateTimeText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>报警状态文本 (多语言友好的组合状态)。</summary>
    public string StatusText => (IsActive, IsAcknowledged, IsShelved) switch
    {
        (true, false, false) => "未确认",
        (true, true, false) => "已确认",
        (false, _, false) => "已恢复",
        (_, _, true) => "已搁置",
    };

    /// <summary>报警持续时长 (活动报警从产生至今的时间)。</summary>
    public TimeSpan? Duration => IsActive ? DateTime.UtcNow - Timestamp.ToUniversalTime() : null;

    /// <summary>指示此报警是否需要视觉闪烁 (Emergency/Critical 未确认)。</summary>
    public bool NeedsFlash => IsActive && !IsAcknowledged && Severity is AlarmSeverity.Critical or AlarmSeverity.Emergency;

    /// <summary>死的区间下限 (用于死区判断)。</summary>
    public double DeadbandLow => Threshold.HasValue ? Threshold.Value - Deadband : 0;

    /// <summary>死的区间上限 (用于死区判断)。</summary>
    public double DeadbandHigh => Threshold.HasValue ? Threshold.Value + Deadband : 0;
}
