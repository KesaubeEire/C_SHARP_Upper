using System.Text.RegularExpressions;
using WpfScada.Models.Plc;

namespace WpfScada.Services.Plc;

public static class UdtFileParser
{
    public static UdtStructure Parse(string filePath)
    {
        var result = new UdtStructure { SourceFile = filePath };
        var text = File.ReadAllText(filePath);

        var typeMatch = Regex.Match(text, @"TYPE\s+""([^""]+)""");
        if (typeMatch.Success) result.UdtName = typeMatch.Groups[1].Value;

        int structStart = text.IndexOf("STRUCT", StringComparison.OrdinalIgnoreCase);
        int structEnd = text.LastIndexOf("END_STRUCT", StringComparison.OrdinalIgnoreCase);
        if (structStart < 0 || structEnd < 0) return result;

        string block = text[structStart..structEnd];
        int currentOffset = 0, bitOffset = 0;

        foreach (var rawLine in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase) || line.StartsWith("END_STRUCT", StringComparison.OrdinalIgnoreCase))
                continue;

            line = Regex.Replace(line, @"\{[^}]*\}", "").Trim();
            var match = Regex.Match(line, @"""?([^""\s:=]+)""?\s*:\s*(\S[^:=]*?)(?:\s*:=\s*([^;]+))?(?:\s*;//\s*(.+))?$");
            if (!match.Success) continue;

            string varName = match.Groups[1].Value.Trim().Trim('"');
            string rawType = match.Groups[2].Value.Trim().TrimEnd(';');
            string comment = match.Groups[4].Success ? match.Groups[4].Value.Trim() : "";

            // Normalize known types to uppercase canonical form
            string upper = rawType.ToUpperInvariant();
            if (SiemensDataTypes.Known.ContainsKey(upper))
                rawType = upper;

            if (rawType == "BOOL")
            {
                if (bitOffset >= 16) { currentOffset += 2; bitOffset = 0; }
                result.Variables.Add(new DbVariable(currentOffset, varName, "BOOL", 1, null, comment));
                bitOffset++;
            }
            else if (SiemensDataTypes.TryResolve(rawType, out int size, out int _))
            {
                // BOOL 组结束 → 推进到下一个 WORD 边界
                if (bitOffset > 0) { currentOffset += 2; bitOffset = 0; }

                result.Variables.Add(new DbVariable(currentOffset, varName, rawType, size, null, comment));
                currentOffset += size;
            }
            else
            {
                // Unknown type — 占位
                if (bitOffset > 0) { currentOffset += 2; bitOffset = 0; }
                result.Variables.Add(new DbVariable(currentOffset, varName, rawType, 4, null, comment));
                currentOffset += 4;
                result.HasUnknownType = true;
            }
        }

        return result;
    }
}
