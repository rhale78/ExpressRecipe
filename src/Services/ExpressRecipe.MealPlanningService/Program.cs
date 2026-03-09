using ExpressRecipe.Data.Common;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.MealPlanningService.Workers;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ExpressRecipe.Shared.Middleware;
using Microsoft.Extensions.Caching.Hybrid;

var builder = WebApplication.CreateBuilder(args);

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
var connectionString = builder.Configuration.GetConnectionString("mealplandb")
    ?? throw new InvalidOperationException("Database connection string 'mealplandb' not found");

// Register repositories
builder.Services.AddScoped<IMealPlanningRepository>(sp =>
    new MealPlanningRepository(connectionString, sp.GetRequiredService<ILogger<MealPlanningRepository>>()));

// Register HTTP clients for external service calls
builder.Services.AddHttpClient("RecipeService");
builder.Services.AddHttpClient("InventoryService");
builder.Services.AddHttpClient("SafeForkService");
builder.Services.AddHttpClient("ShoppingService");
builder.Services.AddHttpClient("NotificationService");
builder.Services.AddHttpClient("MealPlanningService");

// Register RabbitMQ messaging when connection string is configured
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
if (!string.IsNullOrWhiteSpace(messagingConnectionString))
{
    builder.AddRabbitMqMessaging("messaging");
}

// Register HybridCache for suggestion caching
#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018

// Register suggestion service
builder.Services.AddScoped<IMealSuggestionService, MealSuggestionService>();

// Register background workers
builder.Services.AddHostedService<RecipeCookedEventPublisherWorker>();
builder.Services.AddHostedService<CookingRatingPromptWorker>();

// Add controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("MealPlanningService", "mealplandb");

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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
