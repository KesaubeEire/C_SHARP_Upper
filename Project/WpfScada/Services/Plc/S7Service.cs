using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Sharp7;

namespace WpfScada.Services.Plc;

public sealed class S7Service : IDisposable
{
    private readonly S7Client _client = new();
    private readonly object _clientLock = new();

    // 通过反射缓存 MsgSocket 和 TCPSocket 字段，避免反复查找
#pragma warning disable S3011 // Reflection to bypass accessibility is intentional — Sharp7 doesn't expose local bind API
    private static readonly FieldInfo? SocketField = typeof(S7Client)
        .GetField("Socket", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly Type? MsgSocketType = typeof(S7Client).Assembly.GetType("Sharp7.MsgSocket");
    private static readonly FieldInfo? TcpSocketField = MsgSocketType?.GetField("TCPSocket",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly MethodInfo? IdoConnectMethod = typeof(S7Client)
        .GetMethod("ISOConnect", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? NegotiatePduMethod = typeof(S7Client)
        .GetMethod("NegotiatePduLength", BindingFlags.Instance | BindingFlags.NonPublic);
#pragma warning restore S3011

    public bool IsConnected => _client.Connected;
    public string? LastError { get; private set; }

    public int Connect(string localIp, string ip, int port, int rack, int slot)
    {
        lock (_clientLock)
        {
            try
            {
                if (_client.Connected)
                    _client.Disconnect();

                int portVal = port;
                _client.SetParam(Sharp7.S7Consts.p_u16_LocalPort, ref portVal);

                if (!string.IsNullOrEmpty(localIp))
                    return ConnectWithLocalBind(localIp, ip, port, rack, slot);

                int result = _client.ConnectTo(ip, rack, slot);

                if (result != 0)
                    LastError = _client.ErrorText(result);
                else
                    LastError = null;

                return result;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return -1;
            }
        }
    }

    /// <summary>
    /// 带网卡绑定的连接流程：
    /// 1. 预连 TCP Socket（绑定到指定本机 IP）→ 注入 Sharp7
    /// 2. ConnectTo 设置 ISOTsap 等参数后因 Connected 短路返回
    /// 3. 手动调用 ISOConnect + NegotiatePduLength 完成协议栈初始化
    /// </summary>
    private int ConnectWithLocalBind(string localIp, string ip, int port, int rack, int slot)
    {
        PreBindSocket(localIp, ip, port);

        // ConnectTo 会设置 IPAddress、TSAP，然后调用 Connect()
        // 但由于 TCPSocket 已连接，Connect() 短路返回 0
        int result = _client.ConnectTo(ip, rack, slot);

        if (result == 0 && IdoConnectMethod is not null)
            result = InvokeProtocolStep(IdoConnectMethod, "ISO 握手失败");

        if (result == 0 && NegotiatePduMethod is not null)
            result = InvokeProtocolStep(NegotiatePduMethod, "PDU 协商失败");

        if (result != 0)
        {
            _client.Disconnect();
            LastError ??= _client.ErrorText(result);
            if (string.IsNullOrEmpty(LastError))
                LastError = $"S7 协议协商失败 (错误码 {result})";
        }
        else
        {
            LastError = null;
        }

        return result;
    }

    /// <summary>通过反射调用 Sharp7 内部协议方法，捕获异常并设置 LastError</summary>
    private int InvokeProtocolStep(MethodInfo method, string failPrefix)
    {
        try
        {
            return method.Invoke(_client, null) is int r ? r : -1;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            LastError = $"{failPrefix}: {ex.InnerException.Message}";
            return -1;
        }
        catch (Exception ex)
        {
            LastError = $"{failPrefix}: {ex.Message}";
            return -1;
        }
    }

    /// <summary>
    /// 创建绑定到指定本地 IP 的 TCP 连接，通过反射注入 Sharp7 内部的 MsgSocket，
    /// 使其跳过 CreateSocket 直接使用我们预连接的 Socket。
    /// 注意：此方法只完成 TCP 层连接，ISO + PDU 协商由上游 ConnectWithLocalBind 完成。
    /// </summary>
    private void PreBindSocket(string localIp, string remoteIp, int remotePort)
    {
        if (SocketField is null || TcpSocketField is null)
            return;

        var msgSocket = SocketField.GetValue(_client);
        if (msgSocket is null)
            return;

        // 创建并绑定到指定网卡的 IP
        var localEndpoint = new IPEndPoint(IPAddress.Parse(localIp), 0);
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        socket.Bind(localEndpoint);
        socket.Connect(remoteIp, remotePort);

        TcpSocketField.SetValue(msgSocket, socket);
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

    public static int AreaI => (int)S7Area.PE;
    public static int AreaQ => (int)S7Area.PA;
    public static int AreaM => (int)S7Area.MK;
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

    public bool WriteBytesRaw(int area, int start, byte[] data, int dbNumber = 0)
    {
        lock (_clientLock)
        {
            int result = area == (int)S7Area.DB
                ? _client.DBWrite(dbNumber, start, data.Length, data)
                : _client.WriteArea(A(area), 0, start, data.Length, S7WordLength.Byte, data);

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
