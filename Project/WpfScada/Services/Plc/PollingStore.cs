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
}
