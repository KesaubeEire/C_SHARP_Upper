namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>Metadata about a saved version snapshot of a recipe.</summary>
public class RecipeVersionSnapshot
{
    public string RecipeId { get; init; } = "";
    public int Version { get; init; }
    public DateTime SnapshotAt { get; init; }
    public string FilePath { get; init; } = "";
}
