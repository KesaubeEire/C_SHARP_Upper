using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

/// <summary>
/// Defines the condition type for an alarm rule.
/// </summary>
public enum AlarmCondition
{
    Above,
    Below,
    Equal,
    NotEqual,
}

/// <summary>
/// A rule that defines when an alarm should be triggered.
/// Includes deadband and delay settings to prevent alarm chattering (ISA 18.2 §6).
/// </summary>
public class AlarmRule
{
    /// <summary>变量键 (如 "I0", "Q5", "M10", "DB1:6")。</summary>
    public string VariableKey { get; set; } = string.Empty;

    /// <summary>PLC 数据类型 (用于 DB 变量，如 REAL/DINT/INT/WORD/BYTE)。</summary>
    public string DataType { get; set; } = "BYTE";

    /// <summary>可读描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>严重度。</summary>
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;

    /// <summary>报警条件类型。</summary>
    public AlarmConditionType ConditionType { get; set; } = AlarmConditionType.High;

    /// <summary>阈值。</summary>
    public double Threshold { get; set; }

    /// <summary>比较条件 (兼容旧版)。</summary>
    public AlarmCondition Condition { get; set; } = AlarmCondition.Above;

    /// <summary>死区 (hysteresis) — 值必须超过阈值±死区才触发/恢复，防止抖动。</summary>
    public double Deadband { get; set; }

    /// <summary>触发延时 (毫秒) — 条件持续超过此时间才产生报警。</summary>
    public int OnDelayMs { get; set; }

    /// <summary>恢复延时 (毫秒) — 正常状态持续超过此时间才清除报警。</summary>
    public int OffDelayMs { get; set; }

    /// <summary>所属区域/设备组。</summary>
    public string Area { get; set; } = string.Empty;

    /// <summary>此规则是否启用。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>上次检查时的触发状态 (内部状态)。</summary>
    internal bool LastTriggered { get; set; }

    /// <summary>条件开始时间 (用于 OnDelay 计时)。</summary>
    internal DateTime? ConditionStartTime { get; set; }

    /// <summary>正常开始时间 (用于 OffDelay 计时)。</summary>
    internal DateTime? NormalStartTime { get; set; }

    /// <summary>检查值是否满足此规则的触发条件。</summary>
    public bool Check(double value)
    {
        if (!IsEnabled) return false;
        return ConditionType switch
        {
            AlarmConditionType.High => value > Threshold,
            AlarmConditionType.HighHigh => value > Threshold,
            AlarmConditionType.Low => value < Threshold,
            AlarmConditionType.LowLow => value < Threshold,
            AlarmConditionType.NotEqual => Math.Abs(value - Threshold) > 0.001,
            AlarmConditionType.RateOfChange => Math.Abs(value) > Threshold,
            AlarmConditionType.Digital => Math.Abs(value - Threshold) < 0.001,
            _ => Condition switch
            {
                AlarmCondition.Above => value > Threshold,
                AlarmCondition.Below => value < Threshold,
                AlarmCondition.Equal => Math.Abs(value - Threshold) < 0.001,
                AlarmCondition.NotEqual => Math.Abs(value - Threshold) > 0.001,
                _ => false,
            },
        };
    }

    /// <summary>考虑死区后，检查是否仍然处于触发状态 (恢复需要越过死区)。</summary>
    public bool CheckWithDeadband(double value)
    {
        if (!IsEnabled || Deadband <= 0) return Check(value);

        // 如果当前是触发状态，需要值回到阈值以内再减去死区才视为恢复
        if (LastTriggered)
        {
            return ConditionType switch
            {
                AlarmConditionType.High or AlarmConditionType.HighHigh => value > Threshold - Deadband,
                AlarmConditionType.Low or AlarmConditionType.LowLow => value < Threshold + Deadband,
                _ => Check(value),
            };
        }

        return Check(value);
    }
}

/// <summary>
/// Alarm statistics snapshot.
/// </summary>
public class AlarmStatistics
{
    public int TotalActive { get; set; }
    public int TotalUnacknowledged { get; set; }
    public int TotalShelved { get; set; }
    public int TotalToday { get; set; }
    public int TotalThisHour { get; set; }
    public int TotalEmergency { get; set; }
    public int TotalCritical { get; set; }
}

