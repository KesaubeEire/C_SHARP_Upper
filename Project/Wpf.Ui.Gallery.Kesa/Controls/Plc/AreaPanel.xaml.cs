using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219))));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(AreaPanel),
            new PropertyMetadata(false));

    public static readonly DependencyProperty AddressTextProperty =
        DependencyProperty.Register(nameof(AddressText), typeof(string), typeof(AreaPanel),
            new PropertyMetadata("0,1,8"));

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

    public string AddressText
    {
        get => (string)GetValue(AddressTextProperty);
        set => SetValue(AddressTextProperty, value);
    }

    public string AreaLabel { get; private set; } = "I 区（输入.只读）";
    public ObservableCollection<ByteRowViewModel> ByteRows { get; } = [];

    private S7Service? _s7;
    private int _areaCode = S7Service.AreaI;
    private bool _writeMode;

    public AreaPanel()
    {
        DataContext = this;
        InitializeComponent();
    }

    public void Init(S7Service s7) => _s7 = s7;

    private static void OnAreaTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AreaPanel panel) panel.UpdateAreaLabel();
    }

    private void UpdateAreaLabel()
    {
        switch (AreaType)
        {
            case "I":
                AreaLabel = "I 区（输入.只读）";
                AreaColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219));
                _areaCode = S7Service.AreaI;
                break;
            case "Q":
                AreaLabel = "Q 区（输出.可读写）";
                AreaColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
                _areaCode = S7Service.AreaQ;
                break;
            case "M":
                AreaLabel = "M 区（位存储.可读写）";
                AreaColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
                _areaCode = S7Service.AreaM;
                break;
        }
    }

    private void OnReadClick(object sender, RoutedEventArgs e)
    {
        if (_s7 == null) return;
        var addrs = ParseAddresses(addrInput.Text);
        if (addrs.Length == 0) return;

        var bytes = _s7.ReadBytes(_areaCode, addrs);
        ByteRows.Clear();
        emptyHint.Visibility = Visibility.Collapsed;

        foreach (var addr in addrs)
        {
            var row = new ByteRowViewModel(addr, AreaType[..1], IsReadOnly);
            if (bytes.TryGetValue(addr, out byte val))
                row.Value = val;
            ByteRows.Add(row);
        }
    }

    private void OnWriteModeClick(object sender, RoutedEventArgs e)
    {
        _writeMode = !_writeMode;
        if (sender is Button btn)
        {
            btn.Content = _writeMode ? "🔓 写入" : "🔒 写模式";
            btn.Background = _writeMode
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60))
                : System.Windows.Media.Brushes.Transparent;
            btn.Foreground = _writeMode
                ? System.Windows.Media.Brushes.White
                : Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush ?? System.Windows.Media.Brushes.Gray;
        }
    }

    public void UpdateFromPoll(HashSet<string> updated, PollingScheduler scheduler)
    {
        string prefix = AreaType[..1];
        foreach (var row in ByteRows)
        {
            string key = $"{prefix}{row.ByteAddress}";
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
                   .Where(x => int.TryParse(x, out _))
                   .Select(int.Parse)
                   .ToArray();
    }
}
