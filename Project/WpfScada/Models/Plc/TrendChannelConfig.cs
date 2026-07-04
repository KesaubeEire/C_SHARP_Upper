namespace WpfScada.Models.Plc;

public class TrendChannelConfig
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public SkiaSharp.SKColor Color { get; set; } = SkiaSharp.SKColors.Cyan;
    public string ColorHex { get; set; } = "#00FFFF";
    public string Unit { get; set; } = "";
    public double Min { get; set; }
    public double Max { get; set; } = 100;
    public string? Variable { get; set; }
    public bool Enabled { get; set; } = true;
    public int DbNumber { get; set; }
    public int ByteOffset { get; set; }
    public string DataType { get; set; } = "REAL";
    public double CurrentValue { get; set; }
    public string CurrentValueText => $"{CurrentValue:F1}{Unit}";
}

public class TrendDataPoint
{
    public double NormalizedValue { get; set; }
    public double RawValue { get; set; }
    public DateTime Timestamp { get; set; }
}
