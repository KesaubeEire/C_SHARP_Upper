using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>
/// Severity level of an alarm.
/// </summary>
public enum AlarmSeverity
{
    Info,
    Warning,
    Critical,
    Emergency,
}

/// <summary>
/// Represents a single alarm event.
/// </summary>
public class AlarmItem : INotifyPropertyChanged
{
    private bool _isAcknowledged;
    private bool _isActive;

    public DateTime Timestamp { get; init; } = DateTime.Now;
    public AlarmSeverity Severity { get; init; }
    public string VariableName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double CurrentValue { get; init; }
    public double? Threshold { get; init; }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsAcknowledged
    {
        get => _isAcknowledged;
        set
        {
            if (_isAcknowledged != value)
            {
                _isAcknowledged = value;
                AcknowledgedAt = value ? DateTime.Now : null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public DateTime? AcknowledgedAt { get; private set; }
    public string? AcknowledgedBy { get; set; }

    public string TimeText => Timestamp.ToString("HH:mm:ss");

    public string StatusText => (IsActive, IsAcknowledged) switch
    {
        (true, false) => "未确认",
        (true, true) => "已确认",
        (false, _) => "已恢复",
    };

    public bool IsAcknowledgedAndActive => IsAcknowledged && IsActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
