using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TestWpf.Models;
using TestWpf.Services;

namespace TestWpf.Controls.Common;

/// <summary>
/// I/Q/M 三区通用面板 — 通过 AreaType / IsReadOnly 区分行为
/// </summary>
public partial class AreaPanel : UserControl
{
    private readonly ObservableCollection<ByteRowViewModel> _rows = [];
    private Dictionary<int, byte> _lastBytes = [];
    private bool _writeMode;

    // ===== 依赖属性 =====

    public static readonly DependencyProperty AreaTypeProperty =
        DependencyProperty.Register(nameof(AreaType), typeof(string), typeof(AreaPanel),
            new PropertyMetadata("I", OnAreaTypeChanged));

    public static readonly DependencyProperty AreaColorProperty =
        DependencyProperty.Register(nameof(AreaColor), typeof(Brush), typeof(AreaPanel),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x29, 0x80, 0xB9))));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(AreaPanel),
            new PropertyMetadata(false));

    public string AreaType { get => (string)GetValue(AreaTypeProperty); set => SetValue(AreaTypeProperty, value); }
    public Brush AreaColor { get => (Brush)GetValue(AreaColorProperty); set => SetValue(AreaColorProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>地址输入框的当前文本（供 MainWindow 保存/恢复）</summary>
    public string AddressText
    {
        get => txtAddress.Text;
        set
        {
            txtAddress.Text = value;
            _manualSet = !string.IsNullOrEmpty(value);
        }
    }
    private bool _manualSet;

    // ===== 构造 =====

    public AreaPanel()
    {
        InitializeComponent();
        listRows.ItemsSource = _rows;
        UpdateUI();
    }

    private static void OnAreaTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AreaPanel panel) panel.UpdateUI();
    }

    private string _areaLabel = "";
    public string AreaLabel => _areaLabel;

    private int _areaCode;

    private void UpdateUI()
    {
        _areaLabel = AreaType.ToUpper() switch
        {
            "I" => "I 区（输入 · 只读）",
            "Q" => "Q 区（输出 · 可读写）",
            "M" => "M 区（位存储 · 可读写）",
            var x => $"{x} 区"
        };
        _areaCode = AreaType.ToUpper() switch
        {
            "I" => S7Service.AreaI,
            "Q" => S7Service.AreaQ,
            "M" => S7Service.AreaM,
            _ => S7Service.AreaQ
        };
        // I 区没有写模式按钮和状态列
        btnWriteMode.Visibility = IsReadOnly ? Visibility.Collapsed : Visibility.Visible;
        colStatusHeader.Visibility = IsReadOnly ? Visibility.Collapsed : Visibility.Visible;
        // I 区地址默认值（仅当没有手动设置过时才填）
        if (!_manualSet && string.IsNullOrEmpty(txtAddress.Text))
            txtAddress.Text = AreaType.ToUpper() == "I" ? "0,1" : "0";
    }

    // ===== Service 注入 =====

    private S7Service? _s7;
    public void Init(S7Service s7) => _s7 = s7;

    // ===== 读取 =====

    private void OnReadClick(object sender, RoutedEventArgs e)
    {
        if (_s7 == null) return;
        var addrs = ParseAddrs(txtAddress.Text);
        if (addrs.Length == 0) return;
        _lastBytes = _s7.ReadBytes(_areaCode, addrs);
        _rows.Clear();
        foreach (int i in addrs)
            _rows.Add(new ByteRowViewModel(i, AreaType, IsReadOnly) { Value = _lastBytes.GetValueOrDefault(i) });
        UpdateEmpty();
    }

    // ===== 写模式切换（Q/M） =====

    private void OnWriteModeClick(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly) return;
        _writeMode = !_writeMode;
        btnWriteMode.Content = _writeMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)";
        btnWriteMode.Background = _writeMode
            ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
            : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    }

    // ===== 位点击 → 写入 PLC =====

    private void OnRowBitToggled(object sender, BitToggledEventArgs e)
    {
        if (_s7 == null || IsReadOnly || !_writeMode) return;
        var bit = e.Bit;
        if (bit.Parent == null) return;
        _s7.WriteByte(_areaCode, bit.Parent.ByteAddress, bit.Parent.ToByte());
    }

    // ===== 轮询数据更新（由 MainWindow 调用） =====

    /// <summary>轮询数据到达时刷新 UI</summary>
    public void UpdateFromPoll(HashSet<string> updated, PollingScheduler scheduler)
    {
        foreach (var row in _rows)
        {
            string key = $"{row.AreaLabel}{row.ByteAddress}";
            if (updated.Contains(key) && scheduler.GetValue(key) is byte val && val != row.Value)
                row.Value = val;
        }
    }

    // ===== 工具 =====

    private static int[] ParseAddrs(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split(',', '，', ';', '；', ' ')
            .Select(s => s.Trim()).Where(s => int.TryParse(s, out _))
            .Select(int.Parse).Distinct().OrderBy(a => a).ToArray();
    }

    private void UpdateEmpty()
    {
        txtEmpty.Text = _rows.Count == 0
            ? "输入字节地址后点击 [▶ 读取]"
            : "";
    }
}
