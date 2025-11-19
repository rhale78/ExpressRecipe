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

// Register HTTP clients for each microservice with service discovery
builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AuthService"] ?? "http://authservice");
});

builder.Services.AddHttpClient<IProductApiClient, ProductApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ProductService"] ?? "http://productservice");
});

// Additional service registrations can follow the same pattern:
// builder.Services.AddHttpClient<IRecipeApiClient, RecipeApiClient>(...)
// builder.Services.AddHttpClient<IInventoryApiClient, InventoryApiClient>(...)
// etc.

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
