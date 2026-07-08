using WpfScada.Services.Plc;

namespace WpfScada.Models.Plc;

/// <summary>
/// 伺服速度仪表的数据源配置。
/// 指定从哪个 DB 块的哪个偏移读取何种数据类型。
/// </summary>
public class ServoGaugeConfig
{
    /// <summary>显示名称（如 "伺服 1"）</summary>
    public string Name { get; init; } = "";

    /// <summary>PLC DB 号</summary>
    public int DbNumber { get; init; } = 1;

    /// <summary>字节偏移</summary>
    public int Offset { get; init; }

    /// <summary>数据类型：REAL / LREAL / INT / DINT / WORD / BYTE</summary>
    public string DataType { get; init; } = "REAL";

    /// <summary>危险速度阈值 (mm/s)</summary>
    public double DangerThreshold { get; init; } = 160;

    /// <summary>数据类型对应的字节长度（委托给 S7Service）</summary>
    public int DataSize => S7Service.GetDataTypeSize(DataType);
}
