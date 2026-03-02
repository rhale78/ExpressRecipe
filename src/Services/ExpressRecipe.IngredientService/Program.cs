using ExpressRecipe.Data.Common;
using ExpressRecipe.IngredientService.Data;
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

// Add gRPC
builder.Services.AddGrpc();

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyForDevelopmentOnly";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

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
