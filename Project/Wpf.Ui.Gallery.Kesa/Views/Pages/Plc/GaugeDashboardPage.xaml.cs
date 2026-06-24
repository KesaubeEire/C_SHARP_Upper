using System.Windows.Controls;
using LiveChartsCore.SkiaSharpView.WPF;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class GaugeDashboardPage : Page
{
    private readonly S7Service _s7;
    private readonly ServoGaugeConfig[] _configs;

    /// <summary>伺服危险速度阈值 [伺服1, 伺服2, 伺服3, 伺服4]</summary>
    private readonly double[] _dangerThresholds = [160, 180, 160, 180];

    // 每个 gauge 的控件分组
    private readonly (XamlAngularGaugeSeries Green, XamlAngularGaugeSeries Yellow, XamlAngularGaugeSeries Red, XamlNeedle Needle, TextBlock ValueText)[] _gauges;

    public GaugeDashboardPage(S7Service s7)
    {
        _s7 = s7;
        InitializeComponent();

        // CornerRadius workaround (见原代码注释)
        foreach (var g in AllGaugeSeries())
            g.CornerRadius = 0.1;

        Loaded += (_, _) =>
        {
            foreach (var g in AllGaugeSeries())
                g.CornerRadius = 0;

            ReadAllServoValues();
        };

        _gauges =
        [
            (servo1Green, servo1Yellow, servo1Red, servo1Needle, servo1Value),
            (servo2Green, servo2Yellow, servo2Red, servo2Needle, servo2Value),
            (servo3Green, servo3Yellow, servo3Red, servo3Needle, servo3Value),
            (servo4Green, servo4Yellow, servo4Red, servo4Needle, servo4Value),
        ];

        // 默认配置：DB1.DBX6.0 REAL, DB1.DBX10.0 REAL, DB1.DBX14.0 REAL, DB1.DBX18.0 REAL
        // 改这里就行
        _configs =
        [
            new() { Name = "伺服 1", DbNumber = 1, Offset = 6, DataType = "REAL", DangerThreshold = _dangerThresholds[0] },
            new() { Name = "伺服 2", DbNumber = 1, Offset = 10, DataType = "REAL", DangerThreshold = _dangerThresholds[1] },
            new() { Name = "伺服 3", DbNumber = 1, Offset = 14, DataType = "REAL", DangerThreshold = _dangerThresholds[2] },
            new() { Name = "伺服 4", DbNumber = 1, Offset = 18, DataType = "REAL", DangerThreshold = _dangerThresholds[3] },
        ];
    }

    private IEnumerable<XamlAngularGaugeSeries> AllGaugeSeries()
    {
        yield return servo1Green; yield return servo1Yellow; yield return servo1Red;
        yield return servo2Green; yield return servo2Yellow; yield return servo2Red;
        yield return servo3Green; yield return servo3Yellow; yield return servo3Red;
        yield return servo4Green; yield return servo4Yellow; yield return servo4Red;
    }

    /// <summary>读取所有伺服的 DB 值并更新仪表</summary>
    public void ReadAllServoValues()
    {
        for (int i = 0; i < _configs.Length; i++)
        {
            var cfg = _configs[i];
            byte[]? buf = _s7.ReadBytesRaw(S7Service.AreaDB, cfg.Offset, cfg.DataSize, cfg.DbNumber);
            if (buf == null) continue;

            double val = DecodeValue(buf, cfg.DataType);
            UpdateServoValue(i, val);
        }
    }

    /// <summary>更新指定伺服的仪表值。</summary>
    /// <param name="index">0-based 伺服索引 (0-3)。</param>
    /// <param name="value">速度值 (mm/s)。</param>
    public void UpdateServoValue(int index, double value)
    {
        if (index < 0 || index >= _gauges.Length) return;

        Dispatcher.InvokeAsync(() =>
        {
            var (green, yellow, red, needle, valueText) = _gauges[index];
            double danger = _dangerThresholds[index];
            double greenMax = danger * 0.6; // 绿→黄分界点
            double clamped = Math.Clamp(value, 0, 200);

            valueText.Text = value.ToString("F1");
            needle.Value = clamped;

            if (clamped <= greenMax)
            {
                green.GaugeValue = clamped;
                yellow.GaugeValue = 0;
                red.GaugeValue = 0;
            }
            else if (clamped <= danger)
            {
                green.GaugeValue = greenMax;
                yellow.GaugeValue = clamped - greenMax;
                red.GaugeValue = 0;
            }
            else
            {
                green.GaugeValue = greenMax;
                yellow.GaugeValue = danger - greenMax;
                red.GaugeValue = clamped - danger;
            }
        });
    }

    /// <summary>根据数据类型解码字节数组为 double</summary>
    private static double DecodeValue(byte[] buf, string dataType)
    {
        return dataType.ToUpperInvariant() switch
        {
            "REAL" => Sharp7.S7.GetRealAt(buf, 0),
            "DINT" => Sharp7.S7.GetDIntAt(buf, 0),
            "INT" => Sharp7.S7.GetIntAt(buf, 0),
            "WORD" => Sharp7.S7.GetWordAt(buf, 0),
            "BYTE" => buf[0],
            _ => Sharp7.S7.GetRealAt(buf, 0),
        };
    }
}
