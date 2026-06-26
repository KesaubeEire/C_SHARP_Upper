using System.Globalization;
using System.Text;
using System.Text.Json;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

public class RecipeService
{
    private readonly string _recipesDir;
    private readonly string _versionsDir;
    private readonly S7Service _s7;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public RecipeService(S7Service s7)
    {
        _s7 = s7;
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kesa_PLC_TEST");
        _recipesDir = Path.Combine(baseDir, "recipes");
        _versionsDir = Path.Combine(baseDir, "recipes", "_versions");
        Directory.CreateDirectory(_recipesDir);
        Directory.CreateDirectory(_versionsDir);
    }

    // ===================== Recipe CRUD =====================

    public List<RecipeMeta> GetAllRecipes()
    {
        if (!Directory.Exists(_recipesDir))
            return [];

        return Directory.EnumerateFiles(_recipesDir, "*.json")
            .Where(f => !f.Contains("_versions")) // skip version directories
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
                        ProductCode = root.TryGetProperty("ProductCode", out var pc) ? pc.GetString() ?? "" : "",
                        Author = root.TryGetProperty("Author", out var au) ? au.GetString() ?? "" : "",
                        Status = root.TryGetProperty("Status", out var st) && Enum.TryParse<RecipeStatus>(st.GetString(), out var s) ? s : RecipeStatus.Draft,
                        Version = root.TryGetProperty("Version", out var ver) ? ver.GetInt32() : 1,
                        Category = root.TryGetProperty("Category", out var cat) ? cat.GetString() ?? "" : "",
                        Tags = root.TryGetProperty("Tags", out var tags)
                            ? JsonSerializer.Deserialize<List<string>>(tags.GetRawText()) ?? []
                            : [],
                        CreatedAt = root.TryGetProperty("CreatedAt", out var ca) && ca.TryGetDateTime(out var cdt) ? cdt : File.GetCreationTime(f),
                        ModifiedAt = root.TryGetProperty("ModifiedAt", out var ma) && ma.TryGetDateTime(out var mdt) ? mdt : File.GetLastWriteTime(f),
                        ParameterCount = CountParameters(root),
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

    /// <summary>Count total parameters across all groups (or flat Parameters array for old format).</summary>
    private static int CountParameters(JsonElement root)
    {
        if (root.TryGetProperty("Groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            int count = 0;
            foreach (var g in groups.EnumerateArray())
            {
                if (g.TryGetProperty("Parameters", out var ps) && ps.ValueKind == JsonValueKind.Array)
                    count += ps.GetArrayLength();
            }
            return count;
        }

        if (root.TryGetProperty("Parameters", out var flat) && flat.ValueKind == JsonValueKind.Array)
            return flat.GetArrayLength();

        return 0;
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

        // Create version snapshot
        SaveVersionSnapshot(recipe);
    }

    public bool DeleteRecipe(string id)
    {
        var path = GetFilePath(id);
        if (!File.Exists(path)) return false;
        File.Delete(path);

        // Remove version history
        var versionDir = GetVersionDir(id);
        if (Directory.Exists(versionDir))
            Directory.Delete(versionDir, recursive: true);

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
            ProductCode = source.ProductCode,
            Author = source.Author,
            Status = source.Status,
            Category = source.Category,
            Tags = [.. source.Tags],
            DefaultDbNumber = source.DefaultDbNumber,
            DefaultArea = source.DefaultArea,
            Groups = source.Groups.Select(g => new RecipeGroup
            {
                Name = g.Name,
                Description = g.Description,
                Parameters = new System.Collections.ObjectModel.ObservableCollection<RecipeParameter>(
                    g.Parameters.Select(p => DeepCopyParameter(p)).ToList()),
            }).ToList(),
        };

        SaveRecipe(copy);
        return copy;
    }

    private static RecipeParameter DeepCopyParameter(RecipeParameter p) => new()
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
        DataType = p.DataType,
        DbNumber = p.DbNumber,
    };

    // ===================== Version History =====================

