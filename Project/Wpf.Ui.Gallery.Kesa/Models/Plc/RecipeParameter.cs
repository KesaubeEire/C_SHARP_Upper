using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>
/// Represents a single parameter within a recipe.
/// Maps to a PLC register address with scaling support.
/// </summary>
public class RecipeParameter : INotifyPropertyChanged
{
    private string _name = "";
    private double _value;
    private string _unit = "";
    private ushort _address;

    /// <summary>Parameter name, e.g. "温度", "压力".</summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>The parameter value in engineering units.</summary>
    public double Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    /// <summary>Engineering unit, e.g. "°C", "MPa".</summary>
    public string Unit
    {
        get => _unit;
        set { _unit = value; OnPropertyChanged(); }
    }

    /// <summary>PLC register address (Modbus or DB offset).</summary>
    public ushort Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); }
    }

    /// <summary>Scaling factor: actual_value = raw * Scale + Offset.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>Offset: actual_value = raw * Scale + Offset.</summary>
    public double Offset { get; set; }

    /// <summary>Minimum allowed value for validation.</summary>
    public double MinValue { get; set; } = double.MinValue;

    /// <summary>Maximum allowed value for validation.</summary>
    public double MaxValue { get; set; } = double.MaxValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
