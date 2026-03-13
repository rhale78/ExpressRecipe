using ExpressRecipe.Data.Common;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Units;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication (shared JWT bearer configuration)
builder.AddExpressRecipeAuthentication();

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("inventorydb")
    ?? throw new InvalidOperationException("Database connection string 'inventorydb' not found");

// Register repositories
builder.Services.AddScoped<IInventoryRepository>(sp =>
    new InventoryRepository(connectionString,
        sp.GetRequiredService<ILogger<InventoryRepository>>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetService<HybridCacheService>()));

// HybridCache (L1 in-memory + optional L2 Redis)
builder.AddHybridCache();
builder.Services.AddSingleton<HybridCacheService>();

// Register HTTP clients for inter-service calls
builder.Services.AddHttpClient("RecipeService", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:RecipeService"] ?? "http://localhost:5102"));

builder.Services.AddScoped<IEquipmentRepository>(sp =>
    new EquipmentRepository(connectionString));

// Register equipment capability resolver
builder.Services.AddSingleton<IEquipmentCapabilityResolver>(sp =>
    new EquipmentCapabilityResolver(sp.GetRequiredService<IEquipmentRepository>()));

builder.Services.AddScoped<IStorageLocationExtendedRepository>(sp =>
    new StorageLocationExtendedRepository(connectionString));

builder.Services.AddScoped<IInventoryStorageReminderQuery>(sp =>
    new InventoryStorageReminderQuery(connectionString));

builder.Services.AddScoped<IGardenRepository>(_ => new GardenRepository(connectionString));

// Register livestock and sales repositories
builder.Services.AddScoped<ILivestockRepository>(sp =>
    new LivestockRepository(connectionString, sp.GetRequiredService<ILogger<LivestockRepository>>()));
builder.Services.AddScoped<IInventorySaleRepository>(sp =>
    new InventorySaleRepository(connectionString, sp.GetRequiredService<ILogger<InventorySaleRepository>>()));

// Register work queue repository (WQ1)
builder.Services.AddScoped<IWorkQueueRepository>(_ => new WorkQueueRepository(connectionString));

// Register seasonal produce service (singleton - pure in-memory calendar)
builder.Services.AddSingleton<ExpressRecipe.MealPlanningService.Services.ISeasonalProduceService,
    ExpressRecipe.MealPlanningService.Services.SeasonalProduceService>();

// Register RabbitMQ for event publishing (legacy EventPublisher)
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    return new ConnectionFactory
    {
        HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
        Port = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
    };
});

// Register event publisher
builder.Services.AddSingleton<EventPublisher>();

// Register typed HttpClients for inter-service communication
builder.Services.AddHttpClient("notificationservice", client =>
{
    string? baseUrl = builder.Configuration["Services:NotificationService:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});
builder.Services.AddHttpClient("priceservice", client =>
{
    string? baseUrl = builder.Configuration["Services:PriceService:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});
builder.Services.AddHttpClient("shoppingservice", client =>
{
    string? baseUrl = builder.Configuration["Services:ShoppingService:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});
builder.Services.AddHttpClient("recipeservice", client =>
{
    string? baseUrl = builder.Configuration["Services:RecipeService:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});

builder.Services.AddHttpClient("MealPlanningService", client =>
{
    string? baseUrl = builder.Configuration["Services:MealPlanningService"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});

// Register background workers
builder.Services.AddHostedService<ExpirationAlertWorker>();
builder.Services.AddHostedService<LowStockMonitorWorker>();
builder.Services.AddHostedService<PatternAnalysisWorker>();
builder.Services.AddHostedService<StorageReminderWorker>();
builder.Services.AddHostedService<GardenRipeCheckWorker>();
builder.Services.AddHostedService<ExpirationQueueGenerator>();
builder.Services.AddHostedService<LowStockQueueGenerator>();

// Register messaging and subscribers (optional — requires RabbitMQ)
bool messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
string? messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
bool messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    builder.Services.AddHostedService<RecipeCookedEventSubscriber>();
    builder.Services.AddHostedService<MealDelayStorageSubscriber>();
    // GDPR: hard-delete user inventory data on gdpr.user.delete events
    builder.Services.AddHostedService<ExpressRecipe.InventoryService.Services.GdprEventSubscriber>();
}

// Register unit conversion (uses HttpIngredientDensityResolver to call ProductService)
var inventoryProductServiceUrl = builder.Configuration["Services:ProductService:BaseUrl"]
    ?? builder.Configuration["services__productservice__http__0"]
    ?? "http://productservice";
builder.Services.AddHttpClient<IIngredientDensityResolver, HttpIngredientDensityResolver>(client =>
{
    client.BaseAddress = new Uri(inventoryProductServiceUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<IUnitConversionService>(sp =>
    new UnitConversionService(sp.GetRequiredService<IIngredientDensityResolver>()));

// Add controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("InventoryService", "inventorydb");

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

// Add rate limiting middleware
app.UseRateLimiting(new RateLimitOptions
{
    Enabled = true,
    MaxRequestsPerWindow = 100,
    WindowSeconds = 60
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
