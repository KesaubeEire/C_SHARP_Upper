using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WpfScada.Models.Plc;

public class DbVariableDisplay : INotifyPropertyChanged
{
    private string _value = "";
    private string _inputValue = "";

    public int ByteOffset { get; set; }
    public int BitOffset { get; set; } = -1;
    public bool IsBit => BitOffset >= 0;
    public string OffsetDisplay => IsBit ? $"{ByteOffset}.{BitOffset}" : $"{ByteOffset}.0";
    public Visibility BoolVis => IsBit ? Visibility.Visible : Visibility.Collapsed;
    public bool IsEditable => !IsBit && SiemensDataTypes.Known.ContainsKey(DataType.Trim().Trim('"').ToUpperInvariant())
                              && !DataType.Trim().Trim('"').ToUpperInvariant().EndsWith("STRING");
    public Visibility EditVis => IsEditable ? Visibility.Visible : Visibility.Collapsed;
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public int Size { get; set; }
    public string? InitialValue { get; set; }
    public string? Comment { get; set; }
    public bool IsFromUdt { get; set; }
    public string? UdtName { get; set; }

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public string InputValue
    {
        get => _inputValue;
        set { _inputValue = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
