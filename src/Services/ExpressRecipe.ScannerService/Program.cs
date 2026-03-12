using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.ScannerService.Data;
using ExpressRecipe.ScannerService.Services;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication (shared JWT bearer configuration)
builder.AddExpressRecipeAuthentication();

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("scandb")
    ?? throw new InvalidOperationException("Database connection string 'scandb' not found");

// Register repositories
builder.Services.AddScoped<IScannerRepository>(sp =>
    new ScannerRepository(connectionString, sp.GetRequiredService<ILogger<ScannerRepository>>(), sp.GetService<HybridCacheService>()));

// HybridCache (L1 in-memory + optional L2 Redis)
builder.AddHybridCache();
builder.Services.AddSingleton<HybridCacheService>();

// Register external API clients
builder.Services.AddHttpClient<OpenFoodFactsApiClient>();
builder.Services.AddHttpClient<UPCDatabaseApiClient>();

// Register barcode scanner service
builder.Services.AddScoped<BarcodeScannerService>();

// Add controllers
builder.Services.AddControllers();

// Register RabbitMQ messaging (IMessageBus) – conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    // GDPR: hard-delete user scanner data on gdpr.user.delete events
    builder.Services.AddHostedService<GdprEventSubscriber>();
}

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("ScannerService", "scandb");

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
