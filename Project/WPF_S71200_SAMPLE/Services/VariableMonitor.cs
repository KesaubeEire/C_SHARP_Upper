using System.Timers;
using Sharp7;
using Timer = System.Timers.Timer;

namespace TestWpf.Services;

/// <summary>
/// DB 变量监控器 — 按固定间隔读取 PLC DB 块中的某个类型化变量，
/// 解码为 double 后通过事件抛出，供 TrendPanel / GaugePanel 消费。
///
/// 用法：
///   var vm = new VariableMonitor(s7Service);
///   vm.DbNumber = 1;
///   vm.Offset = 6;         // DB1.DBD6 → REAL
///   vm.DataType = "REAL";
///   vm.SampleGenerated += (key, val, ts) => trendPanel.FeedData(key, val, ts);
///   vm.Start();
/// </summary>
public sealed class VariableMonitor : IDisposable
{
    private readonly S7Service _s7;
    private Timer? _timer;
    private bool _busy;

    /// <summary>DB 编号，例如 1 代表 DB1</summary>
    public int DbNumber { get; set; } = 1;

    /// <summary>字节偏移地址（字节索引，不是位索引）</summary>
    public int Offset { get; set; } = 6;

    /// <summary>
    /// 数据类型标识，决定了从 PLC 读取的字节数以及解码方式。
    /// 支持: REAL(4字节), INT(2), DINT(4), WORD(2), BYTE(1)
    /// </summary>
    public string DataType { get; set; } = "REAL";

    /// <summary>轮询间隔（毫秒），默认 100ms</summary>
    public int IntervalMs { get; set; } = 100;

    /// <summary>通道标识键，传给事件接收方用于区分数据源</summary>
    public string Key { get; set; } = "db_monitor";

    /// <summary>通道显示名称（仅标记用途）</summary>
    public string Label { get; set; } = "DB Monitor";

    /// <summary>是否正在运行</summary>
    public bool IsRunning { get; private set; }

    /// <summary>最近一次读到的原始值（double）</summary>
    public double? LastValue { get; private set; }

    /// <summary>最近一次错误信息</summary>
    public string? LastError { get; private set; }

    /// <summary>数据到达事件，参数：(key, value, timestamp)</summary>
    public event Action<string, double, DateTime>? SampleGenerated;

    public VariableMonitor(S7Service s7)
    {
        _s7 = s7 ?? throw new ArgumentNullException(nameof(s7));
    }

    /// <summary>启动轮询</summary>
    public void Start()
    {
        Stop();
        IsRunning = true;
        _busy = false;
        _timer = new Timer(IntervalMs);
        _timer.Elapsed += OnTick;
        _timer.AutoReset = false; // 手动重启，防重叠
        _timer.Start();
    }

    /// <summary>停止轮询</summary>
    public void Stop()
    {
        IsRunning = false;
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Elapsed -= OnTick;
            _timer.Dispose();
            _timer = null;
        }
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        if (!IsRunning || _busy) return;
        if (!_s7.IsConnected) { LastError = "PLC 未连接"; return; }

        _busy = true;
        try
        {
            // 根据 DataType 确定字节数
            int byteCount = DataType.ToUpperInvariant() switch
            {
                "REAL" => 4,
                "INT" => 2,
                "DINT" => 4,
                "WORD" => 2,
                "BYTE" => 1,
                _ => 4, // 默认 REAL
            };

            // 读取连续 byteCount 个字节（REAL=4, INT=2, DINT=4, ...）
            // 必须传全部偏移地址，否则 ReadBytes 只读起始一个字节
            var offsets = new int[byteCount];
            for (int i = 0; i < byteCount; i++) offsets[i] = Offset + i;
            var data = _s7.ReadBytes(S7Service.AreaDB, offsets, DbNumber);
            if (data.Count == 0)
            {
                LastError = _s7.LastError ?? "读取返回空";
                return;
            }

            // 构建连续 byte[] 供 Sharp7 解码
            var buf = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
                buf[i] = data.GetValueOrDefault(Offset + i);

            // 解码为 double
            double val = Decode(buf, DataType);
            LastValue = val;
            LastError = null;

            SampleGenerated?.Invoke(Key, val, DateTime.Now);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            _busy = false;
            if (IsRunning && _timer != null)
            {
                try
                {
                    _timer.Interval = Math.Max(20, IntervalMs);
                    _timer.Start();
                }
                catch { /* Dispose 竞争 */ }
            }
        }
    }

    /// <summary>
    /// 将原始字节数组解码为 double，根据数据类型选择不同的 Sharp7 解码方法。
    ///
    /// REAL  → S7.GetRealAt(4字节, IEEE754浮点数)
    /// INT   → S7.GetIntAt(2字节, 有符号16位)
    /// DINT  → S7.GetDIntAt(4字节, 有符号32位)
    /// WORD  → S7.GetWordAt(2字节, 无符号16位)
    /// BYTE  → 直接返回 buf[0]
    /// </summary>
    private static double Decode(byte[] buf, string dataType)
    {
        return dataType.ToUpperInvariant() switch
        {
            "REAL" => S7.GetRealAt(buf, 0),
            "INT" => S7.GetIntAt(buf, 0),
            "DINT" => S7.GetDIntAt(buf, 0),
            "WORD" => S7.GetWordAt(buf, 0),
            "BYTE" => buf[0],
            _ => S7.GetRealAt(buf, 0),
        };
    }

    public void Dispose() => Stop();
}
