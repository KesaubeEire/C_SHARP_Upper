using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Gallery.Controls.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

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
