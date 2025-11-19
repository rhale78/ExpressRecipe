using ExpressRecipe.MAUI.Views;

namespace ExpressRecipe.MAUI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("productdetail", typeof(ProductDetailPage));
        Routing.RegisterRoute("recipedetail", typeof(RecipeDetailPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("profile", typeof(ProfilePage));
        Routing.RegisterRoute("login", typeof(LoginPage));
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//settings");
    }

    private async void OnProfileClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//profile");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        // Clear token and navigate to login
        await SecureStorage.Default.SetAsync("auth_token", string.Empty);
        await Shell.Current.GoToAsync("//login");
    }
}
