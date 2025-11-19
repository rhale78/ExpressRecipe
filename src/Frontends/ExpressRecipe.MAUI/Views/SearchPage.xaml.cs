using ExpressRecipe.MAUI.ViewModels;

namespace ExpressRecipe.MAUI.Views;

public partial class SearchPage : ContentPage
{
    public SearchPage(SearchPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
