using CommunityToolkit.Maui;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.MAUI.Services;
using ExpressRecipe.MAUI.Services.AI;
using ExpressRecipe.MAUI.Services.Camera;
using ExpressRecipe.MAUI.ViewModels;
using ExpressRecipe.MAUI.Views;
using FFImageLoading.Maui;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace ExpressRecipe.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseFFImageLoading()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register core services
        ConfigureServices(builder.Services, builder.Configuration);

        // Register views and view models
        RegisterViewsAndViewModels(builder.Services);

        return builder.Build();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // API Base URLs - configurable per environment
        var apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://api.expressrecipe.com";

        // SQLite Database
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "expressrecipe.db");
        services.AddSingleton<ISQLiteDatabase>(new SQLiteDatabase(dbPath));

        // Token provider for authentication
        services.AddSingleton<ITokenProvider, SecureStorageTokenProvider>();

        // HTTP Client Factory
        services.AddHttpClient();

        // API Clients (reusing from Client.Shared)
        services.AddHttpClient<IAuthService, AuthService>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/auth");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IProductApiClient, ProductApiClient>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/products");
        });

        services.AddHttpClient<IRecipeApiClient, RecipeApiClient>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/recipes");
        });

        services.AddHttpClient<IUserProfileApiClient, UserProfileApiClient>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/users");
        });

        services.AddHttpClient<IInventoryApiClient, InventoryApiClient>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/inventory");
        });

        services.AddHttpClient<IShoppingListApiClient, ShoppingListApiClient>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/shopping");
        });

        services.AddHttpClient<IMealPlanApiClient, MealPlanApiClient>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/mealplans");
        });

        services.AddHttpClient<INotificationApiClient, NotificationApiClient>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/notifications");
        });

        services.AddHttpClient<IScannerApiClient, ScannerApiClient>(client =>
        {
            client.BaseAddress = new Uri($"{apiBaseUrl}/scanner");
        });

        // MAUI-specific services
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<IProductRecognitionService, ProductRecognitionService>();
        services.AddSingleton<IOllamaService, OllamaService>();
        services.AddSingleton<ICloudAIService, CloudAIService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // SignalR Hubs
        services.AddSingleton<INotificationHubService, NotificationHubService>();
        services.AddSingleton<ISyncHubService, SyncHubService>();

        // Offline sync service
        services.AddSingleton<IOfflineSyncService, OfflineSyncService>();
    }

    private static void RegisterViewsAndViewModels(IServiceCollection services)
    {
        // Shell
        services.AddSingleton<AppShell>();

        // Main Pages
        services.AddTransient<MainPage>();
        services.AddTransient<MainViewModel>();

        services.AddTransient<ScannerPage>();
        services.AddTransient<ScannerViewModel>();

        services.AddTransient<ProductDetailPage>();
        services.AddTransient<ProductDetailViewModel>();

        services.AddTransient<InventoryPage>();
        services.AddTransient<InventoryViewModel>();

        services.AddTransient<ShoppingListPage>();
        services.AddTransient<ShoppingListViewModel>();

        services.AddTransient<RecipesPage>();
        services.AddTransient<RecipesViewModel>();

        services.AddTransient<RecipeDetailPage>();
        services.AddTransient<RecipeDetailViewModel>();

        services.AddTransient<MealPlanPage>();
        services.AddTransient<MealPlanViewModel>();

        services.AddTransient<RecallAlertsPage>();
        services.AddTransient<RecallAlertsViewModel>();

        services.AddTransient<ProfilePage>();
        services.AddTransient<ProfileViewModel>();

        services.AddTransient<SettingsPage>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<LoginPage>();
        services.AddTransient<LoginViewModel>();

        services.AddTransient<SearchPage>();
        services.AddTransient<SearchViewModel>();
    }
}
