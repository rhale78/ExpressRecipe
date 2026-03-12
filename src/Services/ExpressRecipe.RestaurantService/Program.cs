using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.RestaurantService.Data;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add SQL Server via Aspire
builder.AddSqlServerClient("restaurantdb");

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

// Register database connection and repositories
var connectionString = builder.Configuration.GetConnectionString("restaurantdb")
    ?? throw new InvalidOperationException("Database connection string 'restaurantdb' not found");

builder.Services.AddScoped<IRestaurantRepository>(sp => new RestaurantRepository(connectionString));

// Add controllers
builder.Services.AddControllers();

// Register RabbitMQ messaging (IMessageBus) – conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    // GDPR: hard-delete user restaurant rating data on gdpr.user.delete events
    builder.Services.AddHostedService<ExpressRecipe.RestaurantService.Services.GdprEventSubscriber>();
}

// Add CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop/reset) if configured, then migrations
await app.RunDatabaseManagementAsync("RestaurantService", "restaurantdb");

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
