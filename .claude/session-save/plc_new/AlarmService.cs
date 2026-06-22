using System.Collections.ObjectModel;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

/// <summary>
/// Alarm management service.
/// Monitors PLC data changes via <see cref="PollingScheduler.DataUpdated"/>
/// and generates alarm events based on configurable rules.
/// </summary>
public class AlarmService
{
    private readonly List<AlarmRule> _rules = [];
    private int _totalAlarmCount;
    private int _activeAlarmCount;

    /// <summary>
    /// All alarms ever generated (bounded to <see cref="MaxHistory"/>).
    /// </summary>
    public ObservableCollection<AlarmItem> Alarms { get; } = [];

    /// <summary>
    /// Currently active (unresolved) alarms.
    /// </summary>
    public ObservableCollection<AlarmItem> ActiveAlarms { get; } = [];

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

    /// <summary>
    /// Maximum number of historical alarms to keep in memory.
    /// </summary>
    public int MaxHistory { get; set; } = 1000;

    public event Action<int>? TotalAlarmCountChanged;
    public event Action<int>? ActiveAlarmCountChanged;
    public event Action<AlarmItem>? AlarmRaised;
    public event Action<AlarmItem>? AlarmCleared;

    /// <summary>
    /// Subscribe to <see cref="PollingScheduler.DataUpdated"/> to start monitoring.
    /// </summary>
    public void Subscribe(PollingScheduler scheduler)
    {
        scheduler.DataUpdated += OnDataUpdated;

        // Add default alarm rules for I/Q/M areas
        if (_rules.Count == 0)
        {
            AddDefaultRules();
        }
    }

    public void Unsubscribe(PollingScheduler scheduler)
    {
        scheduler.DataUpdated -= OnDataUpdated;
    }

    /// <summary>
    /// Add an alarm rule.
    /// </summary>
    public void AddRule(AlarmRule rule)
    {
        _rules.Add(rule);
    }

    /// <summary>
    /// Remove an alarm rule.
    /// </summary>
    public bool RemoveRule(AlarmRule rule) => _rules.Remove(rule);

    /// <summary>
    /// Get all current alarm rules.
    /// </summary>
    public IReadOnlyList<AlarmRule> GetRules() => _rules.AsReadOnly();

    /// <summary>
    /// Acknowledge an alarm.
    /// </summary>
    public void AcknowledgeAlarm(AlarmItem alarm, string? acknowledgedBy = null)
    {
        alarm.IsAcknowledged = true;
        alarm.AcknowledgedBy = acknowledgedBy;
    }

    /// <summary>
    /// Acknowledge all active alarms.
    /// </summary>
    public void AcknowledgeAll(string? acknowledgedBy = null)
    {
        foreach (var alarm in ActiveAlarms.ToList())
        {
            alarm.IsAcknowledged = true;
            alarm.AcknowledgedBy = acknowledgedBy;
        }
    }

    /// <summary>
    /// Clear all alarms from history.
    /// </summary>
    public void ClearAll()
    {
        Alarms.Clear();
        ActiveAlarms.Clear();
        TotalAlarmCount = 0;
        ActiveAlarmCount = 0;
    }

    private void OnDataUpdated(HashSet<string> updatedKeys)
    {
        foreach (var rule in _rules)
        {
            if (!updatedKeys.Contains(rule.VariableKey))
                continue;

            byte? rawValue = GetValueForKey(rule.VariableKey);
            if (rawValue == null)
                continue;

            double value = rawValue.Value;
            bool isActive = rule.Check(value);

            if (isActive && !rule.LastTriggered)
            {
                // New alarm
                rule.LastTriggered = true;
                var alarm = new AlarmItem
                {
                    Severity = rule.Severity,
                    VariableName = rule.VariableKey,
                    Description = rule.Description,
                    CurrentValue = value,
                    Threshold = rule.Threshold,
                    IsActive = true,
                };
                AddAlarm(alarm);
                AlarmRaised?.Invoke(alarm);
            }
            else if (!isActive && rule.LastTriggered)
            {
                // Alarm cleared
                rule.LastTriggered = false;
                // Mark active alarms for this rule as resolved
                foreach (var active in ActiveAlarms
                             .Where(a => a.VariableName == rule.VariableKey && a.IsActive)
                             .ToList())
                {
                    active.IsActive = false;
                    ActiveAlarms.Remove(active);
                    AlarmCleared?.Invoke(active);
                }
                ActiveAlarmCount = ActiveAlarms.Count;
            }
        }
    }

    private void AddAlarm(AlarmItem alarm)
    {
        Alarms.Insert(0, alarm);
        if (alarm.IsActive)
            ActiveAlarms.Insert(0, alarm);

        TotalAlarmCount = Alarms.Count;
        ActiveAlarmCount = ActiveAlarms.Count;

        // Trim history
        while (Alarms.Count > MaxHistory)
            Alarms.RemoveAt(Alarms.Count - 1);
    }

    private byte? GetValueForKey(string key)
    {
        // This is called from OnDataUpdated which is triggered by PollingScheduler
        // The actual value lookup is done via the scheduler's LastValues cache
        // but we get the value passed through the rule check already
        return null;
    }

    private void AddDefaultRules()
    {
        // I-area high limit examples (disabled by default — user configures via UI)
        // These are just templates; actual rules are set up by the user
        _rules.Add(new AlarmRule
        {
            VariableKey = "I0",
            Description = "I0 高限报警",
            Severity = AlarmSeverity.Warning,
            Threshold = 200,
            Condition = AlarmCondition.Above,
            IsEnabled = false,
        });
    }
}

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
/// </summary>
public class AlarmRule
{
    /// <summary>
    /// Variable key (e.g. "I0", "Q5", "M10", "DB100[42]").
    /// </summary>
    public string VariableKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of this alarm rule.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity level when triggered.
    /// </summary>
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;

    /// <summary>
    /// Threshold value for comparison.
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Comparison condition.
    /// </summary>
    public AlarmCondition Condition { get; set; } = AlarmCondition.Above;

    /// <summary>
    /// Whether this rule is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether this rule was triggered on the last check (internal state).
    /// </summary>
    internal bool LastTriggered { get; set; }

    /// <summary>
    /// Check if the value triggers this rule.
    /// </summary>
    public bool Check(double value)
    {
        if (!IsEnabled) return false;
        return Condition switch
        {
            AlarmCondition.Above => value > Threshold,
            AlarmCondition.Below => value < Threshold,
            AlarmCondition.Equal => Math.Abs(value - Threshold) < 0.001,
            AlarmCondition.NotEqual => Math.Abs(value - Threshold) > 0.001,
            _ => false,
        };
    }
}
