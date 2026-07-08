using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfScada.Models.Plc;

public class FastPathConfig
{
    public bool EnableI { get; set; } = true;
    public bool EnableQ { get; set; } = true;
    public bool EnableM { get; set; } = true;
    public string PollIAddr { get; set; } = "";
    public string PollQAddr { get; set; } = "";
    public string PollMAddr { get; set; } = "";

    public int[] ResolveAddr(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return [];
        return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => int.TryParse(x, out _))
                .Select(int.Parse)
                .ToArray();
    }
}

public class DbPollItem : INotifyPropertyChanged
{
    private bool _enabled = true;
    private string _status = "等待";
    private string? _label;
    private string _dataType = "BYTE";

    public int DbNumber { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }

    /// <summary>
    /// PLC 数据类型。决定从 DB 读取的字节数和解码方式。
    /// 支持: BYTE(1), WORD(2), INT(2), DINT(4), REAL(4) — 默认 BYTE。
    /// 设为 BYTE 时只做单字节存储（原行为），其他类型自动解码为 double。
    /// </summary>
    public string DataType
    {
        get => _dataType;
        set { _dataType = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveLength)); }
    }

    /// <summary>
    /// 根据 DataType 自动推导的字节长度。若不由 DataType 决定则回退到 Length。
    /// </summary>
    public int EffectiveLength => DataTypeByteCount > 0 ? DataTypeByteCount : Math.Max(Length, 1);

    public bool Enabled { get => _enabled; set { _enabled = value; OnPropertyChanged(); } }
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string? Label { get => _label; set { _label = value; OnPropertyChanged(); } }

    /// <summary>根据 DataType 返回需要的字节数（0 = 不由 DataType 决定）。</summary>
    internal int DataTypeByteCount => DataType.ToUpperInvariant() switch
    {
        "BYTE" => 0,          // 0 = 使用 Length
        "WORD" or "INT" => 2,
        "DINT" or "REAL" => 4,
        "LREAL" => 8,                 // LReal = 64-bit 双精度浮点
        _ => 0,
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class PollingConfig
{
    public FastPathConfig Fast { get; set; } = new();
    public List<DbPollItem> DbItems { get; set; } = [];
    public int FastInterval { get; set; } = 500;
}
