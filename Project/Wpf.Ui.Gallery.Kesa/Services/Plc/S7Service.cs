using Sharp7;

namespace Wpf.Ui.Gallery.Services.Plc;

public class S7Service : IDisposable
{
    private readonly S7Client _client = new();
    private readonly object _clientLock = new();

    public bool IsConnected
    {
        get { lock (_clientLock) return _client.Connected; }
    }

    public string? LastError { get; private set; }

    public int Connect(string localIp, string ip, int port, int rack, int slot)
    {
        lock (_clientLock)
        {
            _client.SetConnectionParams(ip, (ushort)rack, (ushort)slot);
            int result = _client.Connect();
            if (result != 0)
                LastError = "连接失败: " + result;
            else
                LastError = null;
            return result;
        }
    }

    public void Disconnect()
    {
        lock (_clientLock)
        {
            if (_client.Connected)
                _client.Disconnect();
        }
    }

    public byte? ReadByte(int area, int byteAddress, int dbNumber = 0)
    {
        lock (_clientLock)
        {
            var buf = new byte[1];
            int result = _client.ReadArea(area, dbNumber, byteAddress, 1, S7Consts.S7WLByte, buf);
            if (result == 0)
                return buf[0];
            LastError = "读取失败: " + result;
            return null;
        }
    }

    public Dictionary<int, byte> ReadBytes(int area, int[] addresses, int dbNumber = 0)
    {
        var result = new Dictionary<int, byte>();
        if (addresses.Length == 0) return result;

        lock (_clientLock)
        {
            var sorted = addresses.Distinct().OrderBy(a => a).ToArray();
            var segments = new List<(int start, int count)>();
            int segStart = sorted[0], segEnd = sorted[0];

            foreach (int addr in sorted.Skip(1))
            {
                if (addr == segEnd + 1) segEnd = addr;
                else { segments.Add((segStart, segEnd - segStart + 1)); segStart = segEnd = addr; }
            }
            segments.Add((segStart, segEnd - segStart + 1));

            foreach (var (start, count) in segments)
            {
                var buf = new byte[count];
                int ret = area == AreaDB
                    ? _client.DBRead(dbNumber, start, count, buf)
                    : _client.ReadArea(area, dbNumber, start, count, S7Consts.S7WLByte, buf);

                if (ret == 0)
                {
                    for (int i = 0; i < count; i++)
                        result[start + i] = buf[i];
                }
            }
        }
        return result;
    }

    public byte[]? ReadBytesRaw(int area, int start, int count, int dbNumber = 0)
    {
        lock (_clientLock)
        {
            var buf = new byte[count];
            int ret = area == AreaDB
                ? _client.DBRead(dbNumber, start, count, buf)
                : _client.ReadArea(area, dbNumber, start, count, S7Consts.S7WLByte, buf);
            if (ret == 0) return buf;
            LastError = "读取失败: " + ret;
            return null;
        }
    }

    public bool WriteByte(int area, int byteAddress, byte value, int dbNumber = 0)
    {
        lock (_clientLock)
        {
            var buf = new[] { value };
            int result = _client.WriteArea(area, dbNumber, byteAddress, 1, S7Consts.S7WLByte, buf);
            if (result != 0) LastError = "写入失败: " + result;
            return result == 0;
        }
    }

    public void Dispose()
    {
        lock (_clientLock)
        {
            if (_client.Connected)
                _client.Disconnect();
            // S7Client doesn't implement IDisposable in v1.1.84
        }
    }

    // S7 area constants
    public const int AreaPE = 0x81;
    public const int AreaPA = 0x82;
    public const int AreaMK = 0x83;
    public const int AreaDB = 0x84;
    public const int AreaCT = 0x1C;
    public const int AreaTM = 0x1D;

    // Alias for backward compatibility
    public const int AreaI = AreaPE;
    public const int AreaQ = AreaPA;
    public const int AreaM = AreaMK;
}
