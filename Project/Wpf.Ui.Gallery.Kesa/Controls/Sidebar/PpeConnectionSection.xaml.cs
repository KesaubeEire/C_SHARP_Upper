using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Controls.Sidebar;

public partial class PpeConnectionSection : UserControl
{
    private readonly S7Service _s7;
    private readonly AppConfigService _config;
    private readonly PollingScheduler _scheduler;

    // ===== 连接部分 =====
    public PpeConnectionSection(S7Service s7, AppConfigService config, PollingScheduler scheduler)
    {
        _s7 = s7;
        _config = config;
        _scheduler = scheduler;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadAdapters();
        RestoreImports();
    }

    /// <summary>
    /// 阻止鼠标事件冒泡到父级 NavigationViewItem，
    /// 避免点击本控件的非交互区域（空白/disabled 按钮等）触发侧边栏菜单折叠。
    /// </summary>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 只拦截非交互控件的点击，正常的 Button/TextBox/ComboBox 仍然正常工作
        if (e.OriginalSource is ButtonBase or TextBox or ComboBox or ToggleButton)
        {
            return;
        }

        // 阻止事件继续冒泡到 NavigationViewItem，防止其 IsExpanded 被切换
        e.Handled = true;
    }

    // =================== 连接逻辑 ===================

    private void LoadAdapters()
    {
        adapterCombo.ItemsSource = NetworkAdapter.Enumerate();
        if (adapterCombo.Items.Count > 0) adapterCombo.SelectedIndex = 0;
    }

    private void OnConnect(object sender, RoutedEventArgs e)
    {
        string localIp = adapterCombo.SelectedItem is NetworkAdapter na ? na.Ip : "";
        string ip = ipInput.Text.Trim();
        int port = int.TryParse(portInput.Text, out int p) ? p : 102;
        int rack = int.TryParse(rackInput.Text, out int r) ? r : 0;
        int slot = int.TryParse(slotInput.Text, out int s) ? s : 1;

        int result = _s7.Connect(localIp, ip, port, rack, slot);
        if (result == 0)
        {
            statusBar.Visibility = Visibility.Visible;
            statusIndicator.Fill = (Brush)Application.Current.FindResource("SystemFillColorSuccessBrush");
            statusText.Text = "已连接";
            btnConnect.IsEnabled = false;
            btnDisconnect.IsEnabled = true;
        }
        else
        {
            statusBar.Visibility = Visibility.Visible;
            statusIndicator.Fill = (Brush)Application.Current.FindResource("SystemFillColorCriticalBrush");
            statusText.Text = $"连接失败: {_s7.LastError ?? "未知错误"}";
        }
    }

    private void OnDisconnect(object sender, RoutedEventArgs e)
    {
        _s7.Disconnect();
        statusIndicator.Fill = (Brush)Application.Current.FindResource("SystemFillColorNeutralBrush");
        statusText.Text = "已断开";
        btnConnect.IsEnabled = true;
        btnDisconnect.IsEnabled = false;
    }

    // =================== 导入逻辑 ===================

    public ObservableCollection<DbStructure> ImportedDbs { get; } = [];
    public ObservableCollection<UdtStructure> ImportedUdts { get; } = [];

    public event EventHandler? ListChanged;

    private void RestoreImports()
    {
        dbList.ItemsSource = ImportedDbs;
        udtList.ItemsSource = ImportedUdts;

        foreach (var d in _config.ImportedDbs)
        {
            ImportedDbs.Add(new DbStructure
            {
                DbNumber = d.DbNumber,
                DbName = d.DbName,
                SourceFile = d.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(d.VariablesJson) ?? []
            });
        }
        foreach (var u in _config.ImportedUdts)
        {
            ImportedUdts.Add(new UdtStructure
            {
                UdtName = u.UdtName,
                SourceFile = u.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(u.VariablesJson) ?? []
            });
        }
    }

    private async void OnImportDb(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DB 文件|*.db;*.txt|All files|*.*",
            Title = "导入 DB 结构"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var db = DbFileParser.Parse(dialog.FileName);
            db.DbNumber = 1;
            ImportedDbs.Add(db);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "错误", Content = $"导入失败: {ex.Message}" }.ShowDialogAsync();
        }
    }

    private async void OnImportUdt(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "UDT 文件|*.udt;*.txt|All files|*.*",
            Title = "导入 UDT 结构"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var udt = UdtFileParser.Parse(dialog.FileName);
            ImportedUdts.Add(udt);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "错误", Content = $"导入失败: {ex.Message}" }.ShowDialogAsync();
        }
    }

    private void OnDeleteDb(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DbStructure db)
        {
            ImportedDbs.Remove(db);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDeleteUdt(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is UdtStructure udt)
        {
            ImportedUdts.Remove(udt);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // =================== 轮询逻辑 ===================

    public int Interval => int.TryParse(intervalInput.Text, out int v) ? v : 500;

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (!_s7.IsConnected)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "提示", Content = "请先连接 PLC" }.ShowDialogAsync();
            return;
        }

        _scheduler.Config.FastInterval = Interval;
        _scheduler.Config.Fast.PollIAddr = "0,1,8";
        _scheduler.Config.Fast.PollQAddr = "0";
        _scheduler.Config.Fast.PollMAddr = "0";

        _scheduler.DataUpdated += OnPollDataUpdated;
        _scheduler.Start(_s7);
        if (_scheduler.IsConnected)
        {
            SetPollingRunning(true);
        }
        else
        {
            pollStatusText.Text = "轮询启动失败";
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        _scheduler.DataUpdated -= OnPollDataUpdated;
        _scheduler.Stop();
        SetPollingRunning(false);
    }

    private void OnPollDataUpdated(HashSet<string> _)
    {
        UpdateLatency(_scheduler.LatencyMs);
    }

    public void SetPollingRunning(bool running)
    {
        btnStart.IsEnabled = !running;
        btnStop.IsEnabled = running;
        pollIndicator.Fill = running
            ? (Brush)Application.Current.FindResource("SystemFillColorSuccessBrush")
            : (Brush)Application.Current.FindResource("SystemFillColorNeutralBrush");
        pollStatusText.Text = running ? "轮询运行中" : "已停止";
    }

    public void UpdateLatency(long ms)
    {
        Dispatcher.InvokeAsync(() => latencyText.Text = $"{ms} ms");
    }
}
