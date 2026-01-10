using ExpressRecipe.Data.Common;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
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

// Register hybrid cache service
builder.Services.AddSingleton<HybridCacheService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
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
var connectionString = builder.Configuration.GetConnectionString("productdb")
    ?? throw new InvalidOperationException("Database connection string 'productdb' not found");

builder.Services.AddScoped<IProductImageRepository>(sp => 
    new ProductImageRepository(connectionString, sp.GetRequiredService<ILogger<ProductImageRepository>>()));
builder.Services.AddScoped<IProductRepository>(sp => 
{
    var cache = sp.GetRequiredService<HybridCacheService>();
    var logger = sp.GetRequiredService<ILogger<ProductRepository>>();
    return new ProductRepository(connectionString, sp.GetRequiredService<IProductImageRepository>(), cache, logger);
});
builder.Services.AddScoped<IIngredientRepository>(sp =>
{
    var cache = sp.GetRequiredService<HybridCacheService>();
    var logger = sp.GetRequiredService<ILogger<IngredientRepository>>();
    return new IngredientRepository(connectionString, cache, logger);
});
builder.Services.AddScoped<IRestaurantRepository>(sp => new RestaurantRepository(connectionString));
builder.Services.AddScoped<IMenuItemRepository>(sp => new MenuItemRepository(connectionString));
builder.Services.AddScoped<IBaseIngredientRepository>(sp => new BaseIngredientRepository(connectionString));
builder.Services.AddScoped<IStoreRepository>(sp => new StoreRepository(connectionString));
builder.Services.AddScoped<ICouponRepository>(sp => new CouponRepository(connectionString));
builder.Services.AddScoped<IProductStagingRepository>(sp => new ProductStagingRepository(connectionString));
builder.Services.AddScoped<IAllergenRepository>(sp => new AllergenRepository(connectionString));

// Register parsers
builder.Services.AddScoped<IIngredientParser, IngredientParser>(); // For parsing individual ingredient components
builder.Services.AddSingleton<IIngredientListParser, AdvancedIngredientParser>(); // For parsing full ingredient lists

// Register OpenFoodFacts import service
builder.Services.AddHttpClient<OpenFoodFactsImportService>();
builder.Services.AddScoped<OpenFoodFactsImportService>(sp => 
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenFoodFactsImportService));
    var productRepo = sp.GetRequiredService<IProductRepository>();
    var stagingRepo = sp.GetRequiredService<IProductStagingRepository>();
    var imageRepo = sp.GetRequiredService<IProductImageRepository>();
    var logger = sp.GetRequiredService<ILogger<OpenFoodFactsImportService>>();
    var ingredientParser = sp.GetRequiredService<IIngredientListParser>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new OpenFoodFactsImportService(httpClient, productRepo, stagingRepo, imageRepo, logger, ingredientParser, configuration);
});
builder.Services.AddScoped<USDAFoodDataImportService>();

// Register background workers
builder.Services.AddHostedService<ExpressRecipe.ProductService.Workers.ProductDataImportWorker>();
builder.Services.AddHostedService<ExpressRecipe.ProductService.Workers.ProductProcessingWorker>();

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
