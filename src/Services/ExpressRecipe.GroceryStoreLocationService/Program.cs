using ExpressRecipe.Data.Common;
using ExpressRecipe.GroceryStoreLocationService.Data;
using ExpressRecipe.GroceryStoreLocationService.Services;
using ExpressRecipe.GroceryStoreLocationService.Workers;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ExpressRecipe.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add SQL Server via Aspire
builder.AddSqlServerClient("grocerystoredb");

// Add Redis for distributed caching
builder.AddRedisClient("cache");

// Add hybrid caching (memory + Redis)
builder.AddHybridCache();

// Register hybrid cache service
builder.Services.AddSingleton<HybridCacheService>();

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

// Register database connection and repository
var connectionString = builder.Configuration.GetConnectionString("grocerystoredb")
    ?? throw new InvalidOperationException("Database connection string 'grocerystoredb' not found");

builder.Services.AddScoped<IGroceryStoreRepository>(sp =>
{
    var cache = sp.GetRequiredService<HybridCacheService>();
    var logger = sp.GetRequiredService<ILogger<GroceryStoreRepository>>();
    return new GroceryStoreRepository(connectionString, cache, logger);
});

// Register import services with HttpClient
builder.Services.AddHttpClient<UsdaSnapImportService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ExpressRecipe.GroceryStoreLocationService/1.0 (+https://github.com/rhale78/ExpressRecipe)");
    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/csv,application/octet-stream,*/*");
})
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<OpenStreetMapImportService>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IOpenPricesLocationImportService, OpenPricesLocationImportService>()
    .AddStandardResilienceHandler();

// Register background worker as singleton so it can be injected into controllers
builder.Services.AddSingleton<StoreLocationImportWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StoreLocationImportWorker>());

// Add controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database migrations
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(connectionString, migrations);

// Configure middleware pipeline
app.MapDefaultEndpoints();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
