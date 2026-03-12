using ExpressRecipe.Data.Common;
using ExpressRecipe.ShoppingService.Data;
using ExpressRecipe.ShoppingService.Services;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Units;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication (shared JWT bearer configuration)
builder.AddExpressRecipeAuthentication();
builder.Services.AddAuthorization();

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("shoppingdb")
    ?? throw new InvalidOperationException("Database connection string 'shoppingdb' not found");

builder.Services.AddHttpClient();

// Register repositories
builder.Services.AddScoped<IShoppingRepository>(sp =>
    new ShoppingRepository(
        connectionString,
        sp.GetRequiredService<ILogger<ShoppingRepository>>(),
        sp.GetRequiredService<IHttpClientFactory>()));

// Register HTTP clients for external service integration
builder.Services.AddHttpClient("PriceService", client =>
{
    string baseUrl = builder.Configuration["Services:PriceService"] ?? "http://priceservice";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddHttpClient("InventoryService", client =>
{
    string baseUrl = builder.Configuration["Services:InventoryService"] ?? "http://inventoryservice";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddHttpClient("RecipeService", client =>
{
    string baseUrl = builder.Configuration["Services:RecipeService"] ?? "http://recipeservice";
    client.BaseAddress = new Uri(baseUrl);
});

// Register optimization and session services
builder.Services.AddScoped<IShoppingOptimizationService, ShoppingOptimizationService>();
builder.Services.AddScoped<IShoppingSessionService, ShoppingSessionService>();

// Register unit conversion (uses HttpIngredientDensityResolver to call ProductService)
var shoppingProductServiceUrl = builder.Configuration["Services:ProductService:BaseUrl"]
    ?? builder.Configuration["services__productservice__http__0"]
    ?? "http://productservice";
builder.Services.AddHttpClient<IIngredientDensityResolver, HttpIngredientDensityResolver>(client =>
{
    client.BaseAddress = new Uri(shoppingProductServiceUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<IUnitConversionService>(sp =>
    new UnitConversionService(sp.GetRequiredService<IIngredientDensityResolver>()));

// Add controllers
builder.Services.AddControllers();

// Add Swagger
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new() { Title = "ExpressRecipe.ShoppingService API", Version = "v1" });
// });

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("ShoppingService", "shoppingdb");

// Run migrations using shared MigrationRunner
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(connectionString, migrations);

// Configure middleware pipeline
app.MapDefaultEndpoints(); // Aspire health checks
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiting(new RateLimitOptions
{
    Enabled = true,
    MaxRequestsPerWindow = 100,
    WindowSeconds = 60
});
app.MapControllers();

app.Run();
