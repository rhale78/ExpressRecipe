using ExpressRecipe.MAUI.ViewModels;

namespace ExpressRecipe.MAUI.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnScannerTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//scanner");
    }

    private async void OnInventoryTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//inventory");
    }

    private async void OnShoppingTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//shopping");
    }

    private async void OnRecipesTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//recipes");
    }
}
