using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestWpf.Models;

/// <summary>
/// DB 变量展示行 — 带偏移格式化 + 值显示 + INotifyPropertyChanged
/// </summary>
public class DbVariableDisplay : INotifyPropertyChanged
{
    /// <summary>格式化偏移量（BOOL 显示 "X.Y"，非 BOOL 显示 "X"）</summary>
    public string OffsetDisplay { get; set; } = "";

    /// <summary>字节偏移（用于读取）</summary>
    public int ByteOffset { get; set; }

    /// <summary>位偏移（仅 BOOL 有效，0-7）</summary>
    public int BitOffset { get; set; } = -1;

    /// <summary>是否为 BOOL 类型（位变量）</summary>
    public bool IsBit => BitOffset >= 0;

    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public int Size { get; set; }

    /// <summary>读取到的数值（文本形式）</summary>
    private string _value = "";
    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    /// <summary>是否从 UDT 展开的子变量</summary>
    public bool IsFromUdt { get; set; }

    /// <summary>所属 UDT 名称（如果是 UDT 展开）</summary>
    public string? UdtName { get; set; }

    /// <summary>初始值（来自文件）</summary>
    public string? InitialValue { get; set; }

    /// <summary>注释</summary>
    public string? Comment { get; set; }

    /// <summary>EndOffset 辅助属性（用于 UI 显示范围）</summary>
    public int EndByteOffset => ByteOffset + Size - 1;

    // ===== INotifyPropertyChanged =====

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public override string ToString()
        => $"{OffsetDisplay,4} | {Name,-20} {DataType,-12} {Size}B = {Value}";
}
