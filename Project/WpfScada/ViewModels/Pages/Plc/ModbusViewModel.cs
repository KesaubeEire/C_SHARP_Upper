using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using WpfScada.Services.Plc.Modbus;

namespace WpfScada.ViewModels.Pages.Plc;

public partial class ModbusViewModel : ViewModel
{
    private readonly ModbusTransport _transport;
    private readonly ModbusPollingService _polling;
    private readonly IContentDialogService _contentDialog;

    // ==================== Connection ====================

    [ObservableProperty] private bool _isTcpMode = true;
    [ObservableProperty] private string _serialPortName = "COM1";
    [ObservableProperty] private int _baudRate = 9600;
    [ObservableProperty] private string _parityStr = "None";
    [ObservableProperty] private string _stopBitsStr = "One";
    [ObservableProperty] private string _tcpIp = "127.0.0.1";
    [ObservableProperty] private int _tcpPort = 502;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionStatus = "未连接";

    // ==================== Request ====================

    [ObservableProperty] private byte _deviceAddr = 1;
    [ObservableProperty] private byte _funcCode = 0x03;
    [ObservableProperty] private ushort _startAddr;
    [ObservableProperty] private ushort _readCount = 10;
    [ObservableProperty] private string _hexSendFrame = "";
    [ObservableProperty] private string _statusText = "就绪";

    // ==================== Response ====================

    [ObservableProperty] private string _hexResponseFrame = "";
    [ObservableProperty] private string _responseRawText = "";
    [ObservableProperty] private ObservableCollection<ModbusRegisterDisplay> _registers = [];

    // ==================== Polling ====================

    [ObservableProperty] private bool _isPolling;
    [ObservableProperty] private int _pollIntervalMs = 1000;
    [ObservableProperty] private string _pollStatsText = "";

    // COM port list
    public ObservableCollection<string> ComPorts { get; } = [.. ModbusTransport.GetPortNames()];
    public int[] BaudRates => [9600, 19200, 38400, 57600, 115200];
    public string[] ParityOptions => ["None", "Odd", "Even"];
    public string[] StopBitsOptions => ["One", "Two", "OnePointFive"];

    public ModbusViewModel(IContentDialogService contentDialog)
    {
        _contentDialog = contentDialog;
        _transport = new ModbusTransport();
        _polling = new ModbusPollingService(_transport, () => IsTcpMode);

        _transport.FrameReceived += OnFrameReceived;
        _transport.ConnectionChanged += OnConnectionChanged;
        _transport.ErrorOccurred += msg => StatusText = $"错误: {msg}";
        _polling.DataReceived += OnPollData;
        _polling.ServiceStateChanged += running => IsPolling = running;
    }

    // ==================== Connect / Disconnect ====================

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            if (IsTcpMode)
            {
                await Task.Run(() => _transport.ConnectTcp(TcpIp, TcpPort));
            }
            else
            {
                var parity = ParityStr switch { "Odd" => Parity.Odd, "Even" => Parity.Even, _ => Parity.None };
                var stopBits = StopBitsStr switch { "Two" => StopBits.Two, "OnePointFive" => StopBits.OnePointFive, _ => StopBits.One };
                await Task.Run(() => _transport.OpenSerial(SerialPortName, BaudRate, stopBits, parity));
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = $"连接失败: {ex.Message}";
            await _contentDialog.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "连接失败",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                });
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        if (IsPolling) StopPolling();
        if (IsTcpMode) _transport.DisconnectTcp();
        else _transport.CloseSerial();
    }

    private void OnConnectionChanged(bool connected, string msg)
    {
        IsConnected = connected;
        ConnectionStatus = msg;
        StatusText = msg;
    }

    // ==================== Send ====================

    [RelayCommand]
    private void SendRequest()
    {
        if (!IsConnected) { StatusText = "请先连接"; return; }

        try
        {
            var (frame, _) = _transport.SendReadRequest(DeviceAddr, FuncCode, StartAddr, ReadCount);
            HexSendFrame = BitConverter.ToString(frame).Replace("-", " ");
            StatusText = "已发送，等待响应...";
        }
        catch (Exception ex)
        {
            StatusText = $"发送失败: {ex.Message}";
        }
    }

    private void OnFrameReceived(byte[] rawFrame, bool isTcp)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            HexResponseFrame = BitConverter.ToString(rawFrame).Replace("-", " ");

            byte[] pduBuf = isTcp && rawFrame.Length > ModbusProtocol.MBAP_HEADER_SIZE
                ? rawFrame[ModbusProtocol.MBAP_HEADER_SIZE..]
                : rawFrame;

            var result = ModbusProtocol.ParseResponse(pduBuf);
            if (result.IsError)
            {
                StatusText = $"解析错误: {result.ErrorMessage}";
                return;
            }

            ResponseRawText = $"功能码: 0x{result.RawFuncCode:X2}  ";
            if (result.Registers.Count > 0)
            {
                ResponseRawText += $"{result.Registers.Count} 个寄存器";
                Registers = new ObservableCollection<ModbusRegisterDisplay>(
                    result.Registers.Select(r => new ModbusRegisterDisplay
                    {
                        Index = (ushort)(StartAddr + r.Index),
                        ValueDec = r.Value,
                        ValueHex = $"0x{r.Value:X4}",
                        ValueBin = Convert.ToString(r.Value, 2).PadLeft(16, '0'),
                        ValueOct = $"0{Convert.ToString(r.Value, 8)}",
                        ValueSigned = (short)r.Value,
                    }));
            }

            StatusText = $"收到 {rawFrame.Length} 字节响应";
        });
    }

    // ==================== Polling ====================

    [RelayCommand]
    private void StartPolling()
    {
        if (!IsConnected) { StatusText = "请先连接"; return; }

        _polling.ClearPollingConfigs();
        _polling.AddPollingConfig(new ModbusPollingConfig
        {
            DeviceAddr = DeviceAddr,
            FuncCode = FuncCode,
            StartAddr = StartAddr,
            Count = ReadCount,
            IntervalMs = PollIntervalMs,
            Tag = "轮询",
        });
        _polling.StartPolling();
        StatusText = "轮询已启动";
    }

    [RelayCommand]
    private void StopPolling()
    {
        _polling.StopPolling();
        _polling.Stop();
        StatusText = "轮询已停止";
    }

    private void OnPollData(ModbusPollingResult result)
    {
        if (!result.ParseResult.IsError && result.ParseResult.Registers.Count > 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Registers = new ObservableCollection<ModbusRegisterDisplay>(
                    result.ParseResult.Registers.Select(r => new ModbusRegisterDisplay
                    {
                        Index = (ushort)(result.Request.StartAddr + r.Index),
                        ValueDec = r.Value,
                        ValueHex = $"0x{r.Value:X4}",
                        ValueBin = Convert.ToString(r.Value, 2).PadLeft(16, '0'),
                        ValueOct = $"0{Convert.ToString(r.Value, 8)}",
                        ValueSigned = (short)r.Value,
                    }));
            });
        }
    }

    // ==================== Navigation ====================

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ComPorts.Clear();
        foreach (var p in ModbusTransport.GetPortNames())
            ComPorts.Add(p);
    }

    public override void OnNavigatedFrom()
    {
        base.OnNavigatedFrom();
        if (_polling.IsRunning)
            _polling.Stop();
    }
}

public class ModbusRegisterDisplay
{
    public ushort Index { get; set; }
    public ushort ValueDec { get; set; }
    public string ValueHex { get; set; } = "";
    public string ValueBin { get; set; } = "";
    public string ValueOct { get; set; } = "";
    public short ValueSigned { get; set; }
}
