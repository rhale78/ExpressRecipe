using ExpressRecipe.MAUI.ViewModels;

namespace ExpressRecipe.MAUI.Views;

public partial class ProductDetailPage : ContentPage
{
    public ProductDetailPage(ProductDetailPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
