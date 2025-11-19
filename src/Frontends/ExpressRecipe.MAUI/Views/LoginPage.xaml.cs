using ExpressRecipe.MAUI.ViewModels;

namespace ExpressRecipe.MAUI.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