    /// <summary>Get all version snapshots for a recipe, sorted descending.</summary>
    public List<RecipeVersionSnapshot> GetVersionHistory(string recipeId)
    {
        var versionDir = GetVersionDir(recipeId);
        if (!Directory.Exists(versionDir))
            return [];

        return Directory.EnumerateFiles(versionDir, "*.json")
            .Select(f =>
            {
                try
                {
                    using var stream = File.OpenRead(f);
                    using var doc = JsonDocument.Parse(stream);
                    var root = doc.RootElement;
                    return new RecipeVersionSnapshot
                    {
                        RecipeId = recipeId,
                        Version = root.TryGetProperty("Version", out var v) ? v.GetInt32() : 0,
                        SnapshotAt = root.TryGetProperty("ModifiedAt", out var m) && m.TryGetDateTime(out var dt) ? dt : File.GetCreationTime(f),
                        FilePath = f,
                    };
                }
                catch { return null; }
            })
            .OfType<RecipeVersionSnapshot>()
            .OrderByDescending(s => s.Version)
            .ToList();
    }

    /// <summary>Load a specific version snapshot.</summary>
    public RecipeRecord? LoadRecipeVersion(string recipeId, int version)
    {
        var path = GetVersionFilePath(recipeId, version);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RecipeRecord>(json);
        }
        catch { return null; }
    }

    /// <summary>Restore a previous version as the current recipe (creates a new version).</summary>
    public RecipeRecord? RestoreVersion(string recipeId, int version)
    {
        var snapshot = LoadRecipeVersion(recipeId, version);
        if (snapshot is null) return null;

        // Preserve the existing Id and timestamps
        var current = LoadRecipe(recipeId);
        var restored = new RecipeRecord
        {
            Id = recipeId,
            Name = snapshot.Name,
            Description = snapshot.Description,
            ProductCode = snapshot.ProductCode,
            Author = snapshot.Author,
            Status = snapshot.Status,
            Category = snapshot.Category,
            Tags = [.. snapshot.Tags],
            DefaultDbNumber = snapshot.DefaultDbNumber,
            DefaultArea = snapshot.DefaultArea,
            CreatedAt = current?.CreatedAt ?? DateTime.Now,
            Groups = snapshot.Groups.Select(g => new RecipeGroup
            {
                Name = g.Name,
                Description = g.Description,
                Parameters = new System.Collections.ObjectModel.ObservableCollection<RecipeParameter>(
                    g.Parameters.Select(p => DeepCopyParameter(p)).ToList()),
            }).ToList(),
        };

        SaveRecipe(restored);
        return restored;
    }

    /// <summary>Save a version snapshot of the recipe.</summary>
    private void SaveVersionSnapshot(RecipeRecord recipe)
    {
        var versionDir = GetVersionDir(recipe.Id);
        Directory.CreateDirectory(versionDir);
        var path = GetVersionFilePath(recipe.Id, recipe.Version);
        var json = JsonSerializer.Serialize(recipe, _jsonOptions);
        File.WriteAllText(path, json);
    }

    // ===================== PLC Download / Upload =====================

    /// <summary>Download all recipe parameters to the PLC. Returns number of parameters written, or -1 on error.</summary>
    public int DownloadToPlc(RecipeRecord recipe, int defaultDb = 1)
    {
        if (!_s7.IsConnected) return -1;

        int success = 0;
        foreach (var param in GetAllParameters(recipe))
        {
            double rawValue = param.RawValue;
            int db = param.DbNumber > 0 ? param.DbNumber : (defaultDb > 0 ? defaultDb : recipe.DefaultDbNumber);
            if (db <= 0) db = 1;

            var data = EncodeForPlc(rawValue, param.DataType);
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
        foreach (var param in GetAllParameters(recipe))
        {
            int db = param.DbNumber > 0 ? param.DbNumber : (defaultDb > 0 ? defaultDb : recipe.DefaultDbNumber);
            if (db <= 0) db = 1;

            int byteSize = GetDataTypeSize(param.DataType);
            var raw = _s7.ReadBytesRaw(S7Service.AreaDB, param.Address, byteSize, db);
            if (raw == null) continue;

            double pVal = DecodeFromPlc(raw, param.DataType);
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
        foreach (var param in GetAllParameters(recipe))
        {
            sb.AppendLine(
                $"{EscapeCsv(param.Name)},{param.Value.ToString(CultureInfo.InvariantCulture)},{EscapeCsv(param.Unit)},{param.Address}," +
                $"{param.Scale.ToString(CultureInfo.InvariantCulture)},{param.Offset.ToString(CultureInfo.InvariantCulture)}," +
                $"{EscapeCsv(param.Group)},{RecipeParameter.DataTypeToName(param.DataType)},{param.DbNumber}," +
                $"{param.MinValue.ToString(CultureInfo.InvariantCulture)},{param.MaxValue.ToString(CultureInfo.InvariantCulture)}");
        }
        return sb.ToString();
    }

    public List<RecipeParameter> ImportFromCsv(string csvText)
    {
        var result = new List<RecipeParameter>();
        var lines = csvText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2) return result;

        // Detect delimiter from header line: tab → TSV, comma → CSV
        char delimiter = lines[0].Contains('\t') ? '\t' : ',';

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var cols = ParseCsvLine(lines[i], delimiter);
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
                    DataType = cols.Length > 7 ? RecipeParameter.ParseDataType(cols[7]) : PlcDataType.Real,
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
    private string GetVersionDir(string id) => Path.Combine(_versionsDir, id);
    private string GetVersionFilePath(string id, int version) => Path.Combine(_versionsDir, id, $"v{version}.json");

    /// <summary>Flatten all parameters from all groups into a single enumerable.</summary>
    private static IEnumerable<RecipeParameter> GetAllParameters(RecipeRecord recipe)
    {
        if (recipe.Groups.Count > 0)
            return recipe.Groups.SelectMany(g => g.Parameters);

        return [];
    }

    private static string EscapeCsv(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

    private static string[] ParseCsvLine(string line, char delimiter = ',')
    {
        // Tab-delimited: no quotes, simple split
        if (delimiter == '\t')
            return line.Split('\t');

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
            else if (c == delimiter && !inQuotes)
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

    // ===================== PLC Encoding =====================

    private static byte[]? EncodeForPlc(double value, PlcDataType dataType)
    {
        return dataType switch
        {
            PlcDataType.Byte or PlcDataType.USInt => [(byte)Math.Clamp(value, 0, 255)],
            PlcDataType.SInt => [(byte)(sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue)],
            PlcDataType.Int => FromInt16BE((short)Math.Clamp(value, short.MinValue, short.MaxValue)),
            PlcDataType.UInt or PlcDataType.Word => FromUInt16BE((ushort)Math.Clamp(value, 0, ushort.MaxValue)),
            PlcDataType.DInt => FromInt32BE((int)Math.Clamp(value, int.MinValue, int.MaxValue)),
            PlcDataType.UDInt or PlcDataType.DWord => FromUInt32BE((uint)Math.Clamp(value, 0, uint.MaxValue)),
            PlcDataType.Real => FromFloatBE((float)value),
            PlcDataType.Bool => [(byte)(value != 0 ? 1 : 0)],
            _ => null,
        };
    }

    private static double DecodeFromPlc(byte[] data, PlcDataType dataType)
    {
        if (data.Length == 0) return 0;

        return dataType switch
        {
            PlcDataType.Byte or PlcDataType.USInt => data[0],
            PlcDataType.SInt => (sbyte)data[0],
            PlcDataType.Int => ToInt16BE(data),
            PlcDataType.UInt or PlcDataType.Word => ToUInt16BE(data),
            PlcDataType.DInt => ToInt32BE(data),
            PlcDataType.UDInt or PlcDataType.DWord => ToUInt32BE(data),
            PlcDataType.Real => ToFloatBE(data),
            PlcDataType.Bool => data[0] != 0 ? 1 : 0,
            _ => 0,
        };
    }

    private static int GetDataTypeSize(PlcDataType dataType) => dataType switch
    {
        PlcDataType.Byte or PlcDataType.USInt or PlcDataType.SInt or PlcDataType.Bool => 1,
        PlcDataType.Int or PlcDataType.UInt or PlcDataType.Word => 2,
        PlcDataType.DInt or PlcDataType.UDInt or PlcDataType.DWord or PlcDataType.Real => 4,
        _ => 4,
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
    public string ProductCode { get; init; } = "";
    public string Author { get; init; } = "";
    public RecipeStatus Status { get; init; } = RecipeStatus.Draft;
    public int Version { get; init; }
    public string Category { get; init; } = "";
    public List<string> Tags { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime ModifiedAt { get; init; }
    public int ParameterCount { get; init; }
}
