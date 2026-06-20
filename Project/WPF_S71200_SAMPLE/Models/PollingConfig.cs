using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestWpf.Models;

/// <summary>
/// 快通道（I/Q/M）轮询配置
/// </summary>
public class FastPathConfig : INotifyPropertyChanged
{
    private bool _enableI = true;
    private int _iStart;
    private int _iEnd = 2;
    private bool _enableQ = true;
    private int _qStart;
    private int _qEnd = 1;
    private bool _enableM = true;
    private int _mStart;
    private int _mEnd = 10;

    public bool EnableI { get => _enableI; set { _enableI = value; OnChanged(); } }
    public int IStart { get => _iStart; set { _iStart = value; OnChanged(); } }
    public int IEnd { get => _iEnd; set { _iEnd = value; OnChanged(); } }
    public bool EnableQ { get => _enableQ; set { _enableQ = value; OnChanged(); } }
    public int QStart { get => _qStart; set { _qStart = value; OnChanged(); } }
    public int QEnd { get => _qEnd; set { _qEnd = value; OnChanged(); } }
    public bool EnableM { get => _enableM; set { _enableM = value; OnChanged(); } }
    public int MStart { get => _mStart; set { _mStart = value; OnChanged(); } }
    public int MEnd { get => _mEnd; set { _mEnd = value; OnChanged(); } }

    /// <summary>生成 I 区地址数组</summary>
    public int[] IAddresses => _enableI ? Range(_iStart, _iEnd) : [];
    public int[] QAddresses => _enableQ ? Range(_qStart, _qEnd) : [];
    public int[] MAddresses => _enableM ? Range(_mStart, _mEnd) : [];

    private static int[] Range(int start, int end)
    {
        if (start > end) return [];
        int len = end - start + 1;
        return len > 200 ? Enumerable.Range(start, 200).ToArray() : Enumerable.Range(start, len).ToArray();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
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
    public int DbInterval { get; set; } = 50;     // ms — 分片轮转，每个 tick 读 1~2 个
}
