namespace ExpressRecipe.MAUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);

        // Set minimum window size for desktop platforms
        window.MinimumHeight = 600;
        window.MinimumWidth = 400;

        return window;
    }
}
