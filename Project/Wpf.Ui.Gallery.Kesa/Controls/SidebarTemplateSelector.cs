using System.Windows.Controls;
using Wpf.Ui.Gallery.Models.Navigation;

namespace Wpf.Ui.Gallery.Controls;

/// <summary>
/// Selects a DataTemplate for <see cref="SidebarEntry"/> based on its properties.
/// </summary>
public class SidebarTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Template for leaf items (no children, no custom content, not a separator).
    /// </summary>
    public DataTemplate? LeafTemplate { get; set; }

    /// <summary>
    /// Template for separator items.
    /// </summary>
    public DataTemplate? SeparatorTemplate { get; set; }

    /// <summary>
    /// Template for items with custom content (e.g. PLC connection panel).
    /// </summary>
    public DataTemplate? CustomContentTemplate { get; set; }

    /// <summary>
    /// Default fallback template.
    /// </summary>
    public DataTemplate? DefaultTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is SidebarEntry entry)
        {
            if (entry.IsSeparator)
                return SeparatorTemplate!;

            if (entry.CustomContent is not null)
                return CustomContentTemplate!;

            if (entry.Children.Count == 0 && entry.TargetPageType is not null)
                return LeafTemplate!;
        }

        return DefaultTemplate!;
    }
}
