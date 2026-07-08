namespace WpfScada.Services.Plc;

public interface IPlcClient
{
    bool IsConnected { get; }
    string? LastError { get; }
    byte[]? ReadBytesRaw(int area, int start, int count, int dbNumber = 0);
    bool WriteByte(int area, int byteAddress, byte value, int dbNumber = 0);
}
