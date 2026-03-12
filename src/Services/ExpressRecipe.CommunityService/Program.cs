using ExpressRecipe.Data.Common;
using ExpressRecipe.CommunityService.Data;
using ExpressRecipe.CommunityService.Services;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
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
var connectionString = builder.Configuration.GetConnectionString("communitydb")
    ?? throw new InvalidOperationException("Database connection string 'communitydb' not found");

// Register repositories
builder.Services.AddScoped<ICommunityRepository>(sp =>
    new CommunityRepository(connectionString, sp.GetRequiredService<ILogger<CommunityRepository>>()));
builder.Services.AddScoped<IApprovalQueueRepository>(sp =>
    new ApprovalQueueRepository(connectionString));

builder.Services.AddScoped<ICommunityRecipeRepository>(sp =>
    new CommunityRecipeRepository(connectionString));

// Conditionally register RabbitMQ for event publishing
var rabbitEnabled = builder.Configuration.GetValue<bool?>("RabbitMQ:Enabled")
    ?? !string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"]) ||
       !string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:ConnectionString"]) ||
       !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("messaging"));

if (rabbitEnabled)
{
    builder.Services.AddSingleton<IConnectionFactory>(sp =>
    {
        var uri = builder.Configuration["RabbitMQ:ConnectionString"]
                  ?? builder.Configuration.GetConnectionString("messaging");

        if (!string.IsNullOrWhiteSpace(uri))
        {
            return new ConnectionFactory { Uri = new Uri(uri) };
        }

        return new ConnectionFactory
        {
            HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var commSvcPort) ? commSvcPort : 5672,
            UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
            Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
        };
    });

    builder.Services.AddSingleton<EventPublisher>();
}

// Register approval queue service
builder.Services.AddScoped<IApprovalQueueService>(sp =>
    new ApprovalQueueService(
        sp.GetRequiredService<ICommunityRecipeRepository>(),
        sp.GetRequiredService<ICommunityRepository>(),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<ApprovalQueueService>>(),
        sp.GetService<EventPublisher>()));

// Add controllers
builder.Services.AddControllers();

// Register RabbitMQ messaging (IMessageBus) – conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    // GDPR: anonymise user community data on gdpr.user.delete and gdpr.user.forget events
    builder.Services.AddHostedService<GdprEventSubscriber>();
}

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("CommunityService", "communitydb");

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
