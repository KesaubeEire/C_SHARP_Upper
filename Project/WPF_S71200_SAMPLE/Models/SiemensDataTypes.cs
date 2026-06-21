namespace TestWpf.Models;

/// <summary>
/// 西门子 S7 数据类型定义 — 名称 + 字节大小 + 对齐规则
/// </summary>
public static class SiemensDataTypes
{
    /// <summary>所有已知数据类型（名称 → 信息）</summary>
    public static readonly Dictionary<string, DataTypeInfo> Known = new()
    {
        ["BOOL"]   = new("BOOL",   1, 1),
        ["BYTE"]   = new("BYTE",   1, 1),
        ["SINT"]   = new("SINT",   1, 1),
        ["USINT"]  = new("USINT",  1, 1),
        ["CHAR"]   = new("CHAR",   1, 1),
        ["WCHAR"]  = new("WCHAR",  2, 2),
        ["WORD"]   = new("WORD",   2, 2),
        ["INT"]    = new("INT",    2, 2),
        ["UINT"]   = new("UINT",   2, 2),
        ["DWORD"]  = new("DWORD",  4, 4),
        ["DINT"]   = new("DINT",   4, 4),
        ["UDINT"]  = new("UDINT",  4, 4),
        ["REAL"]   = new("REAL",   4, 4),
        ["LREAL"]  = new("LREAL",  8, 8),
        ["TIME"]   = new("TIME",   4, 4),
        ["DATE"]   = new("DATE",   2, 2),
        ["TOD"]    = new("TOD",    4, 4),
        ["S5TIME"] = new("S5TIME", 4, 2),
        ["DT"]     = new("DT",     8, 8),
        ["DTL"]    = new("DTL",   12, 4),
        ["LWORD"]  = new("LWORD",  8, 2),
        ["LINT"]   = new("LINT",   8, 2),
        ["ULINT"]  = new("ULINT",  8, 2),
        ["IEC_TIMER"]    = new("IEC_TIMER",    16, 2),
        ["IEC_LTIMER"]   = new("IEC_LTIMER",   20, 2),
        ["IEC_SCOUNTER"] = new("IEC_SCOUNTER", 16, 2),
        ["IEC_COUNTER"]  = new("IEC_COUNTER",  16, 2),
        ["IEC_DCOUNTER"] = new("IEC_DCOUNTER", 16, 2),
        ["IEC_LCOUNTER"] = new("IEC_LCOUNTER", 24, 2),
        ["IEC_SSCOUNTER"]= new("IEC_SSCOUNTER",22, 2),
    };

    /// <summary>字符串默认长度</summary>
    public const int DefaultStringLen = 256;

    public static bool IsKnown(string typeName)
        => Known.ContainsKey(typeName.ToUpper());

    /// <summary>尝试解析类型，如果是 ARRAY/STRING/STRUCT 则特殊处理</summary>
    public static bool TryResolve(string rawType, out int size, out int alignment)
    {
        size = 0; alignment = 1;
        string upper = rawType.Trim().ToUpper();

        // 已知基本类型
        if (Known.TryGetValue(upper, out var info))
        { size = info.Size; alignment = info.Alignment; return true; }

        // STRING[xx]
        if (upper.StartsWith("STRING"))
        {
            int len = DefaultStringLen;
            var match = System.Text.RegularExpressions.Regex.Match(upper, @"STRING\[?(\d+)\]?");
            if (match.Success) len = int.Parse(match.Groups[1].Value);
            size = len + 2; // 2 bytes header
            alignment = 1;
            return true;
        }

        // ARRAY[lo..hi] OF type
        if (upper.StartsWith("ARRAY"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(upper, @"ARRAY\s*\[(\d+)\.\.(\d+)\]\s*OF\s+(.+)");
            if (m.Success)
            {
                int lo = int.Parse(m.Groups[1].Value);
                int hi = int.Parse(m.Groups[2].Value);
                int count = hi - lo + 1;
                string elemType = m.Groups[3].Value.Trim().Trim('"');
                if (TryResolve(elemType, out int elemSize, out int elemAlign))
                {
                    size = count * elemSize;
                    alignment = elemAlign;
                    return true;
                }
            }
            return false;
        }

        return false;
    }
}

public record DataTypeInfo(string Name, int Size, int Alignment);

/// <summary>
/// 序列化友好的导入 DB 信息（用于 JSON 持久化）
/// </summary>
public class ImportedDbInfo
{
    public int DbNumber { get; set; }
    public string DbName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string VariablesJson { get; set; } = "[]"; // JSON 序列化的变量列表
}

/// <summary>
/// 序列化友好的导入 UDT 信息（用于 JSON 持久化）
/// </summary>
public class ImportedUdtInfo
{
    public string UdtName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string VariablesJson { get; set; } = "[]";
}

/// <summary>
/// 解析出的 DB 变量
/// </summary>
public class DbVariable
{
    public int Offset { get; set; }
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public int Size { get; set; }
    public string? InitialValue { get; set; }
    public string? Comment { get; set; }

    public int EndOffset => Offset + Size - 1;
    public override string ToString() => $"+{Offset,4} | {Name,-20} {DataType,-10} {Size}B";
}

/// <summary>
/// 解析出的 DB 块结构
/// </summary>
public class DbStructure
{
    public int DbNumber { get; set; }      // 用户手动输入
    public string DbName { get; set; } = "";
    public string SourceFile { get; set; } = ""; // 原始 .db 文件路径
    public List<DbVariable> Variables { get; set; } = [];
    public int TotalSize => Variables.Count > 0 ? Variables.Max(v => v.EndOffset) + 1 : 0;
    public bool HasUnknownType { get; set; }
    public string? ParseError { get; set; }

    public string Label => $"DB{DbNumber} ({DbName})";
}

/// <summary>
/// 解析出的 UDT 结构
/// </summary>
public class UdtStructure
{
    public string UdtName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public List<DbVariable> Variables { get; set; } = [];
    public int TotalSize => Variables.Count > 0 ? Variables.Max(v => v.EndOffset) + 1 : 0;
    public bool HasUnknownType { get; set; }
    public string? ParseError { get; set; }
}
