using ExpressRecipe.Data.Common;
using ExpressRecipe.CookbookService.Data;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.AddLayeredConfiguration(args);
builder.AddServiceDefaults();
builder.AddSqlServerClient("cookbookdb");

// Add Redis for distributed caching
builder.AddRedisClient("cache");

// Add hybrid caching (memory L1 + Redis L2)
builder.AddHybridCache();

// Register hybrid cache service
builder.Services.AddSingleton<HybridCacheService>();

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

var connectionString = builder.Configuration.GetConnectionString("cookbookdb")
    ?? throw new InvalidOperationException("Database connection string 'cookbookdb' not found");

builder.Services.AddScoped<ICookbookRepository>(sp =>
    new CookbookRepository(connectionString));

builder.Services.AddControllers();

// Register RabbitMQ messaging (IMessageBus) – conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    // GDPR: hard-delete user cookbook data on gdpr.user.delete events
    builder.Services.AddHostedService<ExpressRecipe.CookbookService.Services.GdprEventSubscriber>();
}

builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

await app.RunDatabaseManagementAsync("CookbookService", "cookbookdb");

var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(connectionString, migrations);

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
