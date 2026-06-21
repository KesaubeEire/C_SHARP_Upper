using System.IO;
using System.Text.RegularExpressions;
using TestWpf.Models;

namespace TestWpf.Services;

/// <summary>
/// 解析 TIA Portal 导出的 .db 文件
/// 修复：解析 STRUCT 段（非 BEGIN 段）、处理 {…} 属性块、引用类型、UDF/UDT 类型占位
/// </summary>
public static partial class DbFileParser
{
    /// <summary>解析 .db 文件内容，返回 DB 结构</summary>
    public static DbStructure Parse(string filePath)
    {
        var result = new DbStructure
        {
            SourceFile = filePath,
            DbName = Path.GetFileNameWithoutExtension(filePath)
        };

        try
        {
            string text = File.ReadAllText(filePath);
            var lines = text.Split('\n', '\r')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            // 提取 DB 名称
            var nameMatch = Regex.Match(text, @"DATA_BLOCK\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
                result.DbName = nameMatch.Groups[1].Value;

            // 解析变量 — 在 STRUCT … END_STRUCT 段内
            // 偏移规则（参考 trio op dbParser.ts）：
            //   BOOL: 同字节位偏移 0.0-0.7，超过 8 位进下一字节
            //   非 BOOL: 清位 → 两字节（字）对齐 → 放置 → 递进
            int currentOffset = 0;
            int bitOffset = 0;
            bool inStruct = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase))
                { inStruct = true; continue; }

                if (inStruct && (
                    line.StartsWith("END_STRUCT", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase)))
                { inStruct = false; break; }

                if (!inStruct) continue;

                // 跳过注释、空结构体标记
                if (line.StartsWith("//") || line.StartsWith("/*")) continue;
                if (line == "{" || line == "}" || line == "};") continue;
                // 跳过 VERSION / NON_RETAIN 等元信息
                if (line.StartsWith("VERSION", StringComparison.OrdinalIgnoreCase)) continue;

                // 剥离 { ... } 属性块（西门子 S7-1200 特有）
                string clean = AttributeBlockRegex().Replace(line, "").Trim();
                if (clean.Length == 0) continue;

                // 解析变量行
                var varMatch = VarLineRegex().Match(clean);
                if (!varMatch.Success) continue;

                string varName = varMatch.Groups[1].Value.Trim('"', ' ');
                string rawType  = varMatch.Groups[2].Value.Trim();
                string initVal  = varMatch.Groups[3].Success ? varMatch.Groups[3].Value.Trim() : "";
                string comment  = varMatch.Groups[4].Success ? varMatch.Groups[4].Value.Trim() : "";

                // 去除末尾的分号
                if (rawType.EndsWith(';'))  rawType  = rawType[..^1].Trim();
                if (initVal.EndsWith(';'))  initVal  = initVal[..^1].Trim();

                // 解析数据类型
                if (SiemensDataTypes.TryResolve(rawType, out int size, out _))
                {
                    // BOOL 特殊处理：位偏移
                    if (rawType.Trim().Equals("BOOL", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bitOffset >= 8) { currentOffset++; bitOffset = 0; }

                        result.Variables.Add(new DbVariable
                        {
                            Offset = currentOffset,
                            Name = varName,
                            DataType = rawType,
                            Size = 1,
                            InitialValue = initVal,
                            Comment = comment
                        });

                        bitOffset++;
                    }
                    else
                    {
                        // 非 BOOL：清位 → 两字节对齐
                        if (bitOffset > 0) { currentOffset++; bitOffset = 0; }
                        if (currentOffset % 2 != 0) currentOffset++;

                        result.Variables.Add(new DbVariable
                        {
                            Offset = currentOffset,
                            Name = varName,
                            DataType = rawType,
                            Size = size,
                            InitialValue = initVal,
                            Comment = comment
                        });

                        currentOffset += size;
                    }
                }
                else if (rawType.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase))
                {
                    // 嵌套结构体 — 占位 4 字节
                    if (bitOffset > 0) { currentOffset++; bitOffset = 0; }
                    if (currentOffset % 2 != 0) currentOffset++;

                    result.Variables.Add(new DbVariable
                    {
                        Offset = currentOffset,
                        Name = varName,
                        DataType = "STRUCT",
                        Size = 4,
                        InitialValue = initVal,
                        Comment = comment
                    });
                    currentOffset += 4;
                }
                else if (rawType.StartsWith("ARRAY", StringComparison.OrdinalIgnoreCase))
                {
                    // ARRAY[…] OF type — 尽量计算大小，否则占位
                    if (bitOffset > 0) { currentOffset++; bitOffset = 0; }
                    if (currentOffset % 2 != 0) currentOffset++;

                    int arrSize = TryResolveArraySize(rawType, out _);
                    if (arrSize <= 0) arrSize = 4;

                    result.Variables.Add(new DbVariable
                    {
                        Offset = currentOffset,
                        Name = varName,
                        DataType = rawType,
                        Size = arrSize,
                        InitialValue = initVal,
                        Comment = comment
                    });
                    currentOffset += arrSize;
                }
                else
                {
                    // 可能是 UDT / UDF 引用、或无法解析的类型 — 占位
                    if (bitOffset > 0) { currentOffset++; bitOffset = 0; }
                    if (currentOffset % 2 != 0) currentOffset++;

                    result.Variables.Add(new DbVariable
                    {
                        Offset = currentOffset,
                        Name = varName,
                        DataType = rawType,
                        Size = 4,
                        InitialValue = initVal,
                        Comment = comment
                    });
                    currentOffset += 4;
                }
            }

            if (result.Variables.Count == 0)
                result.ParseError = "未能从文件中解析出任何变量";
        }
        catch (Exception ex)
        {
            result.ParseError = $"文件解析异常: {ex.Message}";
        }

        return result;
    }

    /// <summary>尝试解析 ARRAY[…] 的总字节数</summary>
    private static int TryResolveArraySize(string rawType, out int elemSize)
    {
        elemSize = 1;
        var m = Regex.Match(rawType, @"ARRAY\s*\[(\d+)\.\.(\d+)\]\s*OF\s+(.+)", RegexOptions.IgnoreCase);
        if (!m.Success) return -1;

        int lo = int.Parse(m.Groups[1].Value);
        int hi = int.Parse(m.Groups[2].Value);
        int count = hi - lo + 1;
        string elemType = m.Groups[3].Value.Trim().Trim('"'); // 去除引号

        if (SiemensDataTypes.TryResolve(elemType, out int es, out _))
        {
            elemSize = es;
            return count * es;
        }

        // ARRAY OF UDT — 按 4 字节估算（若无 UDT 展开则仅占位）
        elemSize = 4;
        return count * 4;
    }

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex AttributeBlockRegex();

    [GeneratedRegex(@"""?([^"":=]+)""?\s*:\s*(.+?)(?:\s*:=\s*(.+?))?(?:\s*//\s*(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex VarLineRegex();
}
