using ExpressRecipe.Data.Common;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.MealPlanningService.Workers;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.MealPlanningService.Services.Printing;
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
builder.Services.AddScoped<INutritionLogRepository>(sp =>
    new NutritionLogRepository(connectionString));

builder.Services.AddScoped<ICookingHistoryRepository>(sp =>
    new CookingHistoryRepository(connectionString));

builder.Services.AddScoped<IMealPlanningRepository>(sp =>
    new MealPlanningRepository(connectionString,
        sp.GetRequiredService<ILogger<MealPlanningRepository>>(),
        sp.GetRequiredService<INutritionLogRepository>()));

// Register messaging (IMessageBus) – conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
}

// NutritionLoggingService uses IMessageBus?; if messaging is not configured the bus
// will be null and the service degrades gracefully (logs entries without macros).
builder.Services.AddScoped<INutritionLoggingService>(sp =>
    new NutritionLoggingService(
        sp.GetRequiredService<INutritionLogRepository>(),
        sp.GetService<IMessageBus>(),
        sp.GetRequiredService<ILogger<NutritionLoggingService>>()));

// Register HTTP clients for external service calls
builder.Services.AddHttpClient("RecipeService");
builder.Services.AddHttpClient("InventoryService");
builder.Services.AddHttpClient("SafeForkService");
builder.Services.AddHttpClient("ShoppingService");
builder.Services.AddHttpClient("NotificationService");
builder.Services.AddHttpClient("MealPlanningService");

// Register HybridCache for suggestion caching
#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018

// Register suggestion service
builder.Services.AddScoped<IMealSuggestionService, MealSuggestionService>();

// Register background workers
builder.Services.AddHostedService<RecipeCookedEventPublisherWorker>();
builder.Services.AddHostedService<CookingRatingPromptWorker>();

builder.Services.AddScoped<IMealCourseRepository>(_ => new MealCourseRepository(connectionString));
builder.Services.AddScoped<IMealAttendeeRepository>(_ => new MealAttendeeRepository(connectionString));

// Register services
builder.Services.AddScoped<IMealPlanCopyService, MealPlanCopyService>();
builder.Services.AddScoped<IMealPlanTemplateService, MealPlanTemplateService>();

builder.Services.AddSingleton<IMealVotingRepository>(new MealVotingRepository(connectionString));

builder.Services.AddScoped<IMealPlanHistoryService>(sp =>
    new MealPlanHistoryService(connectionString, sp.GetRequiredService<IMealPlanningRepository>()));

builder.Services.AddHostedService(sp =>
    new MealPlanSnapshotPruningWorker(connectionString, sp.GetRequiredService<ILogger<MealPlanSnapshotPruningWorker>>()));

// Register services
builder.Services.AddSingleton<IHolidayService, HolidayService>();
builder.Services.AddScoped<IMealPlanPdfService, MealPlanPdfService>();

// HTTP clients for inter-service communication
builder.Services.AddHttpClient("RecipeService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["services:recipeservice:https:0"] ?? builder.Configuration["services:recipeservice:http:0"] ?? "http://recipeservice");
});
builder.Services.AddHttpClient("ShoppingService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["services:shoppingservice:https:0"] ?? builder.Configuration["services:shoppingservice:http:0"] ?? "http://shoppingservice");
});

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
app.UseRateLimiting(new RateLimitOptions
{
    Enabled = true,
    MaxRequestsPerWindow = 100,
    WindowSeconds = 60
});
app.MapControllers();

app.Run();
