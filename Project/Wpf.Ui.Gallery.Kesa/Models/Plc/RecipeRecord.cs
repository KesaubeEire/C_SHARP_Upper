using System.Text.Json.Serialization;

namespace Wpf.Ui.Gallery.Models.Plc;

public class RecipeRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public int Version { get; set; } = 1;

    /// <summary>Tags/labels for filtering, e.g. "加热", "冷却", "标准"</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Category for grouping, e.g. "温度配方", "压力配方"</summary>
    public string Category { get; set; } = "";

    /// <summary>Default DB number for this recipe's parameters</summary>
    public int DefaultDbNumber { get; set; } = 1;

    /// <summary>Default PLC data area (DB by default)</summary>
    public int DefaultArea { get; set; } = 4; // S7Area.DB

    [JsonIgnore]
    public List<RecipeParameter> Parameters { get; set; } = [];
}
