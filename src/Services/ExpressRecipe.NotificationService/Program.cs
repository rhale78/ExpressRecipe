using ExpressRecipe.Data.Common;
using ExpressRecipe.NotificationService.Data;
using ExpressRecipe.NotificationService.Hubs;
using ExpressRecipe.NotificationService.Services;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication (shared JWT bearer configuration)
builder.AddExpressRecipeAuthentication();

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("notificationdb")
    ?? throw new InvalidOperationException("Database connection string 'notificationdb' not found");

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

// Register SignalR for real-time notifications
builder.Services.AddSignalR();

// Register push service for SignalR notifications
builder.Services.AddScoped<NotificationPushService>();

// Register broadcast service
builder.Services.AddScoped<NotificationBroadcastService>();

// Register repositories
builder.Services.AddScoped<INotificationRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<NotificationRepository>>();
    var broadcastService = sp.GetRequiredService<NotificationBroadcastService>();
    return new NotificationRepository(connectionString, logger, broadcastService);
});

// Conditionally register RabbitMQ for event subscription
var rabbitEnabled = builder.Configuration.GetValue<bool?>("RabbitMQ:Enabled")
    ?? !string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"]) ||
       !string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:ConnectionString"]) ||
       !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("messaging"));

if (rabbitEnabled)
{
    builder.Services.AddSingleton<IConnectionFactory>(sp =>
    {
        var uri = builder.Configuration["RabbitMQ:ConnectionString"]
                  ?? builder.Configuration.GetConnectionString("messaging"); // Aspire provides this when referenced

        if (!string.IsNullOrWhiteSpace(uri))
        {
            return new ConnectionFactory { Uri = new Uri(uri) };
        }

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

    // Register event subscriber as background service
    builder.Services.AddHostedService<NotificationEventSubscriber>();
}

// Add controllers
builder.Services.AddControllers();

// CORS with SignalR support
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("NotificationService", "notificationdb");

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

// Add rate limiting middleware
app.UseRateLimiting(new RateLimitOptions
{
    Enabled = true,
    MaxRequestsPerWindow = 100,
    WindowSeconds = 60
});

app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapControllers();

app.Run();
