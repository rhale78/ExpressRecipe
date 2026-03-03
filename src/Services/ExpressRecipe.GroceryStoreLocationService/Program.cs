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
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? "development-secret-key-change-in-production-min-32-chars-required!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "ExpressRecipe.AuthService",
            ValidAudience = jwtSettings["Audience"] ?? "ExpressRecipe.API",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

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
builder.Services.AddHttpClient<UsdaSnapImportService>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<OpenStreetMapImportService>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<OpenPricesLocationImportService>()
    .AddStandardResilienceHandler();

// Register background worker as singleton so it can be injected into controllers
builder.Services.AddSingleton<StoreLocationImportWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StoreLocationImportWorker>());

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

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
