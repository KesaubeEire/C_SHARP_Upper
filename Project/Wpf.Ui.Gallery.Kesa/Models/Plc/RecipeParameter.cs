using System.Text.Json.Serialization;
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

    private string _plcDataType = "REAL";

    /// <summary>
    /// PLC data type as string (serialized as "PlcDataType" in JSON for backward compat).
    /// </summary>
    [JsonPropertyName("PlcDataType")]
    public string PlcDataTypeStr
    {
        get => _plcDataType;
        set
        {
            if (!StringComparer.Ordinal.Equals(_plcDataType, value))
            {
                OnPropertyChanging();
                _plcDataType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DataType));
            }
        }
    }

    /// <summary>PLC data type as a typed enum (for code safety, not serialized directly).</summary>
    [JsonIgnore]
    public PlcDataType DataType
    {
        get => ParseDataType(_plcDataType);
        set => PlcDataTypeStr = DataTypeToName(value);
    }

    /// <summary>DB number for PLC write-back (0 = use recipe default).</summary>
    [ObservableProperty]
    private int _dbNumber;

    /// <summary>The raw PLC value before scaling (computed from raw * Scale + Offset).</summary>
    [JsonIgnore]
    public double RawValue => (Value - Offset) / (Scale != 0 ? Scale : 1);

    // ===================== DataType mapping =====================

    public static string DataTypeToName(PlcDataType dt) => dt switch
    {
        PlcDataType.Real => "REAL",
        PlcDataType.Int => "INT",
        PlcDataType.DInt => "DINT",
        PlcDataType.UInt => "UINT",
        PlcDataType.UDInt => "UDINT",
        PlcDataType.Word => "WORD",
        PlcDataType.DWord => "DWORD",
        PlcDataType.Byte => "BYTE",
        PlcDataType.USInt => "USINT",
        PlcDataType.SInt => "SINT",
        PlcDataType.Bool => "BOOL",
        _ => "REAL",
    };

    public static PlcDataType ParseDataType(string name) => (name ?? "REAL").ToUpperInvariant() switch
    {
        "REAL" => PlcDataType.Real,
        "INT" => PlcDataType.Int,
        "DINT" => PlcDataType.DInt,
        "UINT" => PlcDataType.UInt,
        "UDINT" => PlcDataType.UDInt,
        "WORD" => PlcDataType.Word,
        "DWORD" => PlcDataType.DWord,
        "BYTE" => PlcDataType.Byte,
        "USINT" => PlcDataType.USInt,
        "SINT" => PlcDataType.SInt,
        "BOOL" => PlcDataType.Bool,
        _ => PlcDataType.Real,
    };
}
