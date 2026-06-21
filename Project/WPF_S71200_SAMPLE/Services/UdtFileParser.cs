using System.IO;
using System.Text.RegularExpressions;
using TestWpf.Models;

namespace TestWpf.Services;

/// <summary>
/// 解析 TIA Portal 导出的 .udt 文件
/// 修复：处理 {…} 属性块、引用类型占位
/// </summary>
public static partial class UdtFileParser
{
    /// <summary>解析 .udt 文件内容，返回 UDT 结构</summary>
    public static UdtStructure Parse(string filePath)
    {
        var result = new UdtStructure
        {
            SourceFile = filePath,
            UdtName = Path.GetFileNameWithoutExtension(filePath)
        };

        try
        {
            string text = File.ReadAllText(filePath);
            var lines = text.Split('\n', '\r')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            // 提取 UDT 名称
            var nameMatch = Regex.Match(text, @"TYPE\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
                result.UdtName = nameMatch.Groups[1].Value;

            // 解析变量（偏移规则同 DbFileParser）
            int currentOffset = 0;
            int bitOffset = 0;
            bool inStruct = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase))
                { inStruct = true; continue; }

                if (line.StartsWith("END_STRUCT", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("END_TYPE", StringComparison.OrdinalIgnoreCase))
                { inStruct = false; break; }

                if (!inStruct) continue;
                if (line == "{" || line == "}" || line == "};") continue;

                // 剥离 { ... } 属性块
                string clean = AttributeBlockRegex().Replace(line, "").Trim();
                if (clean.Length == 0) continue;

                // 解析变量行
                var varMatch = VarLineRegex().Match(clean);
                if (!varMatch.Success) continue;

                string varName = varMatch.Groups[1].Value.Trim('"', ' ');
                string rawType = varMatch.Groups[2].Value.Trim();
                string initVal = varMatch.Groups[3].Success ? varMatch.Groups[3].Value.Trim() : "";
                string comment = varMatch.Groups[4].Success ? varMatch.Groups[4].Value.Trim() : "";

                if (rawType.EndsWith(';')) rawType = rawType[..^1].Trim();
                if (initVal.EndsWith(';')) initVal = initVal[..^1].Trim();

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
                    // 嵌套结构体 — 占位
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
                else
                {
                    // 可能的 UDT 引用 — 占位 4 字节，不设为错误
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

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex AttributeBlockRegex();

    [GeneratedRegex(@"""?([^"":=]+)""?\s*:\s*(.+?)(?:\s*:=\s*(.+?))?(?:\s*//\s*(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex VarLineRegex();
}
