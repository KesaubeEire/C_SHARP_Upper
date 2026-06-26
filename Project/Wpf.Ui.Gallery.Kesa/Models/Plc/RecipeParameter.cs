using CommunityToolkit.Mvvm.ComponentModel;

namespace Wpf.Ui.Gallery.Models.Plc;

public partial class RecipeParameter : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private string _unit = "";

    [ObservableProperty]
    private ushort _address;

    [ObservableProperty]
    private double _scale = 1.0;

    [ObservableProperty]
    private double _offset;

    [ObservableProperty]
    private double _minValue = double.MinValue;

    [ObservableProperty]
    private double _maxValue = double.MaxValue;

    /// <summary>Parameter group/category, e.g. "温度", "压力", "速度"</summary>
    [ObservableProperty]
    private string _group = "";

    /// <summary>PLC data type for write-back: REAL, INT, DINT, WORD, BYTE</summary>
    [ObservableProperty]
    private string _plcDataType = "REAL";

    /// <summary>DB number for PLC write-back (0 = use default)</summary>
    [ObservableProperty]
    private int _dbNumber;

    /// <summary>The raw PLC value before scaling (computed from raw * Scale + Offset)</summary>
    public double RawValue => (Value - Offset) / (Scale != 0 ? Scale : 1);
}
