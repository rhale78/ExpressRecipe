using System.Net.Sockets;
using System.Text;
using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.Messaging.Saga.Extensions;
using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Saga;
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
// This is the REST fallback – overridden by MessagingProductServiceClient when messaging is on.
// Always register the named HTTP client (used by MessagingProductServiceClient fallback path).
builder.Services.AddHttpClient<ProductServiceClient>(client =>
{
    // Use Aspire service name - service discovery will resolve to actual endpoint
    client.BaseAddress = new Uri("http://productservice");
    client.Timeout = TimeSpan.FromSeconds(5); // Short timeout - don't block price imports
})
.AddServiceDiscovery();

builder.Services.AddSingleton<HybridCacheService>();
builder.Services.AddSingleton<ExpressRecipe.Shared.Services.CacheService>();

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

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

// Register new external price API clients (all disabled by default via config)
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<KrogerApiClient>();
builder.Services.AddHttpClient<FlippApiClient>();
builder.Services.AddHttpClient<FoodLionApiClient>();
// Register all external price API clients as IExternalPriceApiClient so they can be
// resolved as IEnumerable<IExternalPriceApiClient>. Each client checks IsEnabled at runtime.
builder.Services.AddScoped<IExternalPriceApiClient>(sp => sp.GetRequiredService<GoogleShoppingApiClient>());
builder.Services.AddScoped<IExternalPriceApiClient>(sp => sp.GetRequiredService<KrogerApiClient>());
builder.Services.AddScoped<IExternalPriceApiClient>(sp => sp.GetRequiredService<FlippApiClient>());
builder.Services.AddScoped<IExternalPriceApiClient>(sp => sp.GetRequiredService<FoodLionApiClient>());

// Register unit normalizer and effective price calculator
builder.Services.AddSingleton<IPriceUnitNormalizer, PriceUnitNormalizer>();
builder.Services.AddSingleton<IEffectivePriceCalculator, EffectivePriceCalculator>();

// Register new CSV import services
builder.Services.AddScoped<UsdaFmapImportService>();
builder.Services.AddScoped<BlsPriceImportService>();
builder.Services.AddScoped<WalmartKaggleImportService>();
builder.Services.AddScoped<CostcoKaggleImportService>();

// Register background workers
builder.Services.AddHostedService<PriceAnalysisWorker>();
// Register PriceDataImportWorker as singleton so it can be injected into controllers
builder.Services.AddSingleton<PriceDataImportWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceDataImportWorker>());

// Register new CSV import workers (each reads Enabled flag from config)
builder.Services.AddHostedService<UsdaFmapImportWorker>();
builder.Services.AddHostedService<BlsPriceImportWorker>();
builder.Services.AddHostedService<WalmartKaggleImportWorker>();
builder.Services.AddHostedService<CostcoKaggleImportWorker>();

// Add controllers
builder.Services.AddControllers();

// Register RabbitMQ messaging (IMessageBus) - conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");

    // Use messaging-based product lookup (request/response) instead of REST when messaging is on
    builder.Services.AddSingleton<IProductServiceClient>(sp =>
        new MessagingProductServiceClient(
            sp.GetRequiredService<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus>(),
            sp.GetRequiredService<ProductServiceClient>(),
            sp.GetRequiredService<ILogger<MessagingProductServiceClient>>(),
            sp.GetRequiredService<IConfiguration>()));

    // Real event publisher – publishes to RabbitMQ
    builder.Services.AddSingleton<IPriceEventPublisher>(sp =>
        new PriceEventPublisher(
            sp.GetRequiredService<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus>(),
            sp.GetRequiredService<ILogger<PriceEventPublisher>>()));

    // Subscribe to ProductService lifecycle events so price data stays consistent
    builder.Services.AddHostedService<ProductEventSubscriber>();

    // Register the price-processing saga workflow
    builder.Services.AddSqlSagaRepository<PriceProcessingSagaState>(connectionString, "PriceProcessingSagaState");
    builder.Services.AddSagaWorkflow(PriceProcessingWorkflow.Build());
}
else
{
    // Messaging disabled/unavailable: fall back to REST HTTP calls to ProductService
    builder.Services.AddScoped<IProductServiceClient>(sp =>
        new ProductServiceClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ProductServiceClient)),
            sp.GetRequiredService<ILogger<ProductServiceClient>>()));

    // Null publisher – logs events at Debug level so they remain observable; no bus needed
    builder.Services.AddSingleton<IPriceEventPublisher>(sp =>
        new NullPriceEventPublisher(sp.GetRequiredService<ILogger<NullPriceEventPublisher>>()));
}

// Register price batch channel (async batch path) – always available regardless of messaging
builder.Services.AddSingleton<IPriceBatchChannel, PriceBatchChannel>();
builder.Services.AddHostedService<PriceBatchChannelWorker>();

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
