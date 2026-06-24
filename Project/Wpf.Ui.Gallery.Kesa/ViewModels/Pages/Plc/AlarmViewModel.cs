using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Plc;

/// <summary>
/// Sort direction for alarm columns.
/// </summary>
public enum SortDirection
{
    None,
    Ascending,
    Descending,
}

/// <summary>
/// ViewModel-friendly severity filter option.
/// </summary>
public class SeverityFilterOption
{
    public string DisplayName { get; init; } = string.Empty;
    public AlarmSeverity? Severity { get; init; }
    public override string ToString() => DisplayName;
}

/// <summary>
/// ViewModel for the Alarm management page.
/// Supports ISA 18.2 alarm lifecycle: acknowledge, shelve, filter, sort, export, statistics.
/// </summary>
public partial class AlarmViewModel : ViewModel
{
    private readonly AlarmService _alarmService;
    private readonly PollingScheduler _scheduler;

    // ========== 集合 ==========

    [ObservableProperty]
    private ObservableCollection<AlarmItem> _alarms = [];

    [ObservableProperty]
    private ObservableCollection<AlarmItem> _activeAlarms = [];

    [ObservableProperty]
    private ObservableCollection<AlarmItem> _filteredAlarms = [];

    [ObservableProperty]
    private AlarmItem? _selectedAlarm;

    // ========== 统计数据 ==========

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _unacknowledgedCount;

    [ObservableProperty]
    private int _todayCount;

    [ObservableProperty]
    private int _thisHourCount;

    [ObservableProperty]
    private int _shelvedCount;

    [ObservableProperty]
    private int _emergencyCount;

    [ObservableProperty]
    private int _criticalCount;

    // ========== 订阅状态 ==========

    [ObservableProperty]
    private bool _isSubscribed;

    [ObservableProperty]
    private string _statusText = "报警系统就绪";

    // ========== 过滤条件 ==========

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private AlarmSeverity? _filterSeverity;

    [ObservableProperty]
    private string _filterArea = string.Empty;

    [ObservableProperty]
    private DateTime? _filterDateFrom;

    [ObservableProperty]
    private DateTime? _filterDateTo;

    [ObservableProperty]
    private bool _showShelved;

    // ========== 排序 ==========

    [ObservableProperty]
    private string _sortColumn = "Timestamp";

    [ObservableProperty]
    private SortDirection _sortDirection = SortDirection.Descending;

    [ObservableProperty]
    private string _timeSortGlyph = "▼";

    [ObservableProperty]
    private string _severitySortGlyph = string.Empty;

    [ObservableProperty]
    private string _variableSortGlyph = string.Empty;

    [ObservableProperty]
    private string _statusSortGlyph = string.Empty;

    [ObservableProperty]
    private string _areaSortGlyph = string.Empty;

    // ========== 规则管理 ==========

    [ObservableProperty]
    private bool _isRuleManagerVisible;

    [ObservableProperty]
    private bool _isEditingRule;

    [ObservableProperty]
    private ObservableCollection<AlarmRule> _rules = [];

    // 编辑中的规则副本
    [ObservableProperty]
    private string _editVariableKey = string.Empty;

    [ObservableProperty]
    private string _editDataType = "BYTE";

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private AlarmSeverity _editSeverity = AlarmSeverity.Warning;

    [ObservableProperty]
    private AlarmConditionType _editConditionType = AlarmConditionType.High;

    [ObservableProperty]
    private double _editThreshold;

    [ObservableProperty]
    private double _editDeadband = 2;

    [ObservableProperty]
    private int _editOnDelayMs;

    [ObservableProperty]
    private int _editOffDelayMs;

    [ObservableProperty]
    private string _editArea = string.Empty;

    [ObservableProperty]
    private bool _editIsEnabled = true;

    private AlarmRule? _ruleBeingEdited;

    // 编辑表单下拉选项
    public List<AlarmSeverity> EditSeverityOptions { get; } =
        [AlarmSeverity.Info, AlarmSeverity.Warning, AlarmSeverity.Critical, AlarmSeverity.Emergency];

