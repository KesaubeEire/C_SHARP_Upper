using System.Diagnostics.CodeAnalysis;

namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>PLC data types supported for recipe parameter read/write.</summary>
[SuppressMessage("Design", "CA1720", Justification = "PLC standard type names (INT, UINT, etc.)")]
public enum PlcDataType
{
    Real,
    Int,
    DInt,
    UInt,
    UDInt,
    Word,
    DWord,
    Byte,
    USInt,
    SInt,
    Bool,
}
