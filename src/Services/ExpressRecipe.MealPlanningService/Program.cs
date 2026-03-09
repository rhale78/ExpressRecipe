using ExpressRecipe.Data.Common;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ExpressRecipe.Shared.Middleware;

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

// Add controllers
builder.Services.AddControllers();

// Add Swagger
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new() { Title = "ExpressRecipe.MealPlanningService API", Version = "v1" });
// });

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