    public List<AlarmConditionType> EditConditionTypeOptions { get; } =
        [AlarmConditionType.High, AlarmConditionType.HighHigh, AlarmConditionType.Low,
         AlarmConditionType.LowLow, AlarmConditionType.NotEqual, AlarmConditionType.RateOfChange,
         AlarmConditionType.Digital];

    /// <summary>可用 PLC 数据类型选项。</summary>
    public List<string> EditDataTypeOptions { get; } =
        ["BYTE", "WORD", "INT", "DINT", "REAL", "LREAL"];

    // ========== 过滤选项 ==========

    [ObservableProperty]
    private ObservableCollection<SeverityFilterOption> _severityOptions =
    [
        new() { DisplayName = "全部级别", Severity = null },
        new() { DisplayName = "Info", Severity = Models.Plc.AlarmSeverity.Info },
        new() { DisplayName = "Warning", Severity = Models.Plc.AlarmSeverity.Warning },
        new() { DisplayName = "Critical", Severity = Models.Plc.AlarmSeverity.Critical },
        new() { DisplayName = "Emergency", Severity = Models.Plc.AlarmSeverity.Emergency },
    ];

    [ObservableProperty]
    private SeverityFilterOption? _selectedSeverityOption;

    // ========== 区域列表（用于过滤下拉） ==========

    [ObservableProperty]
    private ObservableCollection<string> _areaOptions = [];

    public AlarmViewModel(AlarmService alarmService, PollingScheduler scheduler)
    {
        _alarmService = alarmService;
        _scheduler = scheduler;

        Alarms = _alarmService.Alarms;
        ActiveAlarms = _alarmService.ActiveAlarms;
        FilteredAlarms = Alarms;

        // 订阅服务事件
        _alarmService.AlarmRaised += OnAlarmRaised;
        _alarmService.AlarmCleared += OnAlarmCleared;
        _alarmService.AlarmAcknowledged += OnAlarmChanged;
        _alarmService.AlarmShelved += OnAlarmChanged;
        _alarmService.AlarmUnshelved += OnAlarmChanged;
        _alarmService.TotalAlarmCountChanged += count => TotalCount = count;
        _alarmService.ActiveAlarmCountChanged += count => ActiveCount = count;
        _alarmService.TodayCountChanged += count => TodayCount = count;
        _alarmService.ThisHourCountChanged += count => ThisHourCount = count;

        TotalCount = _alarmService.TotalAlarmCount;
        ActiveCount = _alarmService.ActiveAlarmCount;
        RefreshStatistics();
        SelectedSeverityOption = SeverityOptions.FirstOrDefault();

        // 同步规则集合
        Rules = _alarmService.Rules;

        // 在后台线程更新统计，避免阻塞 UI
        _ = RefreshAreasAsync();
    }

    // ========== 订阅控制 ==========

    [RelayCommand]
    private void Subscribe()
    {
        if (IsSubscribed) return;
        _alarmService.Subscribe();
        IsSubscribed = true;
        StatusText = "报警监控已启动";
    }

    [RelayCommand]
    private void Unsubscribe()
    {
        if (!IsSubscribed) return;
        _alarmService.Unsubscribe();
        IsSubscribed = false;
        StatusText = "报警监控已停止";
    }

    // ========== 规则管理 ==========

    [RelayCommand]
    private void ToggleRuleManager()
    {
        IsRuleManagerVisible = !IsRuleManagerVisible;
        if (!IsRuleManagerVisible)
            IsEditingRule = false;
    }

    [RelayCommand]
    private void AddNewRule()
    {
        // 重置编辑表单
        EditVariableKey = string.Empty;
        EditDataType = "BYTE";
        EditDescription = string.Empty;
        EditSeverity = AlarmSeverity.Warning;
        EditConditionType = AlarmConditionType.High;
        EditThreshold = 0;
        EditDeadband = 2;
        EditOnDelayMs = 0;
        EditOffDelayMs = 0;
        EditArea = string.Empty;
        EditIsEnabled = true;
        _ruleBeingEdited = null;
        IsEditingRule = true;
    }

