using Wpf.Ui.Controls;

namespace Wpf.Ui.Gallery.Models.Navigation;

/// <summary>
/// Represents a node in the sidebar TreeView navigation.
/// Supports unlimited nesting via <see cref="Children"/>.
/// </summary>
public partial class SidebarEntry : ObservableObject
{
    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private IconElement? _icon;

    [ObservableProperty]
    private Type? _targetPageType;

    [ObservableProperty]
    private ObservableCollection<SidebarEntry> _children = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSeparator;

    /// <summary>
    /// Whether this item is the currently active/selected page.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Optional custom content to render instead of the standard item template.
    /// Used for the PLC connection panel.
    /// </summary>
    [ObservableProperty]
    private object? _customContent;
}
