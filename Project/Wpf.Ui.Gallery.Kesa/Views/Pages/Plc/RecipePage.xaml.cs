using System.Windows.Controls;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

[GalleryPage("配方管理", SymbolRegular.DocumentData24)]
public partial class RecipePage : Page
{
    public RecipePage(RecipeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