    [RelayCommand]
    private void EditRule(AlarmRule? rule)
    {
        if (rule == null) return;
        EditVariableKey = rule.VariableKey;
        EditDataType = rule.DataType;
        EditDescription = rule.Description;
        EditSeverity = rule.Severity;
        EditConditionType = rule.ConditionType;
        EditThreshold = rule.Threshold;
        EditDeadband = rule.Deadband;
        EditOnDelayMs = rule.OnDelayMs;
        EditOffDelayMs = rule.OffDelayMs;
        EditArea = rule.Area;
        EditIsEnabled = rule.IsEnabled;
        _ruleBeingEdited = rule;
        IsEditingRule = true;
    }

    [RelayCommand]
    private void SaveRule()
    {
        if (string.IsNullOrWhiteSpace(EditVariableKey))
        {
            StatusText = "⚠ 变量名不能为空";
            return;
        }

        var rule = new AlarmRule
        {
            VariableKey = EditVariableKey.Trim(),
            DataType = EditDataType,
            Description = EditDescription.Trim(),
            Severity = EditSeverity,
            ConditionType = EditConditionType,
            Threshold = EditThreshold,
            Deadband = EditDeadband,
            OnDelayMs = EditOnDelayMs,
            OffDelayMs = EditOffDelayMs,
            Area = EditArea.Trim(),
            IsEnabled = EditIsEnabled,
        };

        if (_ruleBeingEdited != null)
        {
            _alarmService.UpdateRule(_ruleBeingEdited, rule);
            StatusText = $"规则已更新: {rule.Description}";
        }
        else
        {
            _alarmService.AddRule(rule);
            StatusText = $"规则已添加: {rule.Description}";
        }

        // 若 VariableKey 是 DB{N}:{O} 格式，自动同步 DbPollItem
        SyncDbPollItem(rule);

        IsEditingRule = false;
        _ruleBeingEdited = null;
    }

    [RelayCommand]
    private void CancelEditRule()
    {
        IsEditingRule = false;
        _ruleBeingEdited = null;
    }

    /// <summary>
    /// 若 VariableKey 是 DB{N}:{O} 格式，自动同步 DbPollItem 到 PollingScheduler。
    /// 确保 PLC 确实在轮询该变量，否则报警规则永远收不到数据。
    /// </summary>
    private void SyncDbPollItem(AlarmRule rule)
    {
        var match = System.Text.RegularExpressions.Regex.Match(rule.VariableKey, @"^DB(\d+):(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return;

        int dbNumber = int.Parse(match.Groups[1].ValueSpan);
        int offset = int.Parse(match.Groups[2].ValueSpan);

        // 查找是否已有对应 DbPollItem
        var items = _scheduler.Config.DbItems;
        var existing = items.FirstOrDefault(i =>
            i.DbNumber == dbNumber && i.Offset == offset);

        if (existing != null)
        {
            // 更新 DataType（用户可能改了类型）
            existing.DataType = rule.DataType;
            existing.Enabled = rule.IsEnabled;
        }
        else
        {
            // 自动创建 DbPollItem
            var item = new DbPollItem
            {
                DbNumber = dbNumber,
                Offset = offset,
                DataType = rule.DataType,
                Label = rule.Description,
                Enabled = rule.IsEnabled,
            };
            items.Add(item);
        }
    }

    [RelayCommand]
    private void DeleteRule(AlarmRule? rule)
    {
        if (rule == null) return;
        _alarmService.RemoveRule(rule);
        StatusText = $"规则已删除: {rule.Description}";
    }

    // ========== 确认 ==========

    [RelayCommand]
    private void AcknowledgeAlarm(AlarmItem? alarm)
    {
        if (alarm == null) return;
        _alarmService.AcknowledgeAlarm(alarm);
    }

    [RelayCommand]
    private void AcknowledgeAll()
    {
        _alarmService.AcknowledgeAll();
    }

    // ========== 搁置 ==========

    [RelayCommand]
    private void ShelveAlarm(AlarmItem? alarm)
    {
        if (alarm == null || alarm.IsShelved) return;
        // 默认搁置 30 分钟
        _alarmService.ShelveAlarm(alarm, TimeSpan.FromMinutes(30));
    }

    [RelayCommand]
    private void ShelveAlarm1H(AlarmItem? alarm)
    {
        if (alarm == null || alarm.IsShelved) return;
        _alarmService.ShelveAlarm(alarm, TimeSpan.FromHours(1));
    }

    [RelayCommand]
    private void ShelveAlarm8H(AlarmItem? alarm)
    {
        if (alarm == null || alarm.IsShelved) return;
        _alarmService.ShelveAlarm(alarm, TimeSpan.FromHours(8));
    }

    [RelayCommand]
    private void ShelveAlarm24H(AlarmItem? alarm)
    {
        if (alarm == null || alarm.IsShelved) return;
        _alarmService.ShelveAlarm(alarm, TimeSpan.FromHours(24));
    }

    [RelayCommand]
    private void UnshelveAlarm(AlarmItem? alarm)
    {
        if (alarm == null || !alarm.IsShelved) return;
        _alarmService.UnshelveAlarm(alarm);
    }

    [RelayCommand]
    private void ShelveAll()
    {
        foreach (var alarm in ActiveAlarms.Where(a => !a.IsShelved).ToList())
        {
            _alarmService.ShelveAlarm(alarm, TimeSpan.FromHours(1));
        }
    }

    // ========== 清除 ==========

    [RelayCommand]
    private void ClearAll()
    {
        _alarmService.ClearAll();
        FilteredAlarms = Alarms;
        RefreshStatistics();
        StatusText = "报警历史已清除";
    }

    // ========== 导出 ==========

    [RelayCommand]
    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出报警记录",
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = $"报警记录_{DateTime.Now:yyyy-MM-dd_HHmmss}.csv",
            DefaultExt = ".csv",
            AddExtension = true,
        };

