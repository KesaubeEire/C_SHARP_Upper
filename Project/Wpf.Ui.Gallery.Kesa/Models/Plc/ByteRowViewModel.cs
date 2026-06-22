using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wpf.Ui.Gallery.Models.Plc;

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
                OnPropertyChanged(nameof(HexText));
                for (int i = 0; i < 8; i++)
                    Bits[i].IsSet = ((value >> (7 - i)) & 1) == 1;
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
            if (Bits[i].IsSet) v |= (byte)(1 << (7 - i));
        return v;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
