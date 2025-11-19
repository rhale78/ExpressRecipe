using ExpressRecipe.MAUI.ViewModels;

namespace ExpressRecipe.MAUI.Views;

public partial class RecipesPage : ContentPage
{
    public RecipesPage(RecipesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
