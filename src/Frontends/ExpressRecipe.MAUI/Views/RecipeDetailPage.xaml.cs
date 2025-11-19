using ExpressRecipe.MAUI.ViewModels;

namespace ExpressRecipe.MAUI.Views;

public partial class RecipeDetailPage : ContentPage
{
    public RecipeDetailPage(RecipeDetailPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
