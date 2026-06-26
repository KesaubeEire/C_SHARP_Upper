using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>
/// A named group of recipe parameters.
/// Maps to a tab in the recipe editor (e.g. "加热段", "保压段", "冷却段").
/// </summary>
public partial class RecipeGroup : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    /// <summary>Parameters belonging to this group.</summary>
    public ObservableCollection<RecipeParameter> Parameters { get; set; } = [];

    /// <summary>Number of parameters in this group (for UI display).</summary>
    public int ParameterCount => Parameters.Count;
}
