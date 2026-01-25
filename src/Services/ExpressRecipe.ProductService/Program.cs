using ExpressRecipe.Data.Common;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.ProductService.Entities;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using System.Text;
using System.Diagnostics;
using HighSpeedDAL.Core;
using HighSpeedDAL.Core.InMemoryTable;
using HighSpeedDAL.Core.Interfaces;
using HighSpeedDAL.Core.Resilience;
using HighSpeedDAL.SqlServer;
using Microsoft.Data.SqlClient;

try
{
    Trace.WriteLine("[ProductService] ===== STARTUP BEGIN =====");
    Trace.WriteLine("[ProductService] Creating builder...");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    Trace.WriteLine("[ProductService] Loading configuration...");
    builder.AddLayeredConfiguration(args);

    Trace.WriteLine("[ProductService] Adding service defaults...");
    builder.AddServiceDefaults();

    // Note: Don't add Serilog console sink - let ASP.NET Core default logging handle console output
    // This allows Aspire dashboard to apply proper coloring. Serilog is configured via appsettings.json
    // only for structured logging enrichment if needed.

    Trace.WriteLine("[ProductService] Adding database connections...");
    builder.AddSqlServerClient("productdb");
    builder.AddRedisClient("cache");
    builder.AddHybridCache();

    Trace.WriteLine("[ProductService] Registering services...");
    builder.Services.AddSingleton<HybridCacheService>();
    builder.Services.AddSingleton<ProductDatabaseConnection>();
    builder.Services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();

    builder.Services.AddSingleton<RetryPolicyFactory>(sp =>
    {
        ILogger<RetryPolicyFactory> logger = sp.GetRequiredService<ILogger<RetryPolicyFactory>>();
        return new RetryPolicyFactory(logger, maxRetryAttempts: 3, delayMilliseconds: 100);
    });

    builder.Services.AddSingleton<HighSpeedDAL.Core.Resilience.DatabaseRetryPolicy>(sp =>
    {
        RetryPolicyFactory factory = sp.GetRequiredService<RetryPolicyFactory>();
        return factory.CreatePolicy();
    });

    // Register DalMetricsCollector only if metrics are enabled
    var enableMetrics = builder.Configuration.GetValue<bool>("Features:EnableMetrics", defaultValue: true);
    if (enableMetrics)
    {
        builder.Services.AddSingleton<DalMetricsCollector>(sp =>
        {
            return new DalMetricsCollector("ExpressRecipe");
        });
    }
    else
    {
        // Register a no-op version for DI purposes
        builder.Services.AddSingleton<DalMetricsCollector>(sp =>
        {
            return new DalMetricsCollector("ExpressRecipe"); // Still create it, just won't be used
        });
    }

    builder.Services.AddSingleton<InMemoryTableManager>(sp =>
    {
        ILogger<InMemoryTableManager> logger = sp.GetRequiredService<ILogger<InMemoryTableManager>>();
        ProductDatabaseConnection connectionFactory = sp.GetRequiredService<ProductDatabaseConnection>();
        return new InMemoryTableManager(logger, () =>
        {
            SqlConnection conn = new Microsoft.Data.SqlClient.SqlConnection(connectionFactory.ConnectionString);
            return conn;
        });
    });

    // PERFORMANCE: Register DALs as Singleton so in-memory tables are shared across all scopes
    // This prevents reloading millions of rows on each batch. With Scoped, a new DAL instance
    // is created per scope, causing full table reload from database (46s for ProductStaging with 2.6M rows)
    builder.Services.AddSingleton<ProductEntityDal>();
    builder.Services.AddSingleton<IngredientEntityDal>();
    builder.Services.AddSingleton<ProductImageEntityDal>();
    builder.Services.AddSingleton<ProductStagingEntityDal>();
    builder.Services.AddSingleton<ProductAllergenEntityDal>();

    Trace.WriteLine("[ProductService] Configuring authentication...");
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

    var connectionString = builder.Configuration.GetConnectionString("productdb")
        ?? throw new InvalidOperationException("Database connection string 'productdb' not found");

    // Register remaining DALs as Singleton (for consistency with performance optimization above)
    builder.Services.AddSingleton<ProductIngredientEntityDal>();
    builder.Services.AddSingleton<ProductLabelEntityDal>();
    builder.Services.AddSingleton<ProductExternalLinkEntityDal>();
    builder.Services.AddSingleton<ProductMetadataEntityDal>();

    // Repositories can remain Scoped since they delegate to Singleton DALs
    builder.Services.AddScoped<IProductRepository, ProductRepositoryAdapter>();
    builder.Services.AddScoped<IIngredientRepository, IngredientRepositoryAdapter>();
    builder.Services.AddScoped<IProductImageRepository, ProductImageRepositoryAdapter>();
    builder.Services.AddScoped<IProductStagingRepository, ProductStagingRepositoryAdapter>();
    builder.Services.AddScoped<IAllergenRepository, AllergenRepositoryAdapter>();
    builder.Services.AddScoped<ProductSearchAdapter>();

    builder.Services.AddScoped<IRestaurantRepository>(sp => new RestaurantRepository(connectionString));
    builder.Services.AddScoped<IMenuItemRepository>(sp => new MenuItemRepository(connectionString));
    builder.Services.AddScoped<IBaseIngredientRepository>(sp => new BaseIngredientRepository(connectionString));
    builder.Services.AddScoped<IStoreRepository>(sp => new StoreRepository(connectionString));
    builder.Services.AddScoped<ICouponRepository>(sp => new CouponRepository(connectionString));

    builder.Services.AddScoped<IIngredientParser, IngredientParser>();
    builder.Services.AddSingleton<IIngredientListParser, AdvancedIngredientParser>();

    builder.Services.AddHttpClient<OpenFoodFactsImportService>();
    builder.Services.AddScoped<OpenFoodFactsImportService>(sp =>
    {
        HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenFoodFactsImportService));
        IProductRepository productRepo = sp.GetRequiredService<IProductRepository>();
        IProductStagingRepository stagingRepo = sp.GetRequiredService<IProductStagingRepository>();
        IProductImageRepository imageRepo = sp.GetRequiredService<IProductImageRepository>();
        ILogger<OpenFoodFactsImportService> logger = sp.GetRequiredService<ILogger<OpenFoodFactsImportService>>();
        IIngredientListParser ingredientParser = sp.GetRequiredService<IIngredientListParser>();
        IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
        return new OpenFoodFactsImportService(httpClient, productRepo, stagingRepo, imageRepo, logger, ingredientParser, configuration);
    });
    builder.Services.AddScoped<USDAFoodDataImportService>();

    builder.Services.AddHostedService<ExpressRecipe.ProductService.Workers.ProductDataImportWorker>();
    builder.Services.AddHostedService<ExpressRecipe.ProductService.Workers.ProductProcessingWorker>();

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

    builder.Services.AddSingleton<EventPublisher>();
    builder.Services.AddControllers();

    builder.Services.AddHostedService<ExpressRecipe.ProductService.Services.ProductTableInitializer>();

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

    Trace.WriteLine("[ProductService] Building application...");
    WebApplication app = builder.Build();

    Trace.WriteLine("[ProductService] Application built successfully");

    Trace.WriteLine("[ProductService] Running database management tasks...");
    Task dbMgmtTask = app.RunDatabaseManagementAsync("ProductService", "productdb");
    if (await Task.WhenAny(dbMgmtTask, Task.Delay(TimeSpan.FromSeconds(30))) == dbMgmtTask)
    {
        await dbMgmtTask;
        Trace.WriteLine("[ProductService] Database management completed");
    }
    else
    {
        Trace.WriteLine("[ProductService] Database management timed out, continuing...");
    }

    IConfigurationSection dbMgmtSection = app.Configuration.GetSection("DatabaseManagement");
    var enableMigrationRunner = dbMgmtSection.GetValue<bool>("EnableMigrationRunner");
    if (enableMigrationRunner)
    {
        Trace.WriteLine("[ProductService] Running database migrations...");
        var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
        if (!Directory.Exists(migrationsPath))
        {
            migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
        }
        Dictionary<string, string> migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
        Task migrationsTask = app.RunMigrationsAsync(connectionString, migrations);
        if (await Task.WhenAny(migrationsTask, Task.Delay(TimeSpan.FromSeconds(30))) == migrationsTask)
        {
            await migrationsTask;
            Trace.WriteLine("[ProductService] Migrations completed");
        }
        else
        {
            Trace.WriteLine("[ProductService] Migrations timed out, continuing...");
        }
    }

    app.MapDefaultEndpoints();

    if (app.Environment.IsDevelopment())
    {
    }

    app.UseCors();
    app.UseRateLimiting(new RateLimitOptions
    {
        Enabled = true,
        MaxRequestsPerWindow = 100,
        WindowSeconds = 60
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    Trace.WriteLine("[ProductService] ===== STARTUP COMPLETE, RUNNING =====");
    app.Run();
}
catch (Exception ex)
{
    Trace.WriteLine($"[ProductService] FATAL ERROR DURING STARTUP: {ex}");
    Trace.WriteLine($"[ProductService] Exception Type: {ex.GetType().Name}");
    Trace.WriteLine($"[ProductService] Message: {ex.Message}");
    Trace.WriteLine($"[ProductService] StackTrace: {ex.StackTrace}");
    throw;
}
