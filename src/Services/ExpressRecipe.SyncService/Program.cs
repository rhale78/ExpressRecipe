using ExpressRecipe.Data.Common;
using ExpressRecipe.SyncService.Data;
using ExpressRecipe.SyncService.Hubs;
using ExpressRecipe.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication (shared JWT bearer configuration)
builder.AddExpressRecipeAuthentication();

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("syncdb")
    ?? throw new InvalidOperationException("Database connection string 'syncdb' not found");

// Register SignalR for real-time sync updates
builder.Services.AddSignalR();

// Register sync push service
builder.Services.AddScoped<SyncPushService>();

// Register repositories
builder.Services.AddScoped<ISyncRepository>(sp =>
    new SyncRepository(connectionString, sp.GetRequiredService<ILogger<SyncRepository>>()));

// Add controllers
builder.Services.AddControllers();

// Add Swagger
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c =>
//{
//     c.SwaggerDoc("v1", new() { Title = "ExpressRecipe.SyncService API", Version = "v1" });
// });

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("SyncService", "syncdb");

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
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs
app.MapHub<SyncHub>("/hubs/sync");

app.MapControllers();

app.Run();
