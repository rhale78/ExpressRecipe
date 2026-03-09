using ExpressRecipe.Data.Common;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.MealPlanningService.Services.GoogleCalendar;
using ExpressRecipe.MealPlanningService.Workers;
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
string connectionString = builder.Configuration.GetConnectionString("mealplandb")
    ?? throw new InvalidOperationException("Database connection string 'mealplandb' not found");

// Register repositories
builder.Services.AddScoped<IMealPlanningRepository>(sp =>
    new MealPlanningRepository(connectionString, sp.GetRequiredService<ILogger<MealPlanningRepository>>()));

builder.Services.AddScoped<IMealScheduleConfigRepository>(_ =>
    new MealScheduleConfigRepository(connectionString));

// Google Calendar token repository uses AuthService DB when configured; falls back to mealplandb
string calendarTokenConnectionString = builder.Configuration.GetConnectionString("authdb") ?? connectionString;
builder.Services.AddScoped<IGoogleCalendarTokenRepository>(_ =>
    new GoogleCalendarTokenRepository(calendarTokenConnectionString));

// Register Google Calendar service
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();

// Register Holiday service (singleton — purely in-memory)
builder.Services.AddSingleton<IHolidayService, HolidayService>();

// HTTP clients
builder.Services.AddHttpClient("GoogleCalendar");
builder.Services.AddHttpClient("NotificationService", client =>
{
    string notificationServiceUrl = builder.Configuration["Services:NotificationService"] ?? "http://notificationservice";
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Background worker for meal cook notifications
builder.Services.AddHostedService(sp =>
    new MealCookNotificationWorker(
        connectionString,
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<MealCookNotificationWorker>>()));

// Add controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("MealPlanningService", "mealplandb");

// Run migrations using shared MigrationRunner
string migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
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
