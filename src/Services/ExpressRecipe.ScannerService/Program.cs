using ExpressRecipe.Data.Common;
using ExpressRecipe.ScannerService.Data;
using ExpressRecipe.ScannerService.Services;
using ExpressRecipe.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication (shared JWT bearer configuration)
builder.AddExpressRecipeAuthentication();
builder.Services.AddAuthorization();

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("scandb")
    ?? throw new InvalidOperationException("Database connection string 'scandb' not found");

// Register repositories
builder.Services.AddScoped<IScannerRepository>(sp =>
    new ScannerRepository(connectionString, sp.GetRequiredService<ILogger<ScannerRepository>>()));

// Register external API clients
builder.Services.AddHttpClient<OpenFoodFactsApiClient>();
builder.Services.AddHttpClient<UPCDatabaseApiClient>();

// Register barcode scanner service
builder.Services.AddScoped<BarcodeScannerService>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new() { Title = "ExpressRecipe.ScannerService API", Version = "v1" });
// });

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("ScannerService", "scandb");

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
app.MapControllers();

app.Run();
