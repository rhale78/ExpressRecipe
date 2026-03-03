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
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? "development-secret-key-change-in-production-min-32-chars-required!";
if (builder.Environment.IsProduction() && (secretKey == "development-secret-key-change-in-production-min-32-chars-required!" || secretKey.Length < 32))
    throw new InvalidOperationException("[FATAL] JWT_SECRET_KEY must be configured in production and must be at least 32 characters.");
var issuer = jwtSettings["Issuer"] ?? "ExpressRecipe.AuthService";
var audience = jwtSettings["Audience"] ?? "ExpressRecipe.API";

// Log JWT configuration for debugging
var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var startupLogger = loggerFactory.CreateLogger("Startup");
startupLogger.LogInformation("JWT Configuration:");
startupLogger.LogInformation("  Issuer: {Issuer}", issuer);
startupLogger.LogInformation("  Audience: {Audience}", audience);
startupLogger.LogInformation("  SecretKey Length: {Length}", secretKey.Length);
startupLogger.LogInformation("  SecretKey Preview: {Preview}...", secretKey.Substring(0, Math.Min(5, secretKey.Length)));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

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

builder.Services.AddAuthorization();

// Register repositories
var connectionString = builder.Configuration.GetConnectionString("userdb")
    ?? throw new InvalidOperationException("Database connection string 'userdb' not found");

builder.Services.AddScoped<IUserProfileRepository>(sp => new UserProfileRepository(connectionString));
builder.Services.AddScoped<IAllergenRepository>(sp => new AllergenRepository(connectionString));
builder.Services.AddScoped<IEnhancedAllergenRepository>(sp => new EnhancedAllergenRepository(connectionString));
builder.Services.AddScoped<IDietaryRestrictionRepository>(sp => new DietaryRestrictionRepository(connectionString));
builder.Services.AddScoped<IFamilyMemberRepository>(sp => new FamilyMemberRepository(connectionString));
builder.Services.AddScoped<ICuisineRepository>(sp => new CuisineRepository(connectionString));
builder.Services.AddScoped<IHealthGoalRepository>(sp => new HealthGoalRepository(connectionString));
builder.Services.AddScoped<IUserPreferenceRepository>(sp => new UserPreferenceRepository(connectionString));
builder.Services.AddScoped<IPointsRepository>(sp => new PointsRepository(connectionString));
builder.Services.AddScoped<IFriendsRepository>(sp => new FriendsRepository(connectionString));
builder.Services.AddScoped<IFamilyScoreRepository>(sp => new FamilyScoreRepository(connectionString));
builder.Services.AddScoped<IReportsRepository>(sp => new ReportsRepository(connectionString));
builder.Services.AddScoped<ISubscriptionRepository>(sp => new SubscriptionRepository(connectionString));
builder.Services.AddScoped<IActivityRepository>(sp => new ActivityRepository(connectionString));

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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

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
