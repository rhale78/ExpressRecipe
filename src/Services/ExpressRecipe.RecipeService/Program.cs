using ExpressRecipe.Data.Common;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.CQRS.Commands;
using ExpressRecipe.RecipeService.CQRS.Queries;
using ExpressRecipe.Shared.CQRS;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using StackExchange.Redis;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add database connection
builder.AddSqlServerClient("recipedb");

// Add Redis for caching
var redisConnectionString = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<CacheService>();

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

// Configure JWT Authentication
IConfigurationSection jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? "development-secret-key-change-in-production-min-32-chars-required!";

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

// Register repositories
var connectionString = builder.Configuration.GetConnectionString("recipedb")
    ?? throw new InvalidOperationException("Database connection string 'recipedb' not found");

builder.Services.AddScoped<IRecipeImportRepository>(sp => new RecipeImportRepository(connectionString));
builder.Services.AddScoped<ICommentsRepository>(sp => new CommentsRepository(connectionString));
builder.Services.AddScoped<IRecipeRepository>(sp => new RecipeRepository(connectionString));

// Register RabbitMQ for event publishing
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    return new ConnectionFactory
    {
        HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
        Port = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
    };
});

// Register event publisher
builder.Services.AddSingleton<EventPublisher>();

// Register CQRS infrastructure
builder.Services.AddCqrsDispatcher();

// Register command handlers
builder.Services.AddCommandHandler<CreateRecipeCommand, Guid, CreateRecipeCommandHandler>();

// Register query handlers
builder.Services.AddQueryHandler<GetRecipeDetailsQuery, RecipeDetailsDto?, GetRecipeDetailsQueryHandler>();
builder.Services.AddQueryHandler<SearchRecipesQuery, SearchRecipesResult, SearchRecipesQueryHandler>();

// Register services
builder.Services.AddScoped<ExpressRecipe.RecipeService.Services.RecipeImportService>();
builder.Services.AddScoped<ExpressRecipe.RecipeService.Services.NutritionExtractionService>();
builder.Services.AddScoped<ExpressRecipe.RecipeService.Services.AllergenDetectionService>(sp =>
    new ExpressRecipe.RecipeService.Services.AllergenDetectionService(
        connectionString,
        sp.GetRequiredService<ILogger<ExpressRecipe.RecipeService.Services.AllergenDetectionService>>()));
builder.Services.AddHttpClient<ExpressRecipe.RecipeService.Services.ImageDownloadService>();

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
              .AllowAnyHeader()
              .WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "Retry-After");
    });
});

WebApplication app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("RecipeService", "recipedb");

// Run database migrations
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
Dictionary<string, string> migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(connectionString, migrations);

// Configure the HTTP request pipeline
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    // TODO: Re-enable after resolving OpenApi 2.0 compatibility
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseCors();

// Add rate limiting middleware
app.UseRateLimiting(new RateLimitOptions
{
    Enabled = true,
    MaxRequestsPerWindow = 100,
    WindowSeconds = 60
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