        if (dialog.ShowDialog() == true)
        {
            _alarmService.ExportToCsv(dialog.FileName, FilteredAlarms);
            StatusText = $"已导出 {FilteredAlarms.Count} 条报警到 {dialog.FileName}";
        }
    }

    // ========== 备注 ==========

    [RelayCommand]
    private void SaveComment()
    {
        if (SelectedAlarm != null)
        {
            _alarmService.AddComment(SelectedAlarm, SelectedAlarm.Comment);
            StatusText = "备注已保存";
        }
    }

    // ========== 过滤 ==========

    [RelayCommand]
    private void ApplyFilter()
    {
        IEnumerable<AlarmItem> query = Alarms;

        // 搁置过滤
        if (!ShowShelved)
            query = query.Where(a => !a.IsShelved);

        // 文本搜索
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var text = FilterText.Trim();
            query = query.Where(a =>
                a.VariableName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                a.Area.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        // 严重度过滤
        if (SelectedSeverityOption?.Severity is { } severity)
        {
            query = query.Where(a => a.Severity == severity);
        }

        // 区域过滤
        if (!string.IsNullOrWhiteSpace(FilterArea))
        {
            query = query.Where(a =>
                a.Area.Equals(FilterArea, StringComparison.OrdinalIgnoreCase));
        }

        // 日期范围
        if (FilterDateFrom.HasValue)
            query = query.Where(a => a.Timestamp >= FilterDateFrom.Value);

        if (FilterDateTo.HasValue)
            query = query.Where(a => a.Timestamp <= FilterDateTo.Value.AddDays(1));

        FilteredAlarms = new ObservableCollection<AlarmItem>(ApplySort(query));
        RefreshStatistics();
    }

    [RelayCommand]
    private void ResetFilter()
    {
        FilterText = string.Empty;
        FilterSeverity = null;
        SelectedSeverityOption = SeverityOptions.FirstOrDefault();
        FilterArea = string.Empty;
        FilterDateFrom = null;
        FilterDateTo = null;
        ShowShelved = false;
        SortColumn = "Timestamp";
        SortDirection = SortDirection.Descending;
        UpdateSortGlyphs();
        FilteredAlarms = new ObservableCollection<AlarmItem>(ApplySort(Alarms));
        RefreshStatistics();
    }

    // ========== 排序 ==========

    [RelayCommand]
    private void SortByTime()
    {
        ToggleSort("Timestamp");
        ApplyFilter();
    }

    [RelayCommand]
    private void SortBySeverity()
    {
        ToggleSort("Severity");
        ApplyFilter();
    }

    [RelayCommand]
    private void SortByVariable()
    {
        ToggleSort("VariableName");
        ApplyFilter();
    }

    [RelayCommand]
    private void SortByStatus()
    {
        ToggleSort("StatusText");
        ApplyFilter();
    }

    [RelayCommand]
    private void SortByArea()
    {
        ToggleSort("Area");
        ApplyFilter();
    }

    private void ToggleSort(string column)
    {
        if (SortColumn == column)
        {
            SortDirection = SortDirection switch
            {
                SortDirection.Ascending => SortDirection.Descending,
                SortDirection.Descending => SortDirection.Ascending,
                _ => SortDirection.Descending,
            };
        }
        else
        {
            SortColumn = column;
            SortDirection = SortDirection.Descending;
        }
        UpdateSortGlyphs();
    }

    private void UpdateSortGlyphs()
    {
        TimeSortGlyph = SortColumn == "Timestamp" ? (SortDirection == SortDirection.Descending ? "▼" : "▲") : "";
        SeveritySortGlyph = SortColumn == "Severity" ? (SortDirection == SortDirection.Descending ? "▼" : "▲") : "";
        VariableSortGlyph = SortColumn == "VariableName" ? (SortDirection == SortDirection.Descending ? "▼" : "▲") : "";
        StatusSortGlyph = SortColumn == "StatusText" ? (SortDirection == SortDirection.Descending ? "▼" : "▲") : "";
        AreaSortGlyph = SortColumn == "Area" ? (SortDirection == SortDirection.Descending ? "▼" : "▲") : "";
    }

    private IEnumerable<AlarmItem> ApplySort(IEnumerable<AlarmItem> query)
    {
        bool desc = SortDirection == SortDirection.Descending;
        return SortColumn switch
        {
            "Severity" => desc
                ? query.OrderByDescending(a => a.Severity).ThenByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.Severity).ThenByDescending(a => a.Timestamp),
            "VariableName" => desc
                ? query.OrderByDescending(a => a.VariableName).ThenByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.VariableName).ThenByDescending(a => a.Timestamp),
            "StatusText" => desc
                ? query.OrderByDescending(a => a.StatusText).ThenByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.StatusText).ThenByDescending(a => a.Timestamp),
            "Area" => desc
                ? query.OrderByDescending(a => a.Area).ThenByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.Area).ThenByDescending(a => a.Timestamp),
            _ => desc
                ? query.OrderByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.Timestamp),
        };
    }

    // ========== 统计 ==========

    [RelayCommand]
    private void RefreshStatistics()
    {
        var stats = _alarmService.GetStatistics();
        UnacknowledgedCount = stats.TotalUnacknowledged;
        ShelvedCount = stats.TotalShelved;
        EmergencyCount = stats.TotalEmergency;
        CriticalCount = stats.TotalCritical;
        TodayCount = stats.TotalToday;
        ThisHourCount = stats.TotalThisHour;
    }

    // ========== 刷新区域列表 ==========

    private Task RefreshAreasAsync()
    {
        var areas = Alarms
            .Where(a => !string.IsNullOrEmpty(a.Area))
            .Select(a => a.Area)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a)
            .ToList();

        AreaOptions = new ObservableCollection<string>(areas);
        return Task.CompletedTask;
    }

    // ========== 事件处理 ==========

    private void OnAlarmRaised(AlarmItem alarm)
    {
        RefreshStatistics();
        StatusText = $"⚠ 新报警: {alarm.Description}";
        _ = RefreshAreasAsync();
    }

    private void OnAlarmCleared(AlarmItem alarm)
    {
        RefreshStatistics();
    }

    private void OnAlarmChanged(AlarmItem alarm)
    {
        RefreshStatistics();
    }

    // ========== 导航生命周期 ==========

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        RefreshStatistics();
        ApplyFilter();
    }
}
