using System.Windows.Controls;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class IoMonitorPage : Page
{
    private readonly S7Service _s7;
    private readonly PollingScheduler _scheduler;
    private readonly AppConfigService _config;

    public IoMonitorPage(S7Service s7, PollingScheduler scheduler, AppConfigService config)
    {
        _s7 = s7;
        _scheduler = scheduler;
        _config = config;
        InitializeComponent();

        iPanel.Init(_s7);
        qPanel.Init(_s7);
        mPanel.Init(_s7);

        // 恢复上次保存的 I/Q/M 地址
        iPanel.AddressText = _config.ManualIAddress;
        qPanel.AddressText = _config.ManualQAddress;
        mPanel.AddressText = _config.ManualMAddress;

        // 地址一改立即保存，避免关窗口时 Unloaded 不触发丢失数据
        iPanel.AddressTextChanged += (_, _) => SaveAddresses();
        qPanel.AddressTextChanged += (_, _) => SaveAddresses();
        mPanel.AddressTextChanged += (_, _) => SaveAddresses();

        _scheduler.DataUpdated += OnPollData;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SaveAddresses();
    }

    /// <summary>将当前 I/Q/M 地址保存到配置</summary>
    public void SaveAddresses()
    {
        _config.ManualIAddress = iPanel.AddressText ?? "";
        _config.ManualQAddress = qPanel.AddressText ?? "";
        _config.ManualMAddress = mPanel.AddressText ?? "";
        _config.Save();
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
