namespace WpfScada.Models.Plc;

public record DataTypeInfo(string Name, int Size, int Alignment);

public record DbVariable(int Offset, string Name, string DataType, int Size, string? InitialValue, string? Comment);

public class DbStructure
{
    public int DbNumber { get; set; }
    public string DbName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public List<DbVariable> Variables { get; set; } = [];
    public bool HasUnknownType { get; set; }
    public string? ParseError { get; set; }
    public string Label => $"DB{DbNumber}: {DbName}";
}

public class UdtStructure
{
    public string UdtName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public List<DbVariable> Variables { get; set; } = [];
    public bool HasUnknownType { get; set; }
    public string? ParseError { get; set; }
}

public record ImportedDbInfo
{
    public int DbNumber { get; set; }
    public string DbName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string VariablesJson { get; set; } = "";
}

public record ImportedUdtInfo
{
    public string UdtName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string VariablesJson { get; set; } = "";
}

public static class SiemensDataTypes
{
    public static readonly Dictionary<string, DataTypeInfo> Known = new()
    {
        ["BOOL"] = new("BOOL", 1, 1),
        ["BYTE"] = new("BYTE", 1, 1),
        ["CHAR"] = new("CHAR", 1, 1),
        ["INT"] = new("INT", 2, 2),
        ["DINT"] = new("DINT", 4, 4),
        ["REAL"] = new("REAL", 4, 4),
        ["LREAL"] = new("LREAL", 8, 8),
        ["TIME"] = new("TIME", 4, 4),
        ["S5TIME"] = new("S5TIME", 4, 2),
        ["DATE"] = new("DATE", 4, 4),
        ["TIME_OF_DAY"] = new("TIME_OF_DAY", 4, 4),
        ["TOD"] = new("TOD", 4, 4),
        ["DATE_AND_TIME"] = new("DATE_AND_TIME", 8, 8),
        ["DT"] = new("DT", 8, 8),
        ["DTL"] = new("DTL", 12, 4),
        ["STRING"] = new("STRING", 256, 1),
        ["WORD"] = new("WORD", 2, 2),
        ["DWORD"] = new("DWORD", 4, 4),
        ["LWORD"] = new("LWORD", 8, 2),
        ["LINT"] = new("LINT", 8, 2),
        ["ULINT"] = new("ULINT", 8, 2),
        ["SINT"] = new("SINT", 1, 1),
        ["USINT"] = new("USINT", 1, 1),
        ["UINT"] = new("UINT", 2, 2),
        ["UDINT"] = new("UDINT", 4, 4),
    };

    public static bool TryResolve(string rawType, out int size, out int alignment)
    {
        size = 4; alignment = 2;
        rawType = rawType.Trim();

        if (Known.TryGetValue(rawType, out var info))
        {
            size = info.Size;
            alignment = info.Alignment;
            return true;
        }

        var arrayMatch = System.Text.RegularExpressions.Regex.Match(rawType, @"ARRAY\s*\[([^]]+)\]\s*OF\s+(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (arrayMatch.Success)
        {
            var range = arrayMatch.Groups[1].Value;
            var elemType = arrayMatch.Groups[2].Value.Trim();
            var parts = range.Split("..");
            if (parts.Length == 2 && int.TryParse(parts[0], out int lo) && int.TryParse(parts[1], out int hi))
            {
                int count = hi - lo + 1;
                if (count > 0 && TryResolve(elemType, out int elemSize, out int elemAlign))
                {
                    size = count * elemSize;
                    alignment = elemAlign;
                    return true;
                }
            }
        }

        var strMatch = System.Text.RegularExpressions.Regex.Match(rawType, @"STRING\s*\[(\d+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (strMatch.Success && int.TryParse(strMatch.Groups[1].Value, out int maxLen))
        {
            size = maxLen + 2;
            alignment = 1;
            return true;
        }

        return false;
    }
}
