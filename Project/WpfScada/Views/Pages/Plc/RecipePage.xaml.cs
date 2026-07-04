using System.Windows.Controls;
using Wpf.Ui.Controls;
using WpfScada.ControlsLookup;
using WpfScada.ViewModels.Pages.Plc;

namespace WpfScada.Views.Pages.Plc;

[GalleryPage("配方管理", SymbolRegular.DocumentData24)]
public partial class RecipePage : Page
{
    public RecipePage(RecipeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
