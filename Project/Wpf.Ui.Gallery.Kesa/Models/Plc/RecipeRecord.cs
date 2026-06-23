namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>
/// Represents a named recipe — a collection of parameters that can be
/// downloaded to or uploaded from a PLC.
/// </summary>
public class RecipeRecord
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Human-readable recipe name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Optional description.</summary>
    public string Description { get; set; } = "";

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Last modification timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>Version number, incremented on each save.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The list of parameters in this recipe.</summary>
    public List<RecipeParameter> Parameters { get; set; } = [];
}
