using System.Text;
using ExpressRecipe.Data.Common;
using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.IngredientService.Services;
using ExpressRecipe.IngredientService.Services.Matching;
using ExpressRecipe.IngredientService.Services.Parsing;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.Shared.CQRS;
using ExpressRecipe.Shared.Matching;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components
builder.AddServiceDefaults();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("ingredientdb")
    ?? throw new InvalidOperationException("Ingredient database connection not configured");

// Repositories
builder.Services.AddScoped<IIngredientRepository>(sp => new IngredientRepository(connectionString));
builder.Services.AddScoped<IIngredientMatchingRepository>(sp => new IngredientMatchingRepository(connectionString));

// Matching service
builder.Services.AddScoped<IIngredientMatchingService>(sp =>
    new IngredientMatchingService(
        sp.GetRequiredService<IIngredientMatchingRepository>(),
        sp.GetRequiredService<IIngredientRepository>(),
        sp.GetRequiredService<ExpressRecipe.Shared.Services.HybridCacheService>(),
        sp.GetRequiredService<ILogger<IngredientMatchingService>>()));

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
builder.AddExpressRecipeAuthentication();

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

// Register RabbitMQ messaging (IMessageBus) – conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    builder.Services.AddSingleton<IIngredientEventPublisher, IngredientEventPublisher>();

    // Handle lookup and bulk-create requests from ProductService, RecipeService, etc.
    builder.Services.AddScoped<IngredientQueryHandler>();
    builder.Services.AddHostedService<IngredientQuerySubscriber>();
}
else
{
    builder.Services.AddSingleton<IIngredientEventPublisher, NullIngredientEventPublisher>();
}

// Register ingredient batch channel (async path) – always available regardless of messaging
builder.Services.AddSingleton<IIngredientBatchChannel, IngredientBatchChannel>();
builder.Services.AddHostedService<IngredientBatchChannelWorker>();

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
