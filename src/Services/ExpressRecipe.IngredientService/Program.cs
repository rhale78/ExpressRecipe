using ExpressRecipe.Data.Common;
using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.IngredientService.Services.Parsing;
using ExpressRecipe.Shared.CQRS;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components
builder.AddServiceDefaults();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("ingredientdb")
    ?? throw new InvalidOperationException("Ingredient database connection not configured");

// Repositories
builder.Services.AddScoped<IIngredientRepository>(sp => new IngredientRepository(connectionString));

// Parsing Services
builder.Services.AddSingleton<IIngredientListParser, AdvancedIngredientParser>();
builder.Services.AddScoped<IIngredientParser, IngredientParser>();

// Add gRPC
builder.Services.AddGrpc();

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? 
                builder.Configuration["Jwt:Key"] ?? 
                Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? 
                "development-secret-key-change-in-production-min-32-chars-required!";
if (secretKey == "development-secret-key-change-in-production-min-32-chars-required!" && builder.Environment.IsProduction())
    throw new InvalidOperationException("[FATAL] JWT_SECRET_KEY environment variable must be configured in production.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "ExpressRecipe.AuthService",
            ValidAudience = jwtSettings["Audience"] ?? "ExpressRecipe.API",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Register token provider (service-to-service authentication)
builder.Services.AddScoped<ITokenProvider>(sp =>
    new ServiceTokenProvider("IngredientService", builder.Configuration));

// Register authentication handler that adds tokens to all HTTP requests
builder.Services.AddScoped<AuthenticationDelegatingHandler>();

// Configure default HTTP client behavior to use authentication
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddHttpMessageHandler<AuthenticationDelegatingHandler>();
});

var app = builder.Build();

// Map default endpoints
app.MapDefaultEndpoints();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Ensure database is managed (migrations, etc.)
await app.RunDatabaseManagementAsync("IngredientService", "ExpressRecipe.Ingredients");

// Run database migrations
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}

if (Directory.Exists(migrationsPath))
{
    var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
    await app.RunMigrationsAsync(connectionString, migrations);
}
else
{
    app.Logger.LogWarning("Migrations directory not found at {Path}", migrationsPath);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<ExpressRecipe.IngredientService.Services.IngredientGrpcService>();

app.Run();
