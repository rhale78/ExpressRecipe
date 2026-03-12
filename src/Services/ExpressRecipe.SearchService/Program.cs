using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.SearchService.Data;
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
var connectionString = builder.Configuration.GetConnectionString("searchdb")
    ?? throw new InvalidOperationException("Database connection string 'searchdb' not found");

// Register repositories
builder.Services.AddScoped<ISearchRepository>(sp =>
    new SearchRepository(connectionString, sp.GetRequiredService<ILogger<SearchRepository>>(), sp.GetService<HybridCacheService>()));

// HybridCache (L1 in-memory + optional L2 Redis)
builder.AddHybridCache();
builder.Services.AddSingleton<HybridCacheService>();

// Add controllers
builder.Services.AddControllers();

// Register RabbitMQ messaging (IMessageBus) – conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    // GDPR: hard-delete user search data on gdpr.user.delete events
    builder.Services.AddHostedService<ExpressRecipe.SearchService.Services.GdprEventSubscriber>();
}

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("SearchService", "searchdb");

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
