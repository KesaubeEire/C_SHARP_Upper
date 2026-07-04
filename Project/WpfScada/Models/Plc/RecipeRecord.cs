using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace WpfScada.Models.Plc;

public class RecipeRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // ===== New metadata fields =====

    /// <summary>Product/code identifier for this recipe.</summary>
    public string ProductCode { get; set; } = "";

    /// <summary>Operator or engineer who created/modified this recipe.</summary>
    public string Author { get; set; } = "";

    /// <summary>Lifecycle status of the recipe.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;

    // ===== Timestamps & version =====

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public int Version { get; set; } = 1;

    // ===== Tags & category =====

    /// <summary>Tags/labels for filtering, e.g. "加热", "冷却", "标准"</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Category for grouping, e.g. "温度配方", "压力配方"</summary>
    public string Category { get; set; } = "";

    // ===== PLC configuration =====

    /// <summary>Default DB number for this recipe's parameters</summary>
    public int DefaultDbNumber { get; set; } = 1;

    /// <summary>Default PLC data area (DB by default)</summary>
    public int DefaultArea { get; set; } = 4; // S7Area.DB

    // ===== Parameter storage =====

    /// <summary>
    /// Parameter groups (new format).
    /// Each group contains a named set of parameters, displayed as a tab in the UI.
    /// </summary>
    public List<RecipeGroup> Groups { get; set; } = [];

    /// <summary>
    /// Flat parameters list — for backward compatibility with old JSON format.
    /// <para>
    /// GET: Flattens all parameters from <see cref="Groups"/> into one list.
    /// SET: If the JSON had a flat "Parameters" array (no "Groups"), migrates them
    /// into a single default group. Ignores writes when serializing (handled via
    /// <see cref="ShouldSerializeParameters"/>).
    /// </para>
    /// <para>
    /// When saving, new JSON files are written with <see cref="Groups"/> only.
    /// Old JSON files without "Groups" are migrated on first load.
    /// </para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [SuppressMessage("SonarAnalyzer.CSharp", "S2365", Justification = "Required for JSON backward compat: flattens Groups into flat list on read.")]
    public List<RecipeParameter>? Parameters
    {
        get
        {
            // Flatten all groups into a single list for backward-compatible access
            if (Groups.Count > 0)
                return Groups.SelectMany(g => g.Parameters).ToList();

            return null;
        }
        set
        {
            if (value is null || value.Count == 0)
            {
                // Keep existing groups or ensure at least one default group
                if (Groups.Count == 0)
                    Groups = [new RecipeGroup { Name = "参数组1" }];
                return;
            }

            // Check if we already have Groups with content (avoid overwriting on serialize/deserialize round-trip)
            if (Groups.Count > 0 && Groups.Sum(g => g.Parameters.Count) > 0)
                return;

            // Migrate flat parameters into a single default group
            Groups =
            [
                new RecipeGroup
                {
                    Name = "参数组1",
                    Parameters = new System.Collections.ObjectModel.ObservableCollection<RecipeParameter>(value),
                },
            ];
        }
    }

    /// <summary>
    /// Controls serialization of <see cref="Parameters"/>.
    /// Returns true only when Groups is empty (old format), so new saves never write the flat list.
    /// </summary>
    public bool ShouldSerializeParameters() => Groups.Count == 0;
}
