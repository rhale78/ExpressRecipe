using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.Messaging.Saga.Extensions;
using ExpressRecipe.SafeForkService.Data;
using ExpressRecipe.SafeForkService.Saga;
using ExpressRecipe.SafeForkService.Services;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddLayeredConfiguration(args);

builder.AddServiceDefaults();

builder.AddSqlServerClient("safeforkdb");

builder.AddRedisClient("cache");

builder.AddHybridCache();

builder.AddExpressRecipeAuthentication();

string connectionString = builder.Configuration.GetConnectionString("safeforkdb")
    ?? throw new InvalidOperationException("Database connection string 'safeforkdb' not found");

builder.Services.AddSingleton<HybridCacheService>();

builder.Services.AddScoped<IAllergenProfileRepository>(_ => new AllergenProfileRepository(connectionString));
builder.Services.AddScoped<ITemporaryScheduleRepository>(_ => new TemporaryScheduleRepository(connectionString));
builder.Services.AddScoped<IAdaptationOverrideRepository>(_ => new AdaptationOverrideRepository(connectionString));

builder.Services.AddScoped<AllergenResolutionService>();
builder.Services.AddScoped<IAllergenProfileService, AllergenProfileService>();

builder.Services.AddHttpClient("IngredientService", client =>
{
    string url = builder.Configuration["Services:IngredientService"] ?? "http://ingredientservice";
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("ProductService", client =>
{
    string url = builder.Configuration["Services:ProductService"] ?? "http://productservice";
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(10);
});

bool messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
string? messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
bool messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");

    builder.Services.AddSqlSagaRepository<MemberOnboardingSagaState>(connectionString, "MemberOnboardingSagaState");
    builder.Services.AddSqlSagaRepository<AllergenResolutionSagaState>(connectionString, "AllergenResolutionSagaState");

    builder.Services.AddSagaWorkflow(MemberOnboardingWorkflow.Build());
    builder.Services.AddSagaWorkflow(AllergenResolutionWorkflow.Build());

    builder.Services.AddSingleton<ISafeForkEventPublisher, SafeForkEventPublisher>();

    builder.Services.AddHostedService<HouseholdMemberSubscriber>();
}
else
{
    builder.Services.AddSingleton<ISafeForkEventPublisher, NullSafeForkEventPublisher>();
}

builder.Services.AddControllers();

builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

WebApplication app = builder.Build();

await app.RunDatabaseManagementAsync("SafeForkService", "safeforkdb");

string migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}

System.Collections.Generic.Dictionary<string, string> migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
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
