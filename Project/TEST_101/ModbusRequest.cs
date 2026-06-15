namespace TEST_101;

/// <summary>
/// Modbus 请求模型 — 描述一次读操作的所有参数。
/// 由生产者（定时轮询器或用户按钮）创建，入队后由消费者线程处理。
/// </summary>
public class ModbusRequest
{
    public byte DeviceAddr { get; }
    public byte FuncCode { get; }
    public ushort StartAddr { get; }
    public ushort Count { get; }
    /// <summary>显示用标签，如 "温度表_1号"、"PLC1_压力"</summary>
    public string Tag { get; }

    public ModbusRequest(byte deviceAddr, byte funcCode, ushort startAddr, ushort count, string? tag = null)
    {
        DeviceAddr = deviceAddr;
        FuncCode = funcCode;
        StartAddr = startAddr;
        Count = count;
        Tag = tag ?? $"Dev#{deviceAddr} F{funcCode:X2} @{startAddr}";
    }

    public override string ToString() => Tag;
}
