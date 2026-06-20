using System.IO;
using System.Text.RegularExpressions;
using TestWpf.Models;

namespace TestWpf.Services;

/// <summary>
/// 解析 TIA Portal 导出的 .udt 文件
/// </summary>
public static class UdtFileParser
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

            // 解析变量
            int currentOffset = 0;
            bool inStruct = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase))
                { inStruct = true; continue; }

                if (line.StartsWith("END_STRUCT", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("END_TYPE", StringComparison.OrdinalIgnoreCase))
                { inStruct = false; break; }

                if (!inStruct) continue;
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

                    if (dataType.EndsWith(';')) dataType = dataType[..^1].Trim();
                    if (initVal.EndsWith(';')) initVal = initVal[..^1].Trim();

                    if (SiemensDataTypes.TryResolve(dataType, out int size, out int align))
                    {
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
                        result.HasUnknownType = true;
                        result.ParseError = $"未知数据类型: {dataType} (UDT: {result.UdtName}, 变量: {varName})";
                        return result;
                    }
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
}
