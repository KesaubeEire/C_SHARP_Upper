using System.IO;
using System.Text.RegularExpressions;
using TestWpf.Models;

namespace TestWpf.Services;

/// <summary>
/// 解析 TIA Portal 导出的 .db 文件
/// </summary>
public static class DbFileParser
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

            // 解析变量
            int currentOffset = 0;
            bool inBlock = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase))
                { inBlock = true; continue; }

                if (line.StartsWith("END_DATA_BLOCK", StringComparison.OrdinalIgnoreCase))
                { inBlock = false; break; }

                if (!inBlock) continue;
                if (line.StartsWith("//") || line.StartsWith("/*")) continue;

                // 跳过空行/结构体声明
                if (line == "{" || line == "};") continue;

                // 解析变量行
                var varMatch = Regex.Match(line,
                    @"""?([^"":=]+)""?\s*:\s*(.+?)(?:\s*:=\s*(.+?))?(?:\s*//\s*(.+))?$",
                    RegexOptions.IgnoreCase);

                if (varMatch.Success)
                {
                    string varName = varMatch.Groups[1].Value.Trim('"', ' ');
                    string dataType = varMatch.Groups[2].Value.Trim();
                    string initVal = varMatch.Groups[3].Success ? varMatch.Groups[3].Value.Trim() : "";
                    string comment = varMatch.Groups[4].Success ? varMatch.Groups[4].Value.Trim() : "";

                    // 去除末尾的分号
                    if (dataType.EndsWith(';')) dataType = dataType[..^1].Trim();
                    if (initVal.EndsWith(';')) initVal = initVal[..^1].Trim();

                    // 解析数据类型
                    if (SiemensDataTypes.TryResolve(dataType, out int size, out int align))
                    {
                        // 两字节对齐：如果当前偏移不是对齐的，补 padding
                        if (align > 1 && currentOffset % align != 0)
                            currentOffset += (align - currentOffset % align);

                        result.Variables.Add(new DbVariable
                        {
                            Offset = currentOffset,
                            Name = varName,
                            DataType = dataType,
                            Size = size,
                            InitialValue = initVal,
                            Comment = comment
                        });

                        currentOffset += size;
                    }
                    else
                    {
                        // 可能是 STRUCT 或未知类型
                        if (dataType.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase))
                        {
                            // 嵌套结构体 — 暂不深度解析，估一个占位
                            result.Variables.Add(new DbVariable
                            {
                                Offset = currentOffset,
                                Name = varName,
                                DataType = "STRUCT",
                                Size = 4, // 占位
                            });
                            currentOffset += 4;
                        }
                        else
                        {
                            result.HasUnknownType = true;
                            result.ParseError = $"未知数据类型: {dataType} (变量: {varName})";
                            return result;
                        }
                    }
                }
            }

            if (result.Variables.Count == 0)
            {
                result.ParseError = "未能从文件中解析出任何变量";
            }
        }
        catch (Exception ex)
        {
            result.ParseError = $"文件解析异常: {ex.Message}";
        }

        return result;
    }
}
