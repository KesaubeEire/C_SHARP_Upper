using System.Windows.Controls;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class IoMonitorPage : Page
{
    private readonly S7Service _s7;
    private readonly PollingScheduler _scheduler;

    public IoMonitorPage(S7Service s7, PollingScheduler scheduler)
    {
        _s7 = s7;
        _scheduler = scheduler;
        InitializeComponent();

        iPanel.Init(_s7);
        qPanel.Init(_s7);
        mPanel.Init(_s7);

        _scheduler.DataUpdated += OnPollData;
    }

    private void OnPollData(HashSet<string> updated)
    {
        Dispatcher.InvokeAsync(() =>
        {
            iPanel.UpdateFromPoll(updated, _scheduler);
            qPanel.UpdateFromPoll(updated, _scheduler);
            mPanel.UpdateFromPoll(updated, _scheduler);
        });
    }
}
