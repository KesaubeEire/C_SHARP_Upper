namespace WpfScada.Services.Plc.Modbus;

public record ModbusRequest
{
    public byte DeviceAddr { get; init; }
    public byte FuncCode { get; init; }
    public ushort StartAddr { get; init; }
    public ushort Count { get; init; }
    public string Tag { get; init; } = "";
}
