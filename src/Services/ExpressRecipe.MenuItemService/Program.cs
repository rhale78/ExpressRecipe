using ExpressRecipe.Data.Common;
using ExpressRecipe.MenuItemService.Data;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add SQL Server via Aspire
builder.AddSqlServerClient("menuitemdb");

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

// Register database connection and repositories
var connectionString = builder.Configuration.GetConnectionString("menuitemdb")
    ?? throw new InvalidOperationException("Database connection string 'menuitemdb' not found");

builder.Services.AddScoped<IMenuItemRepository>(sp => new MenuItemRepository(connectionString));

// Add controllers
builder.Services.AddControllers();

// Add CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop/reset) if configured, then migrations
await app.RunDatabaseManagementAsync("MenuItemService", "menuitemdb");

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
