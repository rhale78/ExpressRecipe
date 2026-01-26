using ExpressRecipe.Data.Common;
using ExpressRecipe.SyncService.Data;
using ExpressRecipe.SyncService.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"] ?? "http://localhost:5000";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

WebApplication app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("SyncService", "syncdb");

// Run migrations using shared MigrationRunner
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
Dictionary<string, string> migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(connectionString, migrations);

// Configure middleware pipeline
app.MapDefaultEndpoints(); // Aspire health checks

if (app.Environment.IsDevelopment())
{
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs
app.MapHub<SyncHub>("/hubs/sync");

app.MapControllers();

app.Run();
