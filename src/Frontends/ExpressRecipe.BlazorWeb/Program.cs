using ExpressRecipe.BlazorWeb.Components;
using ExpressRecipe.Client.Shared.Services;
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

// Register HTTP clients for each microservice with service discovery
builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AuthService"] ?? "http://authservice");
});

builder.Services.AddHttpClient<IProductApiClient, ProductApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ProductService"] ?? "http://productservice");
});

builder.Services.AddHttpClient<IAdminApiClient, AdminApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ProductService"] ?? "http://productservice");
});

builder.Services.AddHttpClient<IRecipeApiClient, RecipeApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:RecipeService"] ?? "http://recipeservice");
});

builder.Services.AddHttpClient<IUserProfileApiClient, UserProfileApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:UserService"] ?? "http://userservice");
});

builder.Services.AddHttpClient<IInventoryApiClient, InventoryApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:InventoryService"] ?? "http://inventoryservice");
});

builder.Services.AddHttpClient<IShoppingListApiClient, ShoppingListApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ShoppingService"] ?? "http://shoppingservice");
});

builder.Services.AddHttpClient<IMealPlanApiClient, MealPlanApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:MealPlanningService"] ?? "http://mealplanningservice");
});

builder.Services.AddHttpClient<INotificationApiClient, NotificationApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService"] ?? "http://notificationservice");
});

builder.Services.AddHttpClient<IAnalyticsApiClient, AnalyticsApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AnalyticsService"] ?? "http://analyticsservice");
});

builder.Services.AddHttpClient<ICommunityApiClient, CommunityApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:CommunityService"] ?? "http://communityservice");
});

builder.Services.AddHttpClient<IPriceApiClient, PriceApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PriceService"] ?? "http://priceservice");
});

builder.Services.AddHttpClient<IAIApiClient, AIApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AIService"] ?? "http://aiservice");
});

builder.Services.AddHttpClient<IScannerApiClient, ScannerApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ScannerService"] ?? "http://scannerservice");
});

builder.Services.AddHttpClient<IRecallApiClient, RecallApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:RecallService"] ?? "http://recallservice");
});

builder.Services.AddHttpClient<ISearchApiClient, SearchApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:SearchService"] ?? "http://searchservice");
});

builder.Services.AddHttpClient<ISyncApiClient, SyncApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:SyncService"] ?? "http://syncservice");
});

// Register custom authentication state provider
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>(sp => 
    (CustomAuthenticationStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseOutputCache();

// Map Razor components with Auto render mode (Server → WASM)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();

app.Run();
