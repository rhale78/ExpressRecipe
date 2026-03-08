using ExpressRecipe.Data.Common;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using ExpressRecipe.Messaging.Saga.Extensions;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Saga;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
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
builder.AddSqlServerClient("productdb");

// Add Redis for distributed caching
builder.AddRedisClient("cache");

// Add hybrid caching (memory + Redis)
builder.AddHybridCache();

// Register ingredient client
builder.AddIngredientClient();

// Register hybrid cache service
builder.Services.AddSingleton<HybridCacheService>();

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

// Register token provider (service-to-service authentication)
builder.Services.AddScoped<ITokenProvider>(sp =>
    new ServiceTokenProvider("ProductService", builder.Configuration));

// Register authentication handler that adds tokens to all HTTP requests
builder.Services.AddScoped<AuthenticationDelegatingHandler>();

// Configure default HTTP client behavior to use authentication
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddHttpMessageHandler<AuthenticationDelegatingHandler>();
});

// Register repositories
var connectionString = builder.Configuration.GetConnectionString("productdb")
    ?? throw new InvalidOperationException("Database connection string 'productdb' not found");

builder.Services.AddScoped<IProductImageRepository>(sp => 
    new ProductImageRepository(connectionString, sp.GetRequiredService<ILogger<ProductImageRepository>>()));
builder.Services.AddScoped<IProductRepository>(sp => 
{
    var cache = sp.GetRequiredService<HybridCacheService>();
    var logger = sp.GetRequiredService<ILogger<ProductRepository>>();
    var client = sp.GetRequiredService<IIngredientServiceClient>();
    return new ProductRepository(connectionString, sp.GetRequiredService<IProductImageRepository>(), client, cache, logger);
});
builder.Services.AddScoped<IIngredientRepository>(sp =>
{
    var client = sp.GetRequiredService<IIngredientServiceClient>();
    var cache = sp.GetRequiredService<HybridCacheService>();
    var logger = sp.GetRequiredService<ILogger<IngredientRepository>>();
    return new IngredientRepository(connectionString, client, cache, logger);
});
builder.Services.AddScoped<IBaseIngredientRepository>(sp => new BaseIngredientRepository(connectionString));
builder.Services.AddScoped<IStoreRepository>(sp => new StoreRepository(connectionString));
builder.Services.AddScoped<ICouponRepository>(sp => new CouponRepository(connectionString));
builder.Services.AddScoped<IProductStagingRepository>(sp => 
    new ProductStagingRepository(connectionString, sp.GetRequiredService<ILogger<ProductStagingRepository>>()));
builder.Services.AddScoped<IAllergenRepository>(sp => new AllergenRepository(connectionString));

// Register OpenFoodFacts import service
builder.Services.AddHttpClient<OpenFoodFactsImportService>();
builder.Services.AddScoped<OpenFoodFactsImportService>(sp => 
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenFoodFactsImportService));
    var productRepo = sp.GetRequiredService<IProductRepository>();
    var stagingRepo = sp.GetRequiredService<IProductStagingRepository>();
    var imageRepo = sp.GetRequiredService<IProductImageRepository>();
    var logger = sp.GetRequiredService<ILogger<OpenFoodFactsImportService>>();
    var ingredientClient = sp.GetRequiredService<IIngredientServiceClient>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new OpenFoodFactsImportService(httpClient, productRepo, stagingRepo, imageRepo, logger, ingredientClient, configuration);
});
builder.Services.AddScoped<USDAFoodDataImportService>();

// Register background workers
builder.Services.AddHostedService<ExpressRecipe.ProductService.Workers.ProductDataImportWorker>();
builder.Services.AddHostedService<ExpressRecipe.ProductService.Workers.ProductProcessingWorker>();

// Register AI verification service
builder.Services.AddScoped<IProductAIVerificationService, ProductAIVerificationService>();

// Register RabbitMQ messaging (IMessageBus) - conditional based on Aspire connection string
var messagingRequested = builder.Configuration.GetValue<bool>("Messaging:Enabled", true);
var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
var messagingEnabled = messagingRequested && !string.IsNullOrWhiteSpace(messagingConnectionString);

if (messagingEnabled)
{
    builder.AddRabbitMqMessaging("messaging");

    builder.Services.AddSqlSagaRepository<ProductProcessingSagaState>(connectionString, "ProductProcessingSagaState");
    builder.Services.AddSqlSagaRepository<ImportSessionSagaState>(connectionString, "ImportSessionSagaState");
    builder.Services.AddSagaWorkflow(ProductProcessingWorkflow.Build());

    // Real publisher – uses the IMessageBus registered above
    builder.Services.AddSingleton<IProductEventPublisher, ProductEventPublisher>();

    // Handle barcode query messages from PriceService and other consumers
    builder.Services.AddScoped<ProductQueryHandler>();
    builder.Services.AddHostedService<ProductQuerySubscriber>();

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
    // No-op publisher so the controller DI never fails
    builder.Services.AddSingleton<IProductEventPublisher, NullProductEventPublisher>();
}

// Register product batch channel (async path) – always available regardless of messaging
builder.Services.AddSingleton<IProductBatchChannel, ProductBatchChannel>();
builder.Services.AddHostedService<ProductBatchChannelWorker>();

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
await app.RunDatabaseManagementAsync("ProductService", "productdb");

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
