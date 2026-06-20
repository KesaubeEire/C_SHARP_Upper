using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestWpf.Models;

/// <summary>
/// 一行（一个字节）的视图模型
/// </summary>
public class ByteRowViewModel : INotifyPropertyChanged
{
    private byte _value;
    private bool _hasChanges;

    public int ByteAddress { get; }
    public string AreaLabel { get; }
    public string Label => $"{AreaLabel}B{ByteAddress}";
    public bool IsReadOnly { get; }
    public List<BitViewModel> Bits { get; }

    public byte Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged();
                HexText = $"0x{value:X2}";
                OnPropertyChanged(nameof(HexText));
                for (int i = 0; i < 8; i++)
                    Bits[i].IsSet = ((value >> i) & 1) == 1;
            }
        }
    }

    public string HexText { get; private set; } = "0x00";
    public bool HasChanges
    {
        get => _hasChanges;
        set { _hasChanges = value; OnPropertyChanged(); }
    }

    public ByteRowViewModel(int byteAddress, string areaLabel, bool isReadOnly)
    {
        ByteAddress = byteAddress;
        AreaLabel = areaLabel;
        IsReadOnly = isReadOnly;
        Bits = Enumerable.Range(0, 8)
            .Select(i => new BitViewModel(i, isReadOnly))
            .ToList();
        foreach (var bit in Bits)
            bit.Parent = this;
    }

    public void NotifyBitChanged()
    {
        HasChanges = true;
        // 重新计算字节值
        byte newVal = 0;
        for (int i = 0; i < 8; i++)
            if (Bits[i].IsSet)
                newVal |= (byte)(1 << i);
        _value = newVal;
        HexText = $"0x{newVal:X2}";
        OnPropertyChanged(nameof(HexText));
        OnPropertyChanged(nameof(Value));
    }

    public byte ToByte()
    {
        byte val = 0;
        for (int i = 0; i < 8; i++)
            if (Bits[i].IsSet)
                val |= (byte)(1 << i);
        return val;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
