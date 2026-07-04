using System.Text.RegularExpressions;
using WpfScada.Models.Plc;

namespace WpfScada.Services.Plc;

public static class DbFileParser
{
    public static DbStructure Parse(string filePath)
    {
        var result = new DbStructure { SourceFile = filePath };
        var lines = File.ReadAllLines(filePath);
        string text = string.Join("\n", lines);

        var dbMatch = Regex.Match(text, @"DATA_BLOCK\s+""([^""]+)""");
        if (dbMatch.Success) result.DbName = dbMatch.Groups[1].Value;

        int structStart = text.IndexOf("STRUCT", StringComparison.OrdinalIgnoreCase);
        int structEnd = text.LastIndexOf("END_STRUCT", StringComparison.OrdinalIgnoreCase);
        if (structStart < 0 || structEnd < 0 || structEnd <= structStart)
        {
            result.ParseError = "未找到 STRUCT 块";
            return result;
        }

        string block = text[structStart..structEnd];
        var varLines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int currentOffset = 0;
        int currentBitOffset = 0;

        foreach (var rawLine in varLines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase))
                continue;

            // Remove {S7_xxx} attribute blocks
            line = Regex.Replace(line, @"\{[^}]*\}", "").Trim();
            if (line.Length == 0) continue;

            var match = Regex.Match(line, @"""?([^""\s:=]+)""?\s*:\s*(\S[^:=]*?)(?:\s*:=\s*([^;]+))?(?:\s*;//\s*(.+))?$");
            if (!match.Success) continue;

            string varName = match.Groups[1].Value.Trim();
            string rawType = match.Groups[2].Value.Trim().TrimEnd(';');
            string initVal = match.Groups[3].Success ? match.Groups[3].Value.Trim().TrimEnd(';') : "";
            string comment = match.Groups[4].Success ? match.Groups[4].Value.Trim() : "";

            // Handle multi-word types with spaces
            foreach (var known in SiemensDataTypes.Known.Keys.OrderByDescending(k => k.Length))
            {
                if (rawType.StartsWith(known, StringComparison.OrdinalIgnoreCase))
                {
                    rawType = known + rawType[known.Length..];
                    break;
                }
            }

            if (SiemensDataTypes.TryResolve(rawType, out int size, out int alignment))
            {
                // Align offset
                if (alignment > 1 && currentOffset % alignment != 0)
                    currentOffset += alignment - (currentOffset % alignment);

                if (rawType == "BOOL")
                {
                    result.Variables.Add(new DbVariable(currentOffset, varName, "BOOL", 1, initVal, comment)
                    {
                        // Bit offset will be set during expansion
                    });
                    currentBitOffset++;
                    if (currentBitOffset >= 8) { currentBitOffset = 0; currentOffset++; }
                }
                else
                {
                    if (currentBitOffset > 0) { currentOffset++; currentBitOffset = 0; }
                    result.Variables.Add(new DbVariable(currentOffset, varName, rawType, size, initVal, comment));
                    currentOffset += size;
                }
            }
            else
            {
                // Unknown type - placeholder
                if (currentBitOffset > 0) { currentOffset++; currentBitOffset = 0; }
                result.Variables.Add(new DbVariable(currentOffset, varName, rawType, 4, initVal, comment));
                currentOffset += 4;
                result.HasUnknownType = true;
            }
        }

        return result;
    }
}
