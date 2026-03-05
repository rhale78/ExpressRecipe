using System.Text;
using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using ExpressRecipe.PriceService.Workers;
using ExpressRecipe.Shared.CQRS;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add database connection
builder.AddSqlServerClient("pricedb");

// Add Redis for caching
builder.AddRedisClient("redis");

// Add hybrid caching (memory + Redis)
builder.AddHybridCache();

// Register ProductServiceClient using Aspire service discovery
// This is optional - price import will work even if ProductService is unavailable
builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    // Use Aspire service name - service discovery will resolve to actual endpoint
    client.BaseAddress = new Uri("http://productservice");
    client.Timeout = TimeSpan.FromSeconds(5); // Short timeout - don't block price imports
})
.AddServiceDiscovery(); // Use Aspire service discovery - NO AuthenticationDelegatingHandler

builder.Services.AddSingleton<HybridCacheService>();
builder.Services.AddSingleton<ExpressRecipe.Shared.Services.CacheService>();
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? "development-secret-key-change-in-production-min-32-chars-required!";
if (builder.Environment.IsProduction() && (secretKey == "development-secret-key-change-in-production-min-32-chars-required!" || secretKey.Length < 32))
    throw new InvalidOperationException("[FATAL] JWT_SECRET_KEY must be configured in production and must be at least 32 characters.");

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

// Register token provider (service-to-service authentication)
builder.Services.AddScoped<ITokenProvider>(sp =>
    new ServiceTokenProvider("PriceService", builder.Configuration));

// Register authentication handler that adds tokens to HTTP requests
// NOTE: No longer using ConfigureHttpClientDefaults - ProductServiceClient doesn't need JWT auth
builder.Services.AddScoped<AuthenticationDelegatingHandler>();

// Register database connection string for direct use
var connectionString = builder.Configuration.GetConnectionString("pricedb")
    ?? throw new InvalidOperationException("Database connection string 'pricedb' not found");

// Register repositories
builder.Services.AddScoped<IPriceRepository>(sp =>
    new PriceRepository(
        connectionString,
        sp.GetService<HybridCacheService>(),
        sp.GetService<ILogger<PriceRepository>>()));

// Register OpenPrices import service
builder.Services.AddHttpClient<IOpenPricesImportService, OpenPricesImportService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ExpressRecipe.PriceService/1.0 (+https://github.com/rhale78/ExpressRecipe)");
    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
});
builder.Services.AddScoped<IOpenPricesImportService>(sp =>
    new OpenPricesImportService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IOpenPricesImportService)),
        sp.GetRequiredService<IProductServiceClient>(),
        sp.GetRequiredService<IPriceRepository>(),
        sp.GetRequiredService<ILogger<OpenPricesImportService>>(),
        sp.GetRequiredService<IConfiguration>()));

// Register batched product lookup service (reduces strain on ProductService)
builder.Services.AddSingleton<IBatchProductLookupService, BatchProductLookupService>();

// Register batched price insert service (improves throughput)
builder.Services.AddSingleton<IBatchPriceInsertService, BatchPriceInsertService>();

// Register dataflow-based import service (optional, for high-performance scenarios)
builder.Services.AddScoped<DataflowOpenPricesImportService>();

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

// Register RabbitMQ messaging (IMessageBus) - conditional based on configuration
var messagingEnabled = builder.Configuration.GetValue<bool>("Messaging:Enabled", false)
    || !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("messaging"))
    || !string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"]);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
}

// Add CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

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
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
