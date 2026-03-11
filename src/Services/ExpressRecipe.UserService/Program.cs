using ExpressRecipe.Data.Common;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.Shared.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add database connection
builder.AddSqlServerClient("userdb");

// Add Redis for caching
builder.AddRedisClient("redis");

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Authentication failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Token validated for user: {User}", context.Principal?.Identity?.Name);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Authentication challenge: {Error}, {ErrorDescription}", context.Error, context.ErrorDescription);
            return Task.CompletedTask;
        }
    };
});

// Register repositories
var connectionString = builder.Configuration.GetConnectionString("userdb")
    ?? throw new InvalidOperationException("Database connection string 'userdb' not found");

builder.Services.AddScoped<IUserProfileRepository>(sp => new UserProfileRepository(connectionString));
builder.Services.AddScoped<IAllergenRepository>(sp => new AllergenRepository(connectionString));
builder.Services.AddScoped<IEnhancedAllergenRepository>(sp => new EnhancedAllergenRepository(connectionString));
builder.Services.AddScoped<IDietaryRestrictionRepository>(sp => new DietaryRestrictionRepository(connectionString));
builder.Services.AddScoped<IFamilyMemberRepository>(sp => new FamilyMemberRepository(connectionString));
builder.Services.AddScoped<IFamilyRelationshipRepository>(sp => new FamilyRelationshipRepository(connectionString));
builder.Services.AddScoped<IUserFavoritesRepository>(sp => new UserFavoritesRepository(connectionString));
builder.Services.AddScoped<IUserProductRatingRepository>(sp => new UserProductRatingRepository(connectionString));
builder.Services.AddScoped<ICuisineRepository>(sp => new CuisineRepository(connectionString));
builder.Services.AddScoped<IHealthGoalRepository>(sp => new HealthGoalRepository(connectionString));
builder.Services.AddScoped<IUserPreferenceRepository>(sp => new UserPreferenceRepository(connectionString));
builder.Services.AddScoped<IPointsRepository>(sp => new PointsRepository(connectionString));
builder.Services.AddScoped<IFriendsRepository>(sp => new FriendsRepository(connectionString));
builder.Services.AddScoped<IFamilyScoreRepository>(sp => new FamilyScoreRepository(connectionString));
builder.Services.AddScoped<IReportsRepository>(sp => new ReportsRepository(connectionString));
builder.Services.AddScoped<ISubscriptionRepository>(sp => new SubscriptionRepository(connectionString));
builder.Services.AddScoped<IActivityRepository>(sp => new ActivityRepository(connectionString));
builder.Services.AddScoped<IUserSettingsRepository>(sp => new UserSettingsRepository(connectionString));
builder.Services.AddScoped<IStripeEventLogRepository>(sp => new StripeEventLogRepository(connectionString));

// Register payment service — MockPaymentService in local mode, StripePaymentService otherwise
if (builder.Configuration.GetValue<bool>("APP_LOCAL_MODE"))
{
    builder.Services.AddSingleton<ExpressRecipe.UserService.Services.IPaymentService,
        ExpressRecipe.UserService.Services.MockPaymentService>();
}
else
{
    builder.Services.AddScoped<ExpressRecipe.UserService.Services.IPaymentService,
        ExpressRecipe.UserService.Services.StripePaymentService>();
}

// Register the Stripe event constructor delegate used by StripeWebhookController
builder.Services.AddSingleton<Func<string, string, string, Stripe.Event>>(
    (payload, sig, secret) => Stripe.EventUtility.ConstructEvent(payload, sig, secret));

// Register named HTTP clients for service-to-service calls
builder.Services.AddHttpClient("AuthService", client =>
{
    var authServiceUrl = builder.Configuration["Services:AuthService"] ?? "http://authservice";
    client.BaseAddress = new Uri(authServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("NotificationService", client =>
{
    var notificationServiceUrl = builder.Configuration["Services:NotificationService"] ?? "http://notificationservice";
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register background services
builder.Services.AddHostedService<ExpressRecipe.UserService.Services.SubscriptionRenewalService>();
builder.Services.AddHostedService<ExpressRecipe.UserService.Services.ScheduledReportsService>();
builder.Services.AddHostedService<ExpressRecipe.UserService.Services.PointsManagementService>();

// Add controllers
builder.Services.AddControllers();

// Add API documentation
// TODO: Re-add Swagger after resolving OpenApi 2.0 compatibility
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("UserService", "userdb");

// Run database migrations
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(connectionString, migrations);

// Configure the HTTP request pipeline
app.MapDefaultEndpoints();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    // TODO: Re-enable after resolving OpenApi 2.0 compatibility
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Use activity tracking middleware (must be after authentication)
app.UseMiddleware<ExpressRecipe.UserService.Middleware.ActivityTrackingMiddleware>();

app.MapControllers();

app.Run();
