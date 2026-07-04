using System.Windows.Controls;
using WpfScada.Controls.Plc;
using WpfScada.Models.Plc;
using WpfScada.Services.Plc;

namespace WpfScada.Views.Pages.Plc;

public partial class GaugeDashboardPage : Page
{
    private readonly S7Service _s7;
    private readonly ServoGaugeConfig[] _configs;

    private readonly ServoGauge[] _gauges;

    /// <summary>伺服危险速度阈值 [伺服1, 伺服2, 伺服3, 伺服4]</summary>
    private readonly double[] _dangerThresholds = [160, 180, 160, 180];

    public GaugeDashboardPage(S7Service s7)
    {
        _s7 = s7;
        InitializeComponent();

        _gauges = [servo1, servo2, servo3, servo4];

        // 默认配置：DB1.DBX6.0 REAL, DB1.DBX10.0 REAL, DB1.DBX14.0 REAL, DB1.DBX18.0 REAL
        _configs =
        [
            new() { Name = "伺服 1", DbNumber = 1, Offset = 6, DataType = "REAL", DangerThreshold = _dangerThresholds[0] },
            new() { Name = "伺服 2", DbNumber = 1, Offset = 10, DataType = "REAL", DangerThreshold = _dangerThresholds[1] },
            new() { Name = "伺服 3", DbNumber = 1, Offset = 14, DataType = "REAL", DangerThreshold = _dangerThresholds[2] },
            new() { Name = "伺服 4", DbNumber = 1, Offset = 18, DataType = "REAL", DangerThreshold = _dangerThresholds[3] },
        ];
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
    public void UpdateServoValue(int index, double value)
    {
        if (index < 0 || index >= _gauges.Length) return;

        Dispatcher.InvokeAsync(() =>
        {
            _gauges[index].UpdateValue(value);
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