/// <summary>
/// Alarm management service. Monitors PLC data changes via <see cref="PollingScheduler.DataUpdated"/>
/// and generates alarm events based on configurable rules.
/// Supports ISA 18.2 lifecycle: generate → acknowledge → shelve → clear → archive.
/// </summary>
public class AlarmService
{
    private readonly PollingScheduler _scheduler;
    private readonly List<AlarmRule> _rules = [];
    private int _totalAlarmCount;
    private int _activeAlarmCount;
    private int _todayCount;
    private int _thisHourCount;
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromSeconds(2);
    private const int MaxPersistHistory = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = false,
    };

    /// <summary>持久化存储路径。</summary>
    private static string StoragePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KesaPlc",
            "alarms.json");

    private static string RulesPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KesaPlc",
            "rules.json");

    private static string DefaultRulesPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "default-rules.json");

    public AlarmService(PollingScheduler scheduler)
    {
        _scheduler = scheduler;
        LoadFromFile();
        LoadRules();
        // 从 JSON 加载的 DB 规则 → 同步创建对应的 DbPollItem
        SyncDbPollItemsForRules();
    }

    /// <summary>
    /// 为所有 DB{N}:{O} 格式的规则自动创建 DbPollItem，确保轮询能读到这些变量。
    /// UI 添加规则时通过 SyncDbPollItem 同步，此处处理从 default-rules.json 或文件加载的规则。
    /// </summary>
    private void SyncDbPollItemsForRules()
    {
        var items = _scheduler.Config.DbItems;
        foreach (var rule in _rules)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                rule.VariableKey, @"^DB(\d+):(\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            int dbNumber = int.Parse(match.Groups[1].ValueSpan);
            int offset = int.Parse(match.Groups[2].ValueSpan);

            // 已存在则跳过
            if (items.Any(i => i.DbNumber == dbNumber && i.Offset == offset))
                continue;

            items.Add(new DbPollItem
            {
                DbNumber = dbNumber,
                Offset = offset,
                DataType = rule.DataType,
                Label = rule.Description,
                Enabled = rule.IsEnabled,
            });
        }
    }

    /// <summary>所有历史报警 (最新在前)。</summary>
    public ObservableCollection<AlarmItem> Alarms { get; } = [];

    /// <summary>当前活动的报警。</summary>
    public ObservableCollection<AlarmItem> ActiveAlarms { get; } = [];

    /// <summary>已搁置的报警。</summary>
    public ObservableCollection<AlarmItem> ShelvedAlarms { get; } = [];

    public int TotalAlarmCount
    {
        get => _totalAlarmCount;
        private set
        {
            _totalAlarmCount = value;
            TotalAlarmCountChanged?.Invoke(value);
        }
    }

    public int ActiveAlarmCount
    {
        get => _activeAlarmCount;
        private set
        {
            _activeAlarmCount = value;
            ActiveAlarmCountChanged?.Invoke(value);
        }
    }

    public int TodayCount
    {
        get => _todayCount;
        private set
        {
            _todayCount = value;
            TodayCountChanged?.Invoke(value);
        }
    }

    public int ThisHourCount
    {
        get => _thisHourCount;
        private set
        {
            _thisHourCount = value;
            ThisHourCountChanged?.Invoke(value);
        }
    }

    /// <summary>当前所有报警规则。代码 <c>AddRule</c> 和 UI 添加共用此集合。</summary>
    public ObservableCollection<AlarmRule> Rules { get; } = [];

    /// <summary>最大内存历史报警数。</summary>
    public int MaxHistory { get; set; } = 1000;

    public event Action<int>? TotalAlarmCountChanged;
    public event Action<int>? ActiveAlarmCountChanged;
    public event Action<int>? TodayCountChanged;
    public event Action<int>? ThisHourCountChanged;
    public event Action<AlarmItem>? AlarmRaised;
    public event Action<AlarmItem>? AlarmCleared;
    public event Action<AlarmItem>? AlarmAcknowledged;
    public event Action<AlarmItem>? AlarmShelved;
    public event Action<AlarmItem>? AlarmUnshelved;

    // ========== 生命周期 ==========

    public void Subscribe()
    {
        // 确保所有 DB 规则有对应的 DbPollItem（默认规则加载时已同步，
        // 但防御性再同步一次，覆盖运行时修改的场景）
        SyncDbPollItemsForRules();
        _scheduler.DataUpdated += OnDataUpdated;
    }

    public void Unsubscribe()
    {
        _scheduler.DataUpdated -= OnDataUpdated;
    }

    // ========== 规则管理 ==========

    /// <summary>添加规则（同时同步到 UI 集合）。</summary>
    public void AddRule(AlarmRule rule)
    {
        _rules.Add(rule);
        if (!Rules.Contains(rule))
            Rules.Add(rule);
        SaveRules();
    }

    /// <summary>移除规则。</summary>
    public bool RemoveRule(AlarmRule rule)
    {
        _rules.Remove(rule);
        Rules.Remove(rule);
        SaveRules();
        return true;
    }

    /// <summary>替换规则（编辑时先删旧再加新）。</summary>
    public void UpdateRule(AlarmRule oldRule, AlarmRule newRule)
    {
        var idx = _rules.IndexOf(oldRule);
        if (idx >= 0)
            _rules[idx] = newRule;

        var uiIdx = Rules.IndexOf(oldRule);
        if (uiIdx >= 0)
            Rules[uiIdx] = newRule;

        SaveRules();
    }

    public IReadOnlyList<AlarmRule> GetRules() => _rules.AsReadOnly();

    // ========== 确认 ==========

    public void AcknowledgeAlarm(AlarmItem alarm, string? acknowledgedBy = null)
    {
        if (alarm.IsAcknowledged) return;
        alarm.IsAcknowledged = true;
        alarm.AcknowledgedBy = acknowledgedBy;
        alarm.AcknowledgedAt = DateTime.Now;
        AlarmAcknowledged?.Invoke(alarm);
        DebouncedSave();
    }

    public void AcknowledgeAll(string? acknowledgedBy = null)
    {
        foreach (var alarm in ActiveAlarms.ToList())
        {
            if (!alarm.IsAcknowledged)
            {
                alarm.IsAcknowledged = true;
                alarm.AcknowledgedBy = acknowledgedBy;
                alarm.AcknowledgedAt = DateTime.Now;
                AlarmAcknowledged?.Invoke(alarm);
            }
        }
        DebouncedSave();
    }

    // ========== 搁置 (Shelving) ==========

    /// <summary>
    /// 搁置报警。搁置期间报警从活动列表隐藏，到期自动恢复。
    /// </summary>
    /// <param name="alarm">要搁置的报警。</param>
    /// <param name="duration">搁置时长 (null 表示永久搁置)。</param>
    /// <param name="shelvedBy">操作人。</param>
    public void ShelveAlarm(AlarmItem alarm, TimeSpan? duration = null, string? shelvedBy = null)
    {
        if (alarm.IsShelved) return;
        alarm.IsShelved = true;
        alarm.ShelvedBy = shelvedBy;
        alarm.ShelvedUntil = duration.HasValue ? DateTime.Now.Add(duration.Value) : null;

        if (alarm.IsActive && !ShelvedAlarms.Contains(alarm))
            ShelvedAlarms.Insert(0, alarm);

        if (alarm.IsActive && ActiveAlarms.Contains(alarm))
            ActiveAlarms.Remove(alarm);

        AlarmShelved?.Invoke(alarm);
        ActiveAlarmCount = ActiveAlarms.Count;
        DebouncedSave();
    }

    /// <summary>
    /// 取消搁置。
    /// </summary>
    public void UnshelveAlarm(AlarmItem alarm)
    {
        if (!alarm.IsShelved) return;
        alarm.IsShelved = false;
        alarm.ShelvedUntil = null;

        ShelvedAlarms.Remove(alarm);

        if (alarm.IsActive && !ActiveAlarms.Contains(alarm))
            ActiveAlarms.Insert(0, alarm);

        AlarmUnshelved?.Invoke(alarm);
        ActiveAlarmCount = ActiveAlarms.Count;
        DebouncedSave();
    }

    /// <summary>
    /// 检查并自动恢复过期搁置。
    /// </summary>
    public void CheckShelvedAlarms()
    {
        var now = DateTime.Now;
        foreach (var alarm in ShelvedAlarms.ToList())
        {
            if (alarm.ShelvedUntil.HasValue && alarm.ShelvedUntil.Value <= now)
            {
                UnshelveAlarm(alarm);
            }
        }
    }

    // ========== 备注 ==========

    public void AddComment(AlarmItem alarm, string? comment)
    {
        alarm.Comment = comment;
        DebouncedSave();
    }

    // ========== 清除 ==========

    public void ClearAll()
    {
        Alarms.Clear();
        ActiveAlarms.Clear();
        ShelvedAlarms.Clear();
        TotalAlarmCount = 0;
        ActiveAlarmCount = 0;
        TodayCount = 0;
        ThisHourCount = 0;
        DebouncedSave();
    }

    // ========== 统计 ==========

    public AlarmStatistics GetStatistics()
    {
        var now = DateTime.Now;
        return new AlarmStatistics
        {
            TotalActive = ActiveAlarms.Count,
            TotalUnacknowledged = Alarms.Count(a => a.IsActive && !a.IsAcknowledged),
            TotalShelved = ShelvedAlarms.Count,
            TotalToday = Alarms.Count(a => a.Timestamp.Date == now.Date),
            TotalThisHour = Alarms.Count(a =>
                a.Timestamp.Date == now.Date && a.Timestamp.Hour == now.Hour),
            TotalEmergency = Alarms.Count(a => a.Severity == AlarmSeverity.Emergency && a.IsActive),
            TotalCritical = Alarms.Count(a => a.Severity == AlarmSeverity.Critical && a.IsActive),
        };
    }

    // ========== 持久化 ==========

    /// <summary>
    /// 保存报警历史到 JSON 文件。
    /// </summary>
    public void SaveToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // 只持久化最近 MaxPersistHistory 条
            var data = Alarms.Take(MaxPersistHistory).ToList();
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(StoragePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlarmService] Save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 JSON 文件加载报警历史。
    /// </summary>
    public void LoadFromFile()
    {
        try
        {
            if (!File.Exists(StoragePath)) return;
            var json = File.ReadAllText(StoragePath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<List<AlarmItem>>(json, JsonOptions);
            if (data == null) return;

            Alarms.Clear();
            ActiveAlarms.Clear();
            ShelvedAlarms.Clear();

            foreach (var alarm in data.OrderByDescending(a => a.Timestamp))
            {
                Alarms.Add(alarm);
                if (alarm.IsActive && !alarm.IsShelved)
                    ActiveAlarms.Add(alarm);
                if (alarm.IsShelved)
                    ShelvedAlarms.Add(alarm);
            }

            RecalculateCounts();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlarmService] Load failed: {ex.Message}");
        }
    }

    // ========== 规则持久化 ==========

    /// <summary>保存规则到 JSON 文件。</summary>
    public void SaveRules()
    {
        try
        {
            var dir = Path.GetDirectoryName(RulesPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_rules, JsonOptions);
            File.WriteAllText(RulesPath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlarmService] SaveRules failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 JSON 文件加载规则。
    /// 优先加载 %APPDATA% 中用户编辑过的规则（非空时）；
    /// 若为空文件或不存在，则从应用自带的 default-rules.json 复制一份作为初始规则。
    /// </summary>
    public void LoadRules()
    {
        try
        {
            // 尝试从 %APPDATA% 读取用户规则
            if (File.Exists(RulesPath))
            {
                var json = File.ReadAllText(RulesPath, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<List<AlarmRule>>(json, JsonOptions);
                if (data != null && data.Count > 0)
                {
                    _rules.Clear();
                    Rules.Clear();
                    foreach (var rule in data)
                    {
                        _rules.Add(rule);
                        Rules.Add(rule);
                    }
                    return; // 用户有规则 → 加载完成
                }
                // 文件存在但为空 → 继续往下走，从默认复制
            }

            // %APPDATA% 无有效规则 → 从应用自带默认规则复制
            if (File.Exists(DefaultRulesPath))
            {
                var dir = Path.GetDirectoryName(RulesPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(DefaultRulesPath, RulesPath, overwrite: true);

                var json = File.ReadAllText(RulesPath, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<List<AlarmRule>>(json, JsonOptions);
                if (data != null)
                {
                    _rules.Clear();
                    Rules.Clear();
                    foreach (var rule in data)
                    {
                        _rules.Add(rule);
                        Rules.Add(rule);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlarmService] LoadRules failed: {ex.Message}");
        }
    }

    // ========== CSV 导出 ==========

    /// <summary>
    /// 导出报警列表到 CSV 文件。
    /// </summary>
    public void ExportToCsv(string filePath, IEnumerable<AlarmItem>? items = null)
    {
        var source = items ?? Alarms;
        var sb = new StringBuilder();
        sb.AppendLine("时间,严重度,类型,变量,描述,区域,值,阈值,死区,状态,确认人,确认时间,备注,搁置人,搁置到期");

        foreach (var alarm in source)
        {
            sb.AppendLine(
                $"\"{alarm.DateTimeText}\"," +
                $"\"{alarm.Severity}\"," +
                $"\"{alarm.AlarmType}\"," +
                $"\"{alarm.VariableName}\"," +
                $"\"{EscapeCsv(alarm.Description)}\"," +
                $"\"{alarm.Area}\"," +
                $"{alarm.CurrentValue:F2}," +
                $"{alarm.Threshold}," +
                $"{alarm.Deadband}," +
                $"\"{alarm.StatusText}\"," +
                $"\"{alarm.AcknowledgedBy}\"," +
                $"\"{alarm.AcknowledgedAt?.ToString("yyyy-MM-dd HH:mm:ss")}\"," +
                $"\"{EscapeCsv(alarm.Comment)}\"," +
                $"\"{alarm.ShelvedBy}\"," +
                $"\"{alarm.ShelvedUntil?.ToString("yyyy-MM-dd HH:mm:ss")}\"");
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\"", "\"\"");
    }

    // ========== 内部 ==========

    private void OnDataUpdated(HashSet<string> updatedKeys)
    {
        // 检查搁置到期
        CheckShelvedAlarms();

        foreach (var rule in _rules)
        {
            if (!updatedKeys.Contains(rule.VariableKey))
                continue;

            double? rawValue = _scheduler.GetDoubleValue(rule.VariableKey);
            if (rawValue == null)
                continue;

            double value = rawValue.Value;

            // 用死区检查
            bool isActive = rule.CheckWithDeadband(value);

            if (isActive && !rule.LastTriggered)
            {
                // 条件首次满足 — 检查 OnDelay
                if (rule.OnDelayMs > 0)
                {
                    rule.ConditionStartTime ??= DateTime.UtcNow;
                    if ((DateTime.UtcNow - rule.ConditionStartTime.Value).TotalMilliseconds < rule.OnDelayMs)
                        continue; // 延时未到，等待下一个周期
                }

                // 新报警
                rule.LastTriggered = true;
                rule.ConditionStartTime = null;
                rule.NormalStartTime = null;

                var alarm = new AlarmItem
                {
                    Severity = rule.Severity,
                    AlarmType = rule.ConditionType,
                    VariableName = rule.VariableKey,
                    Description = rule.Description,
                    Area = rule.Area,
                    CurrentValue = value,
                    Threshold = rule.Threshold,
                    Deadband = rule.Deadband,
                    IsActive = true,
                };
                AddAlarm(alarm);
                AlarmRaised?.Invoke(alarm);
                DebouncedSave();
            }
            else if (!isActive && rule.LastTriggered)
            {
                // 条件恢复 — 检查 OffDelay
                if (rule.OffDelayMs > 0)
                {
                    rule.NormalStartTime ??= DateTime.UtcNow;
                    if ((DateTime.UtcNow - rule.NormalStartTime.Value).TotalMilliseconds < rule.OffDelayMs)
                        continue; // 延时未到
                }

                // 报警恢复
                rule.LastTriggered = false;
                rule.ConditionStartTime = null;
                rule.NormalStartTime = null;

                foreach (var active in ActiveAlarms
                             .Where(a => a.VariableName == rule.VariableKey && a.IsActive)
                             .ToList())
                {
                    active.IsActive = false;
                    ActiveAlarms.Remove(active);

                    // 如果已搁置，也从搁置列表移除
                    if (active.IsShelved)
                        ShelvedAlarms.Remove(active);

                    AlarmCleared?.Invoke(active);
                }

                RecalculateCounts();
                DebouncedSave();
            }
        }
    }

    private void AddAlarm(AlarmItem alarm)
    {
        Alarms.Insert(0, alarm);
        if (alarm.IsActive)
            ActiveAlarms.Insert(0, alarm);

        RecalculateCounts();

        // 裁剪内存历史
        while (Alarms.Count > MaxHistory)
            Alarms.RemoveAt(Alarms.Count - 1);
    }

    private void RecalculateCounts()
    {
        TotalAlarmCount = Alarms.Count;
        ActiveAlarmCount = ActiveAlarms.Count;
        TodayCount = Alarms.Count(a => a.Timestamp.Date == DateTime.Now.Date);
        ThisHourCount = Alarms.Count(a =>
            a.Timestamp.Date == DateTime.Now.Date &&
            a.Timestamp.Hour == DateTime.Now.Hour);
    }

    private DateTime _lastDebouncedSave = DateTime.MinValue;
    private readonly object _saveLock = new();

    /// <summary>
    /// 防抖保存：短时间内多次调用只触发一次实际写入。
    /// </summary>
    private void DebouncedSave()
    {
        var now = DateTime.Now;
        lock (_saveLock)
        {
            if (now - _lastDebouncedSave < SaveDebounce)
                return;
            _lastDebouncedSave = now;
        }
        SaveToFile();
    }
}
