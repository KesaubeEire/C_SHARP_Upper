using System;
using System.Drawing;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TEST_101
{
    /// <summary>
    /// Modbus 通信层：管理 SerialPort / TcpClient，通过事件通知 UI。
    /// 内部处理 Invoke 封送，所有事件回调都在 UI 线程上执行。
    /// </summary>
    public class ModbusTransport : IDisposable
    {
        private readonly Control _uiControl; // 用于 Invoke 的 UI 控件
        private readonly Func<bool> _isTcpMode; // 回调：当前是否为 TCP 模式

        // SerialPort
        private SerialPort _sp = new SerialPort();

        // TcpClient
        private TcpClient _tcpClient;
        private NetworkStream _tcpStream;
        private ushort _tcpTransactionId = 1;
        private bool _tcpConnecting;

        // 状态
        private bool _disposed;

        // ========== 事件（均在 UI 线程触发）==========

        /// <summary>收到完整的 Modbus 响应帧（原始字节）</summary>
        public event Action<byte[], bool>? FrameReceived;

        /// <summary>通信出错（如断连）</summary>
        public event Action<string>? ErrorOccurred;

        /// <summary>连接状态变化（connected=true 表示已连接）</summary>
        public event Action<bool, string>? ConnectionChanged;

        // ========== 构造函数 ==========

        public ModbusTransport(Control uiControl, Func<bool> isTcpModeCallback)
        {
            _uiControl = uiControl ?? throw new ArgumentNullException(nameof(uiControl));
            _isTcpMode = isTcpModeCallback ?? throw new ArgumentNullException(nameof(isTcpModeCallback));
        }

        // ========== 属性 ==========

        public bool IsSerialOpen => _sp.IsOpen;
        public bool IsTcpConnected => _tcpClient != null && _tcpClient.Connected;

        public string[] GetPortNames() => SerialPort.GetPortNames();

        // ========== 串口操作 ==========

        public void OpenSerial(string portName, int baudRate, StopBits stopBits, Parity parity)
        {
            if (_sp.IsOpen)
                CloseSerial();

            _sp.PortName = portName;
            _sp.BaudRate = baudRate;
            _sp.DataBits = 8;
            _sp.StopBits = stopBits;
            _sp.Parity = parity;
            _sp.ReadTimeout = 2000;
            _sp.WriteTimeout = 500;
            _sp.DataReceived += Sp_DataReceived;

            _sp.Open();
            _sp.DiscardInBuffer();
            _sp.DiscardOutBuffer();

            SafeInvoke(() => ConnectionChanged?.Invoke(true, $"已打开 {portName}，{baudRate} 波特率"));
        }

        public void CloseSerial()
        {
            try
            {
                if (_sp.IsOpen)
                {
                    _sp.DataReceived -= Sp_DataReceived;
                    _sp.Close();
                    _sp.Dispose();
                }
            }
            catch { }
            finally
            {
                _sp = new SerialPort();
            }

            SafeInvoke(() => ConnectionChanged?.Invoke(false, "已断开"));
        }

        // ========== TCP 操作 ==========

        public void ConnectTcp(string ip, int port)
        {
            if (_tcpConnecting) return;
            _tcpConnecting = true;

            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = 3000;
                _tcpClient.SendTimeout = 1000;
                _tcpClient.Connect(ip, port);
                _tcpStream = _tcpClient.GetStream();

                SafeInvoke(() => ConnectionChanged?.Invoke(true, $"已连接 {ip}:{port}"));

                Task.Run(TcpReceiveLoop);
            }
            catch (Exception ex)
            {
                SafeInvoke(() => ErrorOccurred?.Invoke("TCP 连接失败：" + ex.Message));
                try { _tcpClient?.Close(); } catch { }
                _tcpClient = null;
            }
            finally
            {
                _tcpConnecting = false;
            }
        }

        public void DisconnectTcp()
        {
            // 先关 Stream 可中断阻塞中的 Read()，再关 Client 释放 socket
            try { _tcpStream?.Close(); } catch { }
            try { _tcpClient?.Close(); } catch { }

            _tcpStream = null;
            _tcpClient = null;

            SafeInvoke(() => ConnectionChanged?.Invoke(false, "已断开"));
        }

        // ========== 发送 ==========

        /// <summary>发送 Modbus 读请求。返回发送的帧（十六进制字符串，用于 UI 着色显示）。</summary>
        public (byte[] frame, byte funcCode) SendReadRequest(byte devAddr, byte funcCode, ushort startAddr, ushort count)
        {
            byte[] pdu = ModbusProtocol.BuildReadPDU(devAddr, funcCode, startAddr, count);

            byte[] frame;
            if (_isTcpMode())
            {
                frame = ModbusProtocol.BuildTCPFrame(pdu, devAddr, _tcpTransactionId++);

                if (_tcpStream == null)
                    throw new InvalidOperationException("TCP 未连接");

                _tcpStream.Write(frame, 0, frame.Length);
            }
            else
            {
                frame = ModbusProtocol.BuildRTUFrame(pdu);

                if (!_sp.IsOpen)
                    throw new InvalidOperationException("串口未打开");

                _sp.DiscardInBuffer();
                _sp.Write(frame, 0, frame.Length);
            }

            return (frame, funcCode);
        }

        // ========== RTU 接收 ==========

        private void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // 短暂等待缓冲区收完所有字节
            System.Threading.Thread.Sleep(50);
            int bytesToRead = _sp.BytesToRead;
            if (bytesToRead <= 0) return;

            byte[] buffer = new byte[bytesToRead];
            _sp.Read(buffer, 0, bytesToRead);

            // CRC 校验
            if (buffer.Length >= 4 && !ModbusProtocol.VerifyCRC(buffer))
            {
                SafeInvoke(() =>
                    ErrorOccurred?.Invoke("⚠ CRC 校验失败，数据可能损坏"));
                return;
            }

            SafeInvoke(() => FrameReceived?.Invoke(buffer, false));
        }

        // ========== TCP 接收 ==========

        private void TcpReceiveLoop()
        {
            byte[] headerBuf = new byte[ModbusProtocol.MBAP_HEADER_SIZE];

            while (_tcpClient != null && _tcpClient.Connected)
            {
                try
                {
                    // 读 MBAP 头
                    int read = 0;
                    while (read < ModbusProtocol.MBAP_HEADER_SIZE)
                    {
                        int n = _tcpStream!.Read(headerBuf, read,
                            ModbusProtocol.MBAP_HEADER_SIZE - read);
                        if (n == 0) throw new Exception("连接已断开");
                        read += n;
                    }

                    // 解析 Length 字段
                    int length = (headerBuf[4] << 8) | headerBuf[5];

                    // 读 PDU 部分
                    byte[] pduBuf = new byte[length];
                    read = 0;
                    while (read < length)
                    {
                        int n = _tcpStream!.Read(pduBuf, read, length - read);
                        if (n == 0) throw new Exception("连接已断开");
                        read += n;
                    }

                    // 组装完整帧（用于日志显示）
                    byte[] fullFrame = new byte[ModbusProtocol.MBAP_HEADER_SIZE + length];
                    Array.Copy(headerBuf, 0, fullFrame, 0, ModbusProtocol.MBAP_HEADER_SIZE);
                    Array.Copy(pduBuf, 0, fullFrame, ModbusProtocol.MBAP_HEADER_SIZE, length);

                    SafeInvoke(() => FrameReceived?.Invoke(fullFrame, true));
                }
                catch (Exception)
                {
                    SafeInvoke(() =>
                    {
                        ConnectionChanged?.Invoke(false, "TCP 连接断开");
                        DisconnectTcp();
                    });
                    break;
                }
            }
        }

        // ========== 线程安全 Invoke ==========

        private void SafeInvoke(Action action)
        {
            if (_disposed) return;
            if (_uiControl.IsDisposed) return;

            if (_uiControl.InvokeRequired)
                _uiControl.Invoke(action);
            else
                action();
        }

        // ========== 释放 ==========

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            DisconnectTcp();
            CloseSerial();
        }
    }
}
