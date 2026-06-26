using System.Globalization;
using System.Text;
using System.Text.Json;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

public class RecipeService
{
    private readonly string _recipesDir;
    private readonly S7Service _s7;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public RecipeService(S7Service s7)
    {
        _s7 = s7;
        _recipesDir = Path.Combine(AppContext.BaseDirectory, "recipes");
        Directory.CreateDirectory(_recipesDir);
    }

    // ===================== Recipe CRUD =====================

    public List<RecipeMeta> GetAllRecipes()
    {
        if (!Directory.Exists(_recipesDir))
            return [];

        return Directory.EnumerateFiles(_recipesDir, "*.json")
            .Select(f =>
            {
                try
                {
                    using var stream = File.OpenRead(f);
                    using var doc = JsonDocument.Parse(stream);
                    var root = doc.RootElement;
                    return new RecipeMeta
                    {
                        Id = root.TryGetProperty("Id", out var id) ? id.GetString() ?? "" : "",
                        Name = root.TryGetProperty("Name", out var n) ? n.GetString() ?? Path.GetFileNameWithoutExtension(f) : Path.GetFileNameWithoutExtension(f),
                        Description = root.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "",
                        Version = root.TryGetProperty("Version", out var ver) ? ver.GetInt32() : 1,
                        Category = root.TryGetProperty("Category", out var cat) ? cat.GetString() ?? "" : "",
                        Tags = root.TryGetProperty("Tags", out var tags)
                            ? JsonSerializer.Deserialize<List<string>>(tags.GetRawText()) ?? []
                            : [],
                        CreatedAt = root.TryGetProperty("CreatedAt", out var ca) && ca.TryGetDateTime(out var cdt) ? cdt : File.GetCreationTime(f),
                        ModifiedAt = root.TryGetProperty("ModifiedAt", out var ma) && ma.TryGetDateTime(out var mdt) ? mdt : File.GetLastWriteTime(f),
                        ParameterCount = root.TryGetProperty("Parameters", out var paramsEl) ? paramsEl.GetArrayLength() : 0,
                    };
                }
                catch
                {
                    return null;
                }
            })
            .OfType<RecipeMeta>()
            .OrderByDescending(r => r.ModifiedAt)
            .ToList();
    }

