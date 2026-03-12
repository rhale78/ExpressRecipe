using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.PreferencesService.Data;
using ExpressRecipe.PreferencesService.Services;
using ExpressRecipe.Shared.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddLayeredConfiguration(args);

builder.AddServiceDefaults();

builder.AddSqlServerClient("preferencesdb");

builder.AddRedisClient("redis");

builder.AddExpressRecipeAuthentication();

string connectionString = builder.Configuration.GetConnectionString("preferencesdb")
    ?? throw new InvalidOperationException("Database connection string 'preferencesdb' not found");

builder.Services.AddScoped<ICookProfileRepository>(_ => new CookProfileRepository(connectionString));
builder.Services.AddScoped<ICookProfileService, CookProfileService>();

bool messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
string? messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
bool messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    builder.Services.AddHostedService<HouseholdMemberSubscriber>();
}

builder.Services.AddControllers();

builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

WebApplication app = builder.Build();

await app.RunDatabaseManagementAsync("PreferencesService", "preferencesdb");

string migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}

Dictionary<string, string> migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
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
