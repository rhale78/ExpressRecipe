using ExpressRecipe.Data.Common;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.Shared.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

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

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("inventorydb")
    ?? throw new InvalidOperationException("Database connection string 'inventorydb' not found");

// Register repositories
builder.Services.AddScoped<IInventoryRepository>(sp =>
    new InventoryRepository(connectionString, sp.GetRequiredService<ILogger<InventoryRepository>>()));

builder.Services.AddScoped<IEquipmentRepository>(sp =>
    new EquipmentRepository(connectionString));

builder.Services.AddScoped<IStorageLocationExtendedRepository>(sp =>
    new StorageLocationExtendedRepository(connectionString));

builder.Services.AddScoped<IInventoryStorageReminderQuery>(sp =>
    new InventoryStorageReminderQuery(connectionString));

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

// Register background workers
builder.Services.AddHostedService<ExpirationAlertWorker>();
builder.Services.AddHostedService<LowStockMonitorWorker>();
builder.Services.AddHostedService<PatternAnalysisWorker>();
builder.Services.AddHostedService<StorageReminderWorker>();

// Register messaging and subscribers (optional — requires RabbitMQ)
bool messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
string? messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
bool messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    builder.Services.AddHostedService<RecipeCookedEventSubscriber>();
    builder.Services.AddHostedService<MealDelayStorageSubscriber>();
}

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
