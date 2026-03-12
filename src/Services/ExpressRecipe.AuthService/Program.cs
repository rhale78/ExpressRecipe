using ExpressRecipe.AuthService.Data;
using ExpressRecipe.AuthService.Services;
using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (logging, telemetry, health checks)
builder.AddServiceDefaults();

// Add database connection
builder.AddSqlServerClient("authdb");

// Add Redis for caching
builder.AddRedisClient("redis");

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

// Register repositories
var connectionString = builder.Configuration.GetConnectionString("authdb")
    ?? throw new InvalidOperationException("Database connection string 'authdb' not found");

builder.Services.AddScoped<IAuthRepository>(sp =>
    new AuthRepository(connectionString, sp.GetRequiredService<ILogger<AuthRepository>>()));

// Register services
builder.Services.AddScoped<TokenService>();

// Add HttpClient for UserService communication
builder.Services.AddHttpClient("UserService", client =>
{
    var userServiceUrl = builder.Configuration["Services:UserService"] ?? "http://userservice";
    client.BaseAddress = new Uri(userServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add controllers with custom configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Use camelCase for JSON (this is already default, but being explicit)
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Make property name matching case-insensitive
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Disable automatic 400 responses for model validation errors
        // so we can log what's actually wrong
        options.SuppressModelStateInvalidFilter = true;
    });

// Add API documentation
// Swagger/OpenAPI is disabled pending resolution of OpenAPI 2.0 compatibility with .NET 10.
// Tracked in: https://github.com/dotnet/aspnetcore/issues/XXXX
// To re-enable: uncomment the lines below and add a <PackageReference Include="Swashbuckle.AspNetCore" /> to the project.
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new() { Title = "ExpressRecipe.AuthService API", Version = "v1" }); });

// Add CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("AuthService", "authdb");

// Run database migrations
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(connectionString, migrations);

// Seed default admin user
using (var scope = app.Services.CreateScope())
{
    var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await authRepo.EnsureAdminUserExistsAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to seed default admin user");
    }
}

// Configure the HTTP request pipeline
app.MapDefaultEndpoints();
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
