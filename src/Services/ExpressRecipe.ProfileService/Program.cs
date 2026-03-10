using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.ProfileService.Data;
using ExpressRecipe.ProfileService.Services;
using ExpressRecipe.Shared.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddLayeredConfiguration(args);

builder.AddServiceDefaults();

builder.AddSqlServerClient("profilesdb");

builder.AddRedisClient("redis");

builder.AddExpressRecipeAuthentication();

string connectionString = builder.Configuration.GetConnectionString("profilesdb")
    ?? throw new InvalidOperationException("Database connection string 'profilesdb' not found");

builder.Services.AddScoped<IHouseholdMemberRepository>(_ => new HouseholdMemberRepository(connectionString));
builder.Services.AddScoped<IHouseholdMemberService, HouseholdMemberService>();

builder.Services.AddHostedService<GuestExpiryWorker>();

bool messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
string? messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
bool messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    builder.Services.AddSingleton<IProfileEventPublisher, ProfileEventPublisher>();
}
else
{
    builder.Services.AddSingleton<IProfileEventPublisher, NullProfileEventPublisher>();
}

builder.Services.AddControllers();

builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

WebApplication app = builder.Build();

await app.RunDatabaseManagementAsync("ProfileService", "profilesdb");

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
