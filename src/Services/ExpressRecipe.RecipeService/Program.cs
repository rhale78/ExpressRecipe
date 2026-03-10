using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.CQRS.Commands;
using ExpressRecipe.RecipeService.CQRS.Queries;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.Shared.CQRS;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.Shared.Units;
using ExpressRecipe.Client.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add database connection
builder.AddSqlServerClient("recipedb");

// Add Redis for caching
builder.AddRedisClient("redis");

// Add hybrid caching (memory + Redis)
builder.AddHybridCache();

// Register ingredient client
builder.AddIngredientClient();

// Register hybrid cache service
builder.Services.AddSingleton<HybridCacheService>();

// Register CacheService for handlers that depend on it
// CacheService requires IConnectionMultiplexer (provided by AddRedisClient)
builder.Services.AddSingleton<ExpressRecipe.Shared.Services.CacheService>();

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

// Register token provider (service-to-service authentication)
builder.Services.AddScoped<ITokenProvider>(sp =>
    new ServiceTokenProvider("RecipeService", builder.Configuration));

// Register authentication handler that adds tokens to all HTTP requests
builder.Services.AddScoped<AuthenticationDelegatingHandler>();

// Configure default HTTP client behavior to use authentication
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddHttpMessageHandler<AuthenticationDelegatingHandler>();
});

// Register repositories
var connectionString = builder.Configuration.GetConnectionString("recipedb")
    ?? throw new InvalidOperationException("Database connection string 'recipedb' not found");

builder.Services.AddScoped<IRecipeImportRepository>(sp => new RecipeImportRepository(connectionString));
builder.Services.AddScoped<ICommentsRepository>(sp => new CommentsRepository(connectionString));
builder.Services.AddScoped<IRecipeNutritionRepository>(sp => new RecipeNutritionRepository(connectionString));
builder.Services.AddScoped<IRecipeRepository>(sp => 
{
    var client = sp.GetRequiredService<IIngredientServiceClient>();
    return new RecipeRepository(connectionString, client);
});
builder.Services.AddScoped<IRecipeStagingRepository>(sp => 
    new RecipeStagingRepository(connectionString, sp.GetRequiredService<ILogger<RecipeStagingRepository>>()));
builder.Services.AddScoped<ExpressRecipe.RecipeService.Data.IRatingRepository>(sp => 
    new ExpressRecipe.RecipeService.Data.RatingRepository(connectionString));
builder.Services.AddScoped<ICookSessionRepository>(sp => new CookSessionRepository(connectionString));

// Register Background Workers
builder.Services.AddHostedService<ExpressRecipe.RecipeService.Workers.RecipeImportWorker>();
builder.Services.AddHostedService<ExpressRecipe.RecipeService.Workers.RecipeProcessingWorker>();

// Register RabbitMQ messaging (IMessageBus) – conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");
    builder.Services.AddSingleton<IRecipeEventPublisher, RecipeEventPublisher>();

    // Handle nutrition query messages from MealPlanningService and other consumers
    builder.Services.AddScoped<RecipeNutritionQueryHandler>();
    builder.Services.AddHostedService<RecipeNutritionQuerySubscriber>();

    // Replace REST ingredient client with messaging-based client (last registration wins)
    builder.Services.AddScoped<IIngredientServiceClient>(sp =>
        new MessagingIngredientServiceClient(
            sp.GetRequiredService<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus>(),
            sp.GetRequiredService<IngredientServiceClient>(),
            sp.GetRequiredService<ILogger<MessagingIngredientServiceClient>>(),
            sp.GetRequiredService<IConfiguration>()));
}
else
{
    builder.Services.AddSingleton<IRecipeEventPublisher, NullRecipeEventPublisher>();
}

// Register recipe batch channel (async path) – always available regardless of messaging
builder.Services.AddSingleton<IRecipeBatchChannel, RecipeBatchChannel>();
builder.Services.AddHostedService<RecipeBatchChannelWorker>();

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
builder.Services.AddScoped<IAllergenRepository>(sp =>
    new SqlAllergenRepository(
        connectionString,
        sp.GetRequiredService<ILogger<SqlAllergenRepository>>()));
builder.Services.AddScoped<ExpressRecipe.RecipeService.Services.AllergenDetectionService>();
builder.Services.AddHttpClient<ExpressRecipe.RecipeService.Services.ImageDownloadService>();
builder.Services.AddScoped<ExpressRecipe.RecipeService.Services.ServingSizeService>();
builder.Services.AddScoped<ExpressRecipe.RecipeService.Services.ShoppingListIntegrationService>();

// Register print service
builder.Services.AddScoped<IRecipePrintService, RecipePrintService>();

// Register unit conversion (uses HttpIngredientDensityResolver to call ProductService)
var productServiceUrl = builder.Configuration["Services:ProductService:BaseUrl"]
    ?? builder.Configuration["services__productservice__http__0"]
    ?? "http://productservice";
builder.Services.AddHttpClient<IIngredientDensityResolver, HttpIngredientDensityResolver>(client =>
{
    client.BaseAddress = new Uri(productServiceUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<IUnitConversionService>(sp =>
    new UnitConversionService(sp.GetRequiredService<IIngredientDensityResolver>()));

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
await app.RunDatabaseManagementAsync("RecipeService", "recipedb");

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

// Add Static Files mapping for C:\Recipes
var imagePath = "E:\\Recipes";
if (!Directory.Exists(imagePath)) Directory.CreateDirectory(imagePath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(imagePath),
    RequestPath = "/images/recipes"
});

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
