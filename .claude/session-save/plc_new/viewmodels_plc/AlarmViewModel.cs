using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Plc;

/// <summary>
/// ViewModel for the Alarm management page.
/// </summary>
public partial class AlarmViewModel : ObservableObject
{
    private readonly AlarmService _alarmService;
    private readonly PollingScheduler _scheduler;

    [ObservableProperty]
    private ObservableCollection<AlarmItem> _alarms = [];

    [ObservableProperty]
    private ObservableCollection<AlarmItem> _activeAlarms = [];

    [ObservableProperty]
    private ObservableCollection<AlarmItem> _filteredAlarms = [];

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private bool _isSubscribed;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private AlarmSeverity _filterSeverity = AlarmSeverity.Info;

    [ObservableProperty]
    private bool _filterBySeverity;

    [ObservableProperty]
    private string _statusText = "报警系统就绪";

    public AlarmViewModel(AlarmService alarmService, PollingScheduler scheduler)
    {
        _alarmService = alarmService;
        _scheduler = scheduler;

        Alarms = _alarmService.Alarms;
        ActiveAlarms = _alarmService.ActiveAlarms;
        FilteredAlarms = Alarms;

        _alarmService.AlarmRaised += OnAlarmRaised;
        _alarmService.AlarmCleared += OnAlarmCleared;
        _alarmService.TotalAlarmCountChanged += count => TotalCount = count;
        _alarmService.ActiveAlarmCountChanged += count => ActiveCount = count;

        TotalCount = _alarmService.TotalAlarmCount;
        ActiveCount = _alarmService.ActiveAlarmCount;
    }

    [RelayCommand]
    private void Subscribe()
    {
        if (IsSubscribed) return;
        _alarmService.Subscribe(_scheduler);
        IsSubscribed = true;
        StatusText = "报警监控已启动";
    }

    [RelayCommand]
    private void Unsubscribe()
    {
        if (!IsSubscribed) return;
        _alarmService.Unsubscribe(_scheduler);
        IsSubscribed = false;
        StatusText = "报警监控已停止";
    }

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

    [RelayCommand]
    private void ClearAll()
    {
        _alarmService.ClearAll();
        StatusText = "报警历史已清除";
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        var filtered = Alarms.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(a =>
                a.VariableName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        if (FilterBySeverity)
        {
            filtered = filtered.Where(a => a.Severity >= FilterSeverity);
        }

        FilteredAlarms = new ObservableCollection<AlarmItem>(filtered);
    }

    [RelayCommand]
    private void ResetFilter()
    {
        FilterText = string.Empty;
        FilterBySeverity = false;
        FilteredAlarms = Alarms;
    }

    private void OnAlarmRaised(AlarmItem alarm)
    {
        // UI updates happen through collection binding
        StatusText = $"⚠ 新报警: {alarm.Description}";
    }

    private void OnAlarmCleared(AlarmItem alarm)
    {
        // UI updates happen through collection binding
    }
}