    public RecipeRecord? LoadRecipe(string id)
    {
        var path = GetFilePath(id);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RecipeRecord>(json);
        }
        catch { return null; }
    }

    public void SaveRecipe(RecipeRecord recipe)
    {
        recipe.ModifiedAt = DateTime.Now;
        recipe.Version++;
        var path = GetFilePath(recipe.Id);
        var json = JsonSerializer.Serialize(recipe, _jsonOptions);
        File.WriteAllText(path, json);
    }

    public bool DeleteRecipe(string id)
    {
        var path = GetFilePath(id);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public RecipeRecord? CopyRecipe(string sourceId, string newName)
    {
        var source = LoadRecipe(sourceId);
        if (source is null) return null;

        var copy = new RecipeRecord
        {
            Name = newName,
            Description = source.Description,
            Category = source.Category,
            Tags = [.. source.Tags],
            DefaultDbNumber = source.DefaultDbNumber,
            DefaultArea = source.DefaultArea,
            Parameters = source.Parameters.Select(p => new RecipeParameter
            {
                Name = p.Name,
                Value = p.Value,
                Unit = p.Unit,
                Address = p.Address,
                Scale = p.Scale,
                Offset = p.Offset,
                MinValue = p.MinValue,
                MaxValue = p.MaxValue,
                Group = p.Group,
                PlcDataType = p.PlcDataType,
                DbNumber = p.DbNumber,
            }).ToList(),
        };

        SaveRecipe(copy);
        return copy;
    }

    // ===================== PLC Download / Upload =====================

    /// <summary>Download all recipe parameters to the PLC. Returns number of parameters written, or -1 on error.</summary>
    public int DownloadToPlc(RecipeRecord recipe, int defaultDb = 1)
    {
        if (!_s7.IsConnected) return -1;

        int success = 0;
        foreach (var param in recipe.Parameters)
        {
            double rawValue = param.RawValue;
            int db = param.DbNumber > 0 ? param.DbNumber : (defaultDb > 0 ? defaultDb : recipe.DefaultDbNumber);
            if (db <= 0) db = 1;

            var data = EncodeForPlc(rawValue, param.PlcDataType);
            if (data == null) continue;

            if (_s7.WriteBytesRaw(S7Service.AreaDB, param.Address, data, db))
                success++;
        }
        return success;
    }

    /// <summary>Upload all recipe parameter values from the PLC. Returns number read, or -1 on error.</summary>
    public int UploadFromPlc(RecipeRecord recipe, int defaultDb = 1)
    {
        if (!_s7.IsConnected) return -1;

        int success = 0;
        foreach (var param in recipe.Parameters)
        {
            int db = param.DbNumber > 0 ? param.DbNumber : (defaultDb > 0 ? defaultDb : recipe.DefaultDbNumber);
            if (db <= 0) db = 1;

            int byteSize = GetDataTypeSize(param.PlcDataType);
            var raw = _s7.ReadBytesRaw(S7Service.AreaDB, param.Address, byteSize, db);
            if (raw == null) continue;

            double pVal = DecodeFromPlc(raw, param.PlcDataType);
            param.Value = pVal * param.Scale + param.Offset;
            success++;
        }
        return success;
    }

    // ===================== Import / Export CSV =====================

    public string ExportToCsv(RecipeRecord recipe)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Value,Unit,Address,Scale,Offset,Group,PlcDataType,DbNumber,MinValue,MaxValue");
        foreach (var p in recipe.Parameters)
        {
            sb.AppendLine(
                $"{EscapeCsv(p.Name)},{p.Value.ToString(CultureInfo.InvariantCulture)},{EscapeCsv(p.Unit)},{p.Address}," +
                $"{p.Scale.ToString(CultureInfo.InvariantCulture)},{p.Offset.ToString(CultureInfo.InvariantCulture)}," +
                $"{EscapeCsv(p.Group)},{p.PlcDataType},{p.DbNumber}," +
                $"{p.MinValue.ToString(CultureInfo.InvariantCulture)},{p.MaxValue.ToString(CultureInfo.InvariantCulture)}");
        }
        return sb.ToString();
    }

    public List<RecipeParameter> ImportFromCsv(string csvText)
    {
        var result = new List<RecipeParameter>();
        var lines = csvText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2) return result;

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var cols = ParseCsvLine(lines[i]);
                if (cols.Length < 6) continue;

                var param = new RecipeParameter
                {
                    Name = cols[0],
                    Value = double.TryParse(cols[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0,
                    Unit = cols.Length > 2 ? cols[2] : "",
                    Address = ushort.TryParse(cols[3], out var a) ? a : (ushort)0,
                    Scale = cols.Length > 4 && double.TryParse(cols[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 1.0,
                    Offset = cols.Length > 5 && double.TryParse(cols[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var o) ? o : 0,
                    Group = cols.Length > 6 ? cols[6] : "",
                    PlcDataType = cols.Length > 7 ? cols[7] : "REAL",
                    DbNumber = cols.Length > 8 && int.TryParse(cols[8], out var db) ? db : 0,
                    MinValue = cols.Length > 9 && double.TryParse(cols[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var min) ? min : double.MinValue,
                    MaxValue = cols.Length > 10 && double.TryParse(cols[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var max) ? max : double.MaxValue,
                };
                result.Add(param);
            }
            catch { /* skip malformed lines */ }
        }
        return result;
    }

    // ===================== Helpers =====================

    private string GetFilePath(string id) => Path.Combine(_recipesDir, $"{id}.json");

    private static string EscapeCsv(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        int idx = 0;
        while (idx < line.Length)
        {
            char c = line[idx];
            if (c == '"')
            {
                if (inQuotes && idx + 1 < line.Length && line[idx + 1] == '"')
                {
                    current.Append('"');
                    idx++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
            idx++;
        }
        result.Add(current.ToString());
        return [.. result];
    }

    private static byte[]? EncodeForPlc(double value, string dataType)
    {
        return dataType.ToUpperInvariant() switch
        {
            "BYTE" or "USINT" => [(byte)Math.Clamp(value, 0, 255)],
            "SINT" => [(byte)(sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue)],
            "INT" => FromInt16BE((short)Math.Clamp(value, short.MinValue, short.MaxValue)),
            "UINT" or "WORD" => FromUInt16BE((ushort)Math.Clamp(value, 0, ushort.MaxValue)),
            "DINT" => FromInt32BE((int)Math.Clamp(value, int.MinValue, int.MaxValue)),
            "UDINT" or "DWORD" => FromUInt32BE((uint)Math.Clamp(value, 0, uint.MaxValue)),
            "REAL" => FromFloatBE((float)value),
            _ => null
        };
    }

    private static double DecodeFromPlc(byte[] data, string dataType)
    {
        return dataType.ToUpperInvariant() switch
        {
            "BYTE" or "USINT" => data[0],
            "SINT" => (sbyte)data[0],
            "INT" => ToInt16BE(data),
            "UINT" or "WORD" => ToUInt16BE(data),
            "DINT" => ToInt32BE(data),
            "UDINT" or "DWORD" => ToUInt32BE(data),
            "REAL" => ToFloatBE(data),
            _ => 0
        };
    }

    private static int GetDataTypeSize(string dataType) => dataType.ToUpperInvariant() switch
    {
        "BYTE" or "USINT" or "SINT" => 1,
        "INT" or "UINT" or "WORD" => 2,
        "DINT" or "UDINT" or "DWORD" or "REAL" => 4,
        _ => 4
    };

    private static byte[] FromInt16BE(short val) => [(byte)(val >> 8), (byte)val];
    private static byte[] FromUInt16BE(ushort val) => [(byte)(val >> 8), (byte)val];
    private static byte[] FromInt32BE(int val) => [(byte)(val >> 24), (byte)(val >> 16), (byte)(val >> 8), (byte)val];
    private static byte[] FromUInt32BE(uint val) => [(byte)(val >> 24), (byte)(val >> 16), (byte)(val >> 8), (byte)val];
    private static byte[] FromFloatBE(float val)
    {
        var le = BitConverter.GetBytes(val);
        return [le[3], le[2], le[1], le[0]];
    }

    private static short ToInt16BE(byte[] b) => (short)((b[0] << 8) | b[1]);
    private static ushort ToUInt16BE(byte[] b) => (ushort)((b[0] << 8) | b[1]);
    private static int ToInt32BE(byte[] b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    private static uint ToUInt32BE(byte[] b) => (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    private static float ToFloatBE(byte[] b)
    {
        byte[] le = [b[3], b[2], b[1], b[0]];
        return BitConverter.ToSingle(le);
    }
}

public class RecipeMeta
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Version { get; init; }
    public string Category { get; init; } = "";
    public List<string> Tags { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime ModifiedAt { get; init; }
    public int ParameterCount { get; init; }
}
