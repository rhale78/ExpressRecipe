using ExpressRecipe.Data.Common;
using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using ExpressRecipe.PriceService.Workers;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"] ?? "http://localhost:5000";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

// SQL Server via Aspire
builder.AddSqlServerClient("pricedb");

// Redis cache via Aspire
builder.AddRedisClient("cache");

// Memory cache + HybridCacheService
builder.AddHybridCache();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<HybridCacheService>();

// Register database connection string for direct use
var connectionString = builder.Configuration.GetConnectionString("pricedb")
    ?? throw new InvalidOperationException("Database connection string 'pricedb' not found");

// Register repositories
builder.Services.AddScoped<IPriceRepository>(sp =>
    new PriceRepository(
        connectionString,
        sp.GetService<HybridCacheService>(),
        sp.GetService<ILogger<PriceRepository>>()));

// Register import services
builder.Services.AddHttpClient<OpenPricesImportService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<OpenPricesImportService>(sp =>
    new OpenPricesImportService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenPricesImportService)),
        sp.GetRequiredService<IPriceRepository>(),
        sp.GetRequiredService<ILogger<OpenPricesImportService>>(),
        sp.GetRequiredService<IConfiguration>()));

builder.Services.AddHttpClient<GroceryDbImportService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<GroceryDbImportService>(sp =>
    new GroceryDbImportService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GroceryDbImportService)),
        sp.GetRequiredService<IPriceRepository>(),
        sp.GetRequiredService<ILogger<GroceryDbImportService>>(),
        sp.GetRequiredService<IConfiguration>()));

// Register price scraping services
builder.Services.AddHttpClient<PriceScraperService>();
builder.Services.AddHttpClient<GoogleShoppingApiClient>();

// Register background workers
builder.Services.AddHostedService<PriceAnalysisWorker>();
// Register PriceDataImportWorker as singleton so it can be injected into controllers
builder.Services.AddSingleton<PriceDataImportWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceDataImportWorker>());

// Add controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("PriceService", "pricedb");

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

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
