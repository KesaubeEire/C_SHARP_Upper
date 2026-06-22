using Sharp7;

namespace Wpf.Ui.Gallery.Services.Plc;

public sealed class S7Service : IDisposable
{
    private readonly S7Client _client = new();
    private readonly object _clientLock = new();

    public bool IsConnected => _client.Connected;
    public string? LastError { get; private set; }

    public int Connect(string localIp, string ip, int port, int rack, int slot)
    {
        lock (_clientLock)
        {
            if (_client.Connected)
                _client.Disconnect();

            int portVal = port;
            _client.SetParam(Sharp7.S7Consts.p_u16_LocalPort, ref portVal);
            int result = _client.ConnectTo(ip, rack, slot);

            if (result != 0)
                LastError = _client.ErrorText(result);
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
            LastError = null;
        }
    }

    public static int AreaI  => (int)S7Area.PE;
    public static int AreaQ  => (int)S7Area.PA;
    public static int AreaM  => (int)S7Area.MK;
    public static int AreaDB => (int)S7Area.DB;
    private static S7Area A(int v) => (S7Area)v;

    public byte? ReadByte(int area, int byteAddress, int dbNumber = 0)
    {
        lock (_clientLock)
        {
            byte[] buffer = new byte[1];
            int result = area == (int)S7Area.DB
                ? _client.DBRead(dbNumber, byteAddress, 1, buffer)
                : _client.ReadArea(A(area), 0, byteAddress, 1, S7WordLength.Byte, buffer);

            if (result != 0)
            {
                LastError = _client.ErrorText(result);
                return null;
            }
            return buffer[0];
        }
    }

    public Dictionary<int, byte> ReadBytes(int area, int[] byteAddresses, int dbNumber = 0)
    {
        var result = new Dictionary<int, byte>();
        if (byteAddresses.Length == 0) return result;

        var sorted = byteAddresses.Distinct().OrderBy(a => a).ToArray();
        var groups = new List<(int Start, int Count)>();
        int gs = sorted[0], ge = sorted[0];
        for (int i = 1; i < sorted.Length; i++)
        {
            if (sorted[i] == ge + 1) { ge = sorted[i]; }
            else { groups.Add((gs, ge - gs + 1)); gs = sorted[i]; ge = sorted[i]; }
        }
        groups.Add((gs, ge - gs + 1));

        lock (_clientLock)
        {
            foreach (var (start, count) in groups)
            {
                byte[] buffer = new byte[count];
                int ret = area == (int)S7Area.DB
                    ? _client.DBRead(dbNumber, start, count, buffer)
                    : _client.ReadArea(A(area), 0, start, count, S7WordLength.Byte, buffer);

                if (ret != 0)
                {
                    LastError = _client.ErrorText(ret);
                    for (int i = 0; i < count; i++) result[start + i] = 0;
                    continue;
                }
                for (int i = 0; i < count; i++) result[start + i] = buffer[i];
            }
        }
        return result;
    }

    public byte[]? ReadBytesRaw(int area, int start, int count, int dbNumber = 0)
    {
        lock (_clientLock)
        {
            byte[] buffer = new byte[count];
            int ret = area == (int)S7Area.DB
                ? _client.DBRead(dbNumber, start, count, buffer)
                : _client.ReadArea(A(area), 0, start, count, S7WordLength.Byte, buffer);

            if (ret != 0)
            {
                LastError = _client.ErrorText(ret);
                return null;
            }
            return buffer;
        }
    }

    public bool WriteByte(int area, int byteAddress, byte value, int dbNumber = 0)
    {
        lock (_clientLock)
        {
            byte[] buffer = [value];
            int result = area == (int)S7Area.DB
                ? _client.DBWrite(dbNumber, byteAddress, 1, buffer)
                : _client.WriteArea(A(area), 0, byteAddress, 1, S7WordLength.Byte, buffer);

            if (result != 0)
            {
                LastError = _client.ErrorText(result);
                return false;
            }
            return true;
        }
    }

    public void Dispose() { lock (_clientLock) Disconnect(); }
}
