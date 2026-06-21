namespace TestWpf.Models;

/// <summary>
/// 趋势图通道配置（对标 Trioop TrendChannel）
/// </summary>
public class TrendChannelConfig
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Color { get; set; } = "#3498DB";
    public string Unit { get; set; } = "";
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public string Variable { get; set; } = "";   // DB块:变量名
    public bool Enabled { get; set; } = true;
    public int DbNumber { get; set; }
    public int ByteOffset { get; set; }
    public string DataType { get; set; } = "real"; // real / int / dint / word / bool
}

/// <summary>
/// 水平轴配置
/// </summary>
public class HorizontalAxisConfig
{
    public double LeftValue { get; set; } = 0;
    public double RightValue { get; set; } = 100;
    public int TickCount { get; set; } = 10;
    public double CurrentPosition { get; set; } = 45;
    public string Label { get; set; } = "伺服位置";
    public string Unit { get; set; } = "mm";
}
