using CommunityToolkit.Mvvm.ComponentModel;

namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>
/// Represents a single parameter within a recipe.
/// Maps to a PLC register address with scaling support.
/// </summary>
public partial class RecipeParameter : ObservableObject
{
    /// <summary>Parameter name, e.g. "温度", "压力".</summary>
    [ObservableProperty]
    private string _name = "";

    /// <summary>The parameter value in engineering units.</summary>
    [ObservableProperty]
    private double _value;

    /// <summary>Engineering unit, e.g. "°C", "MPa".</summary>
    [ObservableProperty]
    private string _unit = "";

    /// <summary>PLC register address (Modbus or DB offset).</summary>
    [ObservableProperty]
    private ushort _address;

    /// <summary>Scaling factor: actual_value = raw * Scale + Offset.</summary>
    [ObservableProperty]
    private double _scale = 1.0;

    /// <summary>Offset: actual_value = raw * Scale + Offset.</summary>
    [ObservableProperty]
    private double _offset;

    /// <summary>Minimum allowed value for validation.</summary>
    [ObservableProperty]
    private double _minValue = double.MinValue;

    /// <summary>Maximum allowed value for validation.</summary>
    [ObservableProperty]
    private double _maxValue = double.MaxValue;
}
