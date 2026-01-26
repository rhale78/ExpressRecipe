using ExpressRecipe.BlazorWeb.Components;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.Client.Shared.Services.LocalStorage;
using ExpressRecipe.Client.Shared.Services.SignalR;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container - Enable both Server and WebAssembly render modes
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add output caching with Redis
builder.AddRedisOutputCache("redis");

// Add Blazored LocalStorage for token management
builder.Services.AddBlazoredLocalStorage();

// Add authentication support
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();

// Register token provider
builder.Services.AddScoped<ITokenProvider, LocalStorageTokenProvider>();

// Register toast notification service
builder.Services.AddSingleton<IToastService, ToastService>();

// Helper method to get service URL with fallback for development
string GetServiceUrl(string serviceName)
{
    // Try to get from configuration (appsettings.Development.json)
    var configUrl = builder.Configuration[$"Services:{serviceName}"];
    if (!string.IsNullOrEmpty(configUrl))
    {
        return configUrl;
    }
    
    // Fallback to Aspire service discovery format (works when running via AppHost)
    return $"http://{serviceName.ToLowerInvariant()}";
}

// Register HTTP clients for each microservice with Aspire service discovery
builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
{
    // Aspire service discovery will automatically resolve "http://authservice" when running in AppHost
    client.BaseAddress = new Uri(GetServiceUrl("AuthService"));
});

builder.Services.AddHttpClient<IProductApiClient, ProductApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("ProductService"));
});

// Register AdminApiClient with IHttpClientFactory for multi-service communication
builder.Services.AddScoped<IAdminApiClient, AdminApiClient>();

// Register named HttpClients for AdminApiClient to use
builder.Services.AddHttpClient("ProductService", client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("ProductService"));
});

builder.Services.AddHttpClient("RecallService", client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("RecallService"));
});

builder.Services.AddHttpClient<IRecipeApiClient, RecipeApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("RecipeService"));
});

builder.Services.AddHttpClient<IUserProfileApiClient, UserProfileApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("UserService"));
});

builder.Services.AddHttpClient<IInventoryApiClient, InventoryApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("InventoryService"));
});

builder.Services.AddHttpClient<IShoppingListApiClient, ShoppingListApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("ShoppingService"));
});

builder.Services.AddHttpClient<IMealPlanApiClient, MealPlanApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("MealPlanningService"));
});

builder.Services.AddHttpClient<INotificationApiClient, NotificationApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("NotificationService"));
});

builder.Services.AddHttpClient<IAnalyticsApiClient, AnalyticsApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("AnalyticsService"));
});

builder.Services.AddHttpClient<ICommunityApiClient, CommunityApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("CommunityService"));
});

builder.Services.AddHttpClient<IPriceApiClient, PriceApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("PriceService"));
});

builder.Services.AddHttpClient<IAIApiClient, AIApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("AIService"));
});

builder.Services.AddHttpClient<IScannerApiClient, ScannerApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("ScannerService"));
});

builder.Services.AddHttpClient<IRecallApiClient, RecallApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("RecallService"));
});

builder.Services.AddHttpClient<ISearchApiClient, SearchApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("SearchService"));
});

builder.Services.AddHttpClient<ISyncApiClient, SyncApiClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("SyncService"));
});

// Register local storage services
builder.Services.AddScoped(typeof(LocalStorageRepository<>));
builder.Services.AddScoped<SyncQueueService>();
builder.Services.AddScoped<OfflineDetectionService>();

// Register SignalR client services (these would be initialized per-user)
builder.Services.AddScoped<NotificationHubClient>(sp =>
{
    ILogger<NotificationHubClient> logger = sp.GetRequiredService<ILogger<NotificationHubClient>>();
    IToastService toast = sp.GetRequiredService<IToastService>();
    ITokenProvider tokenProvider = sp.GetRequiredService<ITokenProvider>();

    var hubUrl = $"{GetServiceUrl("NotificationService")}/hubs/notifications";
    var token = tokenProvider.GetAccessTokenAsync().Result; // Get auth token

    return new NotificationHubClient(hubUrl, token, logger, toast);
});

builder.Services.AddScoped<SyncHubClient>(sp =>
{
    ILogger<SyncHubClient> logger = sp.GetRequiredService<ILogger<SyncHubClient>>();
    ITokenProvider tokenProvider = sp.GetRequiredService<ITokenProvider>();

    var hubUrl = $"{GetServiceUrl("SyncService")}/hubs/sync";
    var token = tokenProvider.GetAccessTokenAsync().Result;

    return new SyncHubClient(hubUrl, token, logger);
});

// Register AuthenticationStateProvider - register both base type and concrete type
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthenticationStateProvider>());

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ExpressRecipe.Client.Shared.Services.IAuthService).Assembly);

app.Run();
