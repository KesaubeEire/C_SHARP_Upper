using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfScada.Models.Plc;

public class ByteRowViewModel : INotifyPropertyChanged
{
    private byte _value;
    private bool _hasChanges;

    public int ByteAddress { get; }
    public string AreaLabel { get; }
    public static string AreaLabelToChinese(string area) => area.ToUpperInvariant() switch
    {
        "I" => "I 区",
        "Q" => "Q 区",
        "M" => "M 区",
        _ => area
    };

    public string Label => $"{AreaLabelToChinese(AreaLabel)}B{ByteAddress}";
    public bool IsReadOnly { get; }

    private bool _writeModeEnabled;
    public bool WriteModeEnabled
    {
        get => _writeModeEnabled;
        set
        {
            _writeModeEnabled = value;
            foreach (var bit in Bits)
                bit.WriteModeEnabled = value;
        }
    }

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
                OnPropertyChanged(nameof(HexText));
                for (int i = 0; i < 8; i++)
                    Bits[i].IsSet = ((value >> i) & 1) == 1;
            }
        }
    }

    public string HexText => $"0x{_value:X2}";
    public bool HasChanges { get => _hasChanges; set { _hasChanges = value; OnPropertyChanged(); } }

    public ByteRowViewModel(int byteAddress, string areaLabel, bool isReadOnly)
    {
        ByteAddress = byteAddress;
        AreaLabel = areaLabel;
        IsReadOnly = isReadOnly;
        Bits = Enumerable.Range(0, 8).Select(i => new BitViewModel(i, isReadOnly) { Parent = this }).ToList();
    }

    public void NotifyBitChanged()
    {
        HasChanges = true;
        _value = ToByte();
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(HexText));
    }

    public byte ToByte()
    {
        byte v = 0;
        for (int i = 0; i < 8; i++)
            if (Bits[i].IsSet) v |= (byte)(1 << i);
        return v;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
