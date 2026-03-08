using ExpressRecipe.BlazorWeb.Components;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.Client.Shared.Services.LocalStorage;
using ExpressRecipe.Client.Shared.Services.SignalR;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

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

// Register HTTP clients for each microservice with Aspire service discovery
builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
{
    client.BaseAddress = new Uri("http://authservice");
});

builder.Services.AddHttpClient<IProductApiClient, ProductApiClient>(client =>
{
    client.BaseAddress = new Uri("http://productservice");
});

// Register AdminApiClient with IHttpClientFactory for multi-service communication
builder.Services.AddScoped<IAdminApiClient, AdminApiClient>();

// Register named HttpClients for AdminApiClient to use
builder.Services.AddHttpClient("ProductService", client =>
{
    client.BaseAddress = new Uri("http://productservice");
});

builder.Services.AddHttpClient("RecallService", client =>
{
    client.BaseAddress = new Uri("http://recallservice");
});

builder.Services.AddHttpClient<IRecipeApiClient, RecipeApiClient>(client =>
{
    client.BaseAddress = new Uri("http://recipeservice");
});

builder.Services.AddHttpClient<IUserProfileApiClient, UserProfileApiClient>(client =>
{
    client.BaseAddress = new Uri("http://userservice");
});

// IngredientService client - REST API only (gRPC disabled until HTTP/2 issues resolved)
builder.Services.AddHttpClient<IngredientServiceClient>(client =>
{
    client.BaseAddress = new Uri("http://ingredientservice");
});

builder.Services.AddHttpClient<IInventoryApiClient, InventoryApiClient>(client =>
{
    client.BaseAddress = new Uri("http://inventoryservice");
});

builder.Services.AddHttpClient<IShoppingListApiClient, ShoppingListApiClient>(client =>
{
    client.BaseAddress = new Uri("http://shoppingservice");
});

builder.Services.AddHttpClient<IMealPlanApiClient, MealPlanApiClient>(client =>
{
    client.BaseAddress = new Uri("http://mealplanningservice");
});

builder.Services.AddHttpClient<INotificationApiClient, NotificationApiClient>(client =>
{
    client.BaseAddress = new Uri("http://notificationservice");
});

builder.Services.AddHttpClient<IAnalyticsApiClient, AnalyticsApiClient>(client =>
{
    client.BaseAddress = new Uri("http://analyticsservice");
});

builder.Services.AddHttpClient<ICommunityApiClient, CommunityApiClient>(client =>
{
    client.BaseAddress = new Uri("http://communityservice");
});

builder.Services.AddHttpClient<IPriceApiClient, PriceApiClient>(client =>
{
    client.BaseAddress = new Uri("http://priceservice");
});

builder.Services.AddHttpClient<IAIApiClient, AIApiClient>(client =>
{
    client.BaseAddress = new Uri("http://aiservice");
});

builder.Services.AddHttpClient<IScannerApiClient, ScannerApiClient>(client =>
{
    client.BaseAddress = new Uri("http://scannerservice");
});

builder.Services.AddHttpClient<IVisionApiClient, VisionApiClient>(client =>
{
    client.BaseAddress = new Uri("http://visionservice");
});

builder.Services.AddHttpClient<IGroceryStoreApiClient, GroceryStoreApiClient>(client =>
{
    client.BaseAddress = new Uri("http://grocerystoreservice");
});

builder.Services.AddHttpClient<IRecallApiClient, RecallApiClient>(client =>
{
    client.BaseAddress = new Uri("http://recallservice");
});

builder.Services.AddHttpClient<ISearchApiClient, SearchApiClient>(client =>
{
    client.BaseAddress = new Uri("http://searchservice");
});

builder.Services.AddHttpClient<ISyncApiClient, SyncApiClient>(client =>
{
    client.BaseAddress = new Uri("http://syncservice");
});

builder.Services.AddHttpClient<ICookbookApiClient, CookbookApiClient>(client =>
{
    client.BaseAddress = new Uri("http://cookbookservice");
});

// Register local storage services
builder.Services.AddScoped(typeof(LocalStorageRepository<>));
builder.Services.AddScoped<SyncQueueService>();
builder.Services.AddScoped<OfflineDetectionService>();

// Register recipe file and sync services
builder.Services.AddScoped<ExpressRecipe.BlazorWeb.Services.RecipeFileService>();
builder.Services.AddScoped<ExpressRecipe.BlazorWeb.Services.RecipeSyncService>();

// Register SignalR client services (these would be initialized per-user)
builder.Services.AddScoped<NotificationHubClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<NotificationHubClient>>();
    var toast = sp.GetRequiredService<IToastService>();
    var tokenProvider = sp.GetRequiredService<ITokenProvider>();

    var hubUrl = "http://notificationservice/hubs/notifications";
    var token = tokenProvider.GetAccessTokenAsync().Result; // Get auth token

    return new NotificationHubClient(hubUrl, token, logger, toast);
});

builder.Services.AddScoped<SyncHubClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SyncHubClient>>();
    var tokenProvider = sp.GetRequiredService<ITokenProvider>();

    var hubUrl = "http://syncservice/hubs/sync";
    var token = tokenProvider.GetAccessTokenAsync().Result;

    return new SyncHubClient(hubUrl, token, logger);
});

// Register AuthenticationStateProvider - register both base type and concrete type
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthenticationStateProvider>());

var app = builder.Build();

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
