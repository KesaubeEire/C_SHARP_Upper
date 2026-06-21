using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestWpf.Models;

/// <summary>
/// 快通道（I/Q/M）轮询配置
/// </summary>
public class FastPathConfig
{
    public bool EnableI { get; set; } = true;
    public bool EnableQ { get; set; } = true;
    public bool EnableM { get; set; } = true;

    /// <summary>逗号分隔的字节地址，如 "0,1,8"</summary>
    public string PollIAddr { get; set; } = "0,1";
    public string PollQAddr { get; set; } = "0";
    public string PollMAddr { get; set; } = "0,1";

    public int[] IAddresses => EnableI ? ParseAddrs(PollIAddr) : [];
    public int[] QAddresses => EnableQ ? ParseAddrs(PollQAddr) : [];
    public int[] MAddresses => EnableM ? ParseAddrs(PollMAddr) : [];

    private static int[] ParseAddrs(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split(',', '，', ';', '；')
            .Select(s => s.Trim()).Where(s => int.TryParse(s, out _))
            .Select(int.Parse).Distinct().OrderBy(a => a).ToArray();
    }
}

/// <summary>
/// 单个 DB 块轮询配置
/// </summary>
public class DbPollItem : INotifyPropertyChanged
{
    private bool _enabled = true;
    private int _dbNumber = 1;
    private int _offset;
    private int _length = 100;
    private string _status = "待读取";

    public bool Enabled { get => _enabled; set { _enabled = value; OnChanged(); } }
    public int DbNumber { get => _dbNumber; set { _dbNumber = value; OnChanged(); } }
    public int Offset { get => _offset; set { _offset = value; OnChanged(); } }
    public int Length { get => _length; set { _length = value; OnChanged(); } }
    public string Status { get => _status; set { _status = value; OnChanged(); } }

    public string Label => $"DB{_dbNumber}[{_offset}..{_offset + _length - 1}]";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// 轮询调度器全局配置
/// </summary>
public class PollingConfig
{
    public FastPathConfig Fast { get; } = new();
    public List<DbPollItem> DbItems { get; } = [];
    public int FastInterval { get; set; } = 50;   // ms
    public string DbIp { get; set; } = "";
    public int DbRack { get; set; }
    public int DbSlot { get; set; }
}
