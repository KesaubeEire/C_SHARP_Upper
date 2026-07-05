using System.IO.Ports;
using System.Net.Sockets;

namespace WpfScada.Services.Plc.Modbus;

public sealed class ModbusTransport : IDisposable
{
    private SerialPort? _serialPort;
    private TcpClient? _tcpClient;
    private NetworkStream? _tcpStream;
    private ushort _tcpTransactionId = 1;
    private bool _tcpConnecting;
    private bool _disposed;

    private TaskCompletionSource<byte[]>? _syncReadTcs;
    private readonly object _syncLock = new();
    private CancellationTokenSource? _tcpCts;

    public bool IsSerialOpen => _serialPort is { IsOpen: true };
    public bool IsTcpConnected => _tcpClient?.Connected == true;

    public event Action<byte[], bool>? FrameReceived;
    public event Action<string>? ErrorOccurred;
    public event Action<bool, string>? ConnectionChanged;

    public static string[] GetPortNames() => SerialPort.GetPortNames();

    public void OpenSerial(string portName, int baudRate, StopBits stopBits, Parity parity)
    {
        CloseSerial();

        _serialPort = new SerialPort(portName, baudRate, parity, 8, stopBits)
        {
            ReadTimeout = 2000,
            WriteTimeout = 500,
        };
        _serialPort.DataReceived += OnSerialDataReceived;
        _serialPort.Open();
        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();

        ConnectionChanged?.Invoke(true, $"已打开 {portName}，{baudRate} 波特率");
    }

    public void CloseSerial()
    {
        try
        {
            if (_serialPort is { IsOpen: true })
            {
                _serialPort.DataReceived -= OnSerialDataReceived;
                _serialPort.Close();
            }
        }
        catch { }
        finally
        {
            _serialPort?.Dispose();
            _serialPort = null;
        }

        ConnectionChanged?.Invoke(false, "已断开");
    }

    public void ConnectTcp(string ip, int port)
    {
        if (_tcpConnecting) return;
        _tcpConnecting = true;

        if (_tcpClient != null)
            DisconnectTcp();

        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.ReceiveTimeout = 3000;
            _tcpClient.SendTimeout = 1000;
            _tcpClient.Connect(ip, port);
            _tcpStream = _tcpClient.GetStream();
            _tcpTransactionId = 1;

            ConnectionChanged?.Invoke(true, $"已连接 {ip}:{port}");

            _tcpCts = new CancellationTokenSource();
            _ = TcpReceiveLoopAsync(_tcpCts.Token);
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or InvalidOperationException)
        {
            ConnectionChanged?.Invoke(false, "TCP 连接失败：" + ex.Message);
            CleanupTcp();
        }
        catch
        {
            ConnectionChanged?.Invoke(false, "TCP 连接失败：未知错误");
            CleanupTcp();
        }
        finally
        {
            _tcpConnecting = false;
        }
    }

    public void DisconnectTcp()
    {
        _tcpCts?.Cancel();
        CleanupTcp();
        ConnectionChanged?.Invoke(false, "已断开");
    }

    private void CleanupTcp()
    {
        try { _tcpStream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }
        _tcpStream = null;
        _tcpClient = null;
        _tcpCts?.Dispose();
        _tcpCts = null;
    }

    public (byte[] frame, byte funcCode) SendReadRequest(byte devAddr, byte funcCode, ushort startAddr, ushort count)
    {
        byte[] pdu = ModbusProtocol.BuildReadPDU(devAddr, funcCode, startAddr, count);

        if (IsTcpConnected)
        {
            byte[] frame = ModbusProtocol.BuildTCPFrame(pdu, devAddr, _tcpTransactionId++);
            _tcpStream!.Write(frame, 0, frame.Length);
            return (frame, funcCode);
        }
        else if (IsSerialOpen)
        {
            byte[] frame = ModbusProtocol.BuildRTUFrame(pdu);
            _serialPort!.DiscardInBuffer();
            _serialPort!.Write(frame, 0, frame.Length);
            return (frame, funcCode);
        }

        throw new InvalidOperationException("未连接串口或 TCP");
    }

    public async Task<byte[]> SendAndReadAsync(byte devAddr, byte funcCode, ushort startAddr, ushort count, int timeoutMs = 2000)
    {
        var tcs = new TaskCompletionSource<byte[]>();
        lock (_syncLock) { _syncReadTcs = tcs; }

        try
        {
            SendReadRequest(devAddr, funcCode, startAddr, count);

            var timeoutTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);

            if (completed == timeoutTask)
                throw new TimeoutException(
                    $"Modbus 响应超时 (dev={devAddr}, func=0x{funcCode:X2}, addr={startAddr}, timeout={timeoutMs}ms)");

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_syncLock) { _syncReadTcs = null; }
        }
    }

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var sp = _serialPort;
        if (sp == null || !sp.IsOpen) return;

        Thread.Sleep(50);
        int bytesToRead = sp.BytesToRead;
        if (bytesToRead <= 0) return;

        byte[] buffer = new byte[bytesToRead];
        sp.Read(buffer, 0, bytesToRead);

        if (buffer.Length >= 4 && !ModbusProtocol.VerifyCRC(buffer))
        {
            ErrorOccurred?.Invoke("CRC 校验失败，数据可能损坏");
            return;
        }

        HandleReceivedFrame(buffer, false);
    }

    private async Task TcpReceiveLoopAsync(CancellationToken ct)
    {
        byte[] headerBuf = new byte[ModbusProtocol.MBAP_HEADER_SIZE];

        while (!ct.IsCancellationRequested && _tcpClient?.Connected == true)
        {
            try
            {
                int read = 0;
                while (read < ModbusProtocol.MBAP_HEADER_SIZE)
                {
                    int n = await _tcpStream!.ReadAsync(headerBuf.AsMemory(read, ModbusProtocol.MBAP_HEADER_SIZE - read), ct);
                    if (n == 0) throw new IOException("连接已断开");
                    read += n;
                }

                int length = (headerBuf[4] << 8) | headerBuf[5];
                byte[] pduBuf = new byte[length];
                read = 0;
                while (read < length)
                {
                    int n = await _tcpStream!.ReadAsync(pduBuf.AsMemory(read, length - read), ct);
                    if (n == 0) throw new IOException("连接已断开");
                    read += n;
                }

                byte[] fullFrame = new byte[ModbusProtocol.MBAP_HEADER_SIZE + length];
                Array.Copy(headerBuf, 0, fullFrame, 0, ModbusProtocol.MBAP_HEADER_SIZE);
                Array.Copy(pduBuf, 0, fullFrame, ModbusProtocol.MBAP_HEADER_SIZE, length);

                HandleReceivedFrame(fullFrame, true);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException)
            {
                ConnectionChanged?.Invoke(false, "TCP 连接断开");
                DisconnectTcp();
                break;
            }
            catch (ObjectDisposedException) { break; }
        }
    }

    private void HandleReceivedFrame(byte[] frame, bool isTcp)
    {
        TaskCompletionSource<byte[]>? tcs;
        lock (_syncLock) { tcs = _syncReadTcs; }

        if (tcs != null)
            tcs.TrySetResult(frame);
        else
            FrameReceived?.Invoke(frame, isTcp);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisconnectTcp();
        CloseSerial();
    }
}
