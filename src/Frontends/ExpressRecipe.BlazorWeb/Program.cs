using ExpressRecipe.BlazorWeb.Components;
using ExpressRecipe.BlazorWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add output caching with Redis
builder.AddRedisOutputCache("redis");

// Add HTTP clients for API services
builder.Services.AddHttpClient("AuthService", client =>
{
    client.BaseAddress = new Uri("http://authservice");
});

builder.Services.AddHttpClient("UserService", client =>
{
    client.BaseAddress = new Uri("http://userservice");
});

builder.Services.AddHttpClient("ProductService", client =>
{
    client.BaseAddress = new Uri("http://productservice");
});

builder.Services.AddHttpClient("RecipeService", client =>
{
    client.BaseAddress = new Uri("http://recipeservice");
});

// Register application services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
