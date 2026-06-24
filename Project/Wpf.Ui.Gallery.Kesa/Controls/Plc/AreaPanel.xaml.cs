using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Controls.Plc;

public partial class AreaPanel : UserControl
{
    public static readonly DependencyProperty AreaTypeProperty =
        DependencyProperty.Register(nameof(AreaType), typeof(string), typeof(AreaPanel),
            new PropertyMetadata("I", OnAreaTypeChanged));

    public static readonly DependencyProperty AreaColorProperty =
        DependencyProperty.Register(nameof(AreaColor), typeof(Brush), typeof(AreaPanel),
            new PropertyMetadata(GetResourceBrush("SystemFillColorAttentionBrush",
                Color.FromRgb(52, 152, 219))));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(AreaPanel),
            new PropertyMetadata(false));

    public static readonly DependencyProperty AddressTextProperty =
        DependencyProperty.Register(nameof(AddressText), typeof(string), typeof(AreaPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShowEmptyHintProperty =
        DependencyProperty.Register(nameof(ShowEmptyHint), typeof(bool), typeof(AreaPanel),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowWriteButtonProperty =
        DependencyProperty.Register(nameof(ShowWriteButton), typeof(bool), typeof(AreaPanel),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowStatusColumnProperty =
        DependencyProperty.Register(nameof(ShowStatusColumn), typeof(bool), typeof(AreaPanel),
            new PropertyMetadata(false));

    public string AreaType
    {
        get => (string)GetValue(AreaTypeProperty);
        set => SetValue(AreaTypeProperty, value);
    }

    public Brush AreaColor
    {
        get => (Brush)GetValue(AreaColorProperty);
        set => SetValue(AreaColorProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool ShowEmptyHint
    {
        get => (bool)GetValue(ShowEmptyHintProperty);
        set => SetValue(ShowEmptyHintProperty, value);
    }

    public bool ShowWriteButton
    {
        get => (bool)GetValue(ShowWriteButtonProperty);
        set => SetValue(ShowWriteButtonProperty, value);
    }

    public bool ShowStatusColumn
    {
        get => (bool)GetValue(ShowStatusColumnProperty);
        set => SetValue(ShowStatusColumnProperty, value);
    }

    public string? AddressText
    {
        get => (string?)GetValue(AddressTextProperty);
        set => SetValue(AddressTextProperty, value);
    }

    // 改为 DP 以便绑定通知
    public static readonly DependencyProperty AreaLabelProperty =
        DependencyProperty.Register(nameof(AreaLabel), typeof(string), typeof(AreaPanel),
            new PropertyMetadata("I 区（输入 · 只读）"));

    public string AreaLabel
    {
        get => (string)GetValue(AreaLabelProperty);
        private set => SetValue(AreaLabelProperty, value);
    }

    public ObservableCollection<ByteRowViewModel> ByteRows { get; } = [];

    private S7Service? _s7;
    private int _areaCode = S7Service.AreaI;
    private bool _writeMode;
    private bool _manualSet;

    public AreaPanel()
    {
        DataContext = this;
        InitializeComponent();
        ApplyAreaType();
    }

    public void Init(S7Service s7) => _s7 = s7;

    private static void OnAreaTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AreaPanel panel) panel.ApplyAreaType();
    }

    private void ApplyAreaType()
    {
        switch (AreaType)
        {
            case "I":
                AreaLabel = "I 区（输入 · 只读）";
                AreaColor = GetResourceBrush("SystemFillColorAttentionBrush", Color.FromRgb(52, 152, 219));
                _areaCode = S7Service.AreaI;
                break;
            case "Q":
                AreaLabel = "Q 区（输出 · 可读写）";
                AreaColor = GetResourceBrush("SystemFillColorCriticalBrush", Color.FromRgb(231, 76, 60));
                _areaCode = S7Service.AreaQ;
                break;
            case "M":
                AreaLabel = "M 区（位存储 · 可读写）";
                AreaColor = GetResourceBrush("SystemFillColorSuccessBrush", Color.FromRgb(46, 204, 113));
                _areaCode = S7Service.AreaM;
                break;
        }

        // I 区隐藏写模式按钮和状态列，Q/M 显示
        bool ro = IsReadOnly || AreaType == "I";
        ShowWriteButton = !ro;
        ShowStatusColumn = !ro;

        // 默认地址（仅第一次）
        if (!_manualSet && string.IsNullOrEmpty(addrInput.Text))
            addrInput.Text = AreaType == "I" ? "0,1" : "0";
    }

    private void OnReadClick(object sender, RoutedEventArgs e)
    {
        if (_s7 == null) return;
        _manualSet = true;

        var addrs = ParseAddresses(addrInput.Text);
        if (addrs.Length == 0) return;

        var bytes = _s7.ReadBytes(_areaCode, addrs);
        ByteRows.Clear();
        ShowEmptyHint = false;

        foreach (var addr in addrs)
        {
            var row = new ByteRowViewModel(addr, AreaType, IsReadOnly)
            {
                WriteModeEnabled = _writeMode
            };
            if (bytes.TryGetValue(addr, out byte val))
                row.Value = val;
            ByteRows.Add(row);
        }
    }

    private void OnWriteModeClick(object sender, RoutedEventArgs e)
    {
        _writeMode = !_writeMode;
        foreach (var row in ByteRows)
            row.WriteModeEnabled = _writeMode;
        if (sender is System.Windows.Controls.Primitives.ButtonBase btn)
        {
            btn.Content = _writeMode ? "写入" : "写模式";
            btn.SetCurrentValue(Wpf.Ui.Controls.Button.AppearanceProperty,
                _writeMode ? ControlAppearance.Danger : ControlAppearance.Secondary);
        }
    }

    private async void OnRowBitToggled(object sender, BitToggledEventArgs e)
    {
        if (_s7 == null || IsReadOnly || !_writeMode) return;
        if (!_s7.IsConnected)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "提示", Content = "PLC 未连接" }.ShowDialogAsync();
            return;
        }
        var bit = e.Bit;
        if (bit.Parent == null) return;
        bit.Toggle();
        if (!_s7.WriteByte(_areaCode, bit.Parent.ByteAddress, bit.Parent.ToByte()))
        {
            bit.Toggle(); // 写入失败还原
            await new Wpf.Ui.Controls.MessageBox { Title = "PLC 写入错误", Content = _s7.LastError ?? "未知错误" }.ShowDialogAsync();
        }
    }

    public void UpdateFromPoll(HashSet<string> updated, PollingScheduler scheduler)
    {
        foreach (var row in ByteRows)
        {
            string key = $"{AreaType}{row.ByteAddress}";
            if (updated.Contains(key))
            {
                var val = scheduler.GetValue(key);
                if (val.HasValue)
                    row.Value = val.Value;
            }
        }
    }

    private static int[] ParseAddresses(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(s => s.Trim()).Where(s => int.TryParse(s, out _))
                   .Select(int.Parse).Distinct().OrderBy(a => a).ToArray();
    }

    private static Brush GetResourceBrush(string key, Color fallback)
    {
        return Application.Current.TryFindResource(key) as Brush
               ?? new SolidColorBrush(fallback);
    }
}
