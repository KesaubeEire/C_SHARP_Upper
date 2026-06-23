using System.Text.Json;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

/// <summary>
/// Manages recipe CRUD operations with JSON file persistence.
/// Each recipe is stored as a JSON file in the recipes directory.
/// </summary>
public class RecipeService
{
    private readonly string _recipesDir;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public RecipeService()
    {
        _recipesDir = Path.Combine(AppContext.BaseDirectory, "recipes");
        Directory.CreateDirectory(_recipesDir);
    }

    /// <summary>Get all recipe names and basic info.</summary>
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

    /// <summary>Load a recipe by ID (file name without extension).</summary>
    public RecipeRecord? LoadRecipe(string id)
    {
        var path = GetFilePath(id);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RecipeRecord>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Save a recipe (create or update).</summary>
    public void SaveRecipe(RecipeRecord recipe)
    {
        recipe.ModifiedAt = DateTime.Now;
        recipe.Version++;
        var path = GetFilePath(recipe.Id);
        var json = JsonSerializer.Serialize(recipe, _jsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>Delete a recipe by ID.</summary>
    public bool DeleteRecipe(string id)
    {
        var path = GetFilePath(id);
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    /// <summary>Create a copy of an existing recipe with a new name.</summary>
    public RecipeRecord? CopyRecipe(string sourceId, string newName)
    {
        var source = LoadRecipe(sourceId);
        if (source is null)
            return null;

        var copy = new RecipeRecord
        {
            Name = newName,
            Description = source.Description,
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
            }).ToList(),
        };

        SaveRecipe(copy);
        return copy;
    }

    private string GetFilePath(string id) =>
        Path.Combine(_recipesDir, $"{id}.json");
}

public class RecipeMeta
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ModifiedAt { get; init; }
    public int ParameterCount { get; init; }
}
