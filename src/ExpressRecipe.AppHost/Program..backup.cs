using Microsoft.Extensions.Hosting;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// Get logger for startup diagnostics
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

logger.LogInformation("=== Starting ExpressRecipe AppHost ===");
logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);

// ========================================
// Infrastructure Services
// ========================================

logger.LogInformation("Configuring infrastructure services...");

// SQL Server - Primary cloud database
// Using drive I for data storage (14TB available)
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("I:\\DockerVolumes\\ExpressRecipe\\sqlserver");

logger.LogInformation("SQL Server configured (data: I:\\DockerVolumes\\ExpressRecipe\\sqlserver)");

// Create databases for each service
var authDb = sqlServer.AddDatabase("authdb", "ExpressRecipe.Auth");
var userDb = sqlServer.AddDatabase("userdb", "ExpressRecipe.Users");
var productDb = sqlServer.AddDatabase("productdb", "ExpressRecipe.Products");
var recipeDb = sqlServer.AddDatabase("recipedb", "ExpressRecipe.Recipes");
var inventoryDb = sqlServer.AddDatabase("inventorydb", "ExpressRecipe.Inventory");
var scanDb = sqlServer.AddDatabase("scandb", "ExpressRecipe.Scans");
var shoppingDb = sqlServer.AddDatabase("shoppingdb", "ExpressRecipe.Shopping");
var mealPlanDb = sqlServer.AddDatabase("mealplandb", "ExpressRecipe.MealPlanning");
var priceDb = sqlServer.AddDatabase("pricedb", "ExpressRecipe.Pricing");
var recallDb = sqlServer.AddDatabase("recalldb", "ExpressRecipe.Recalls");
var notificationDb = sqlServer.AddDatabase("notificationdb", "ExpressRecipe.Notifications");
var communityDb = sqlServer.AddDatabase("communitydb", "ExpressRecipe.Community");
var syncDb = sqlServer.AddDatabase("syncdb", "ExpressRecipe.Sync");
var searchDb = sqlServer.AddDatabase("searchdb", "ExpressRecipe.Search");
var analyticsDb = sqlServer.AddDatabase("analyticsdb", "ExpressRecipe.Analytics");

logger.LogInformation("15 databases configured");

// Redis - Caching layer
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("I:\\DockerVolumes\\ExpressRecipe\\redis");

logger.LogInformation("Redis configured (data: I:\\DockerVolumes\\ExpressRecipe\\redis)");

// RabbitMQ - Message bus for async communication
var messaging = builder.AddRabbitMQ("messaging")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("I:\\DockerVolumes\\ExpressRecipe\\rabbitmq")
    .WithManagementPlugin(); // Enables RabbitMQ management UI at port 15672

logger.LogInformation("RabbitMQ configured (data: I:\\DockerVolumes\\ExpressRecipe\\rabbitmq)");

// ========================================
// Microservices
// ========================================

logger.LogInformation("Configuring microservices...");

// Auth Service - Authentication and authorization
var authService = builder.AddProject<Projects.ExpressRecipe_AuthService>("authservice", launchProfileName: null)
    .WithReference(authDb)
    .WithReference(redis)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// User Service - User profiles and dietary restrictions
var userService = builder.AddProject<Projects.ExpressRecipe_UserService>("userservice", launchProfileName: null)
    .WithReference(userDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Product Service - Product catalog and ingredients
var productService = builder.AddProject<Projects.ExpressRecipe_ProductService>("productservice", launchProfileName: null)
    .WithReference(productDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Recipe Service - Recipe management
var recipeService = builder.AddProject<Projects.ExpressRecipe_RecipeService>("recipeservice", launchProfileName: null)
    .WithReference(recipeDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Inventory Service - Inventory tracking and expiration alerts
var inventoryService = builder.AddProject<Projects.ExpressRecipe_InventoryService>("inventoryservice", launchProfileName: null)
    .WithReference(inventoryDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Scanner Service - Barcode scanning and allergen alerts
var scannerService = builder.AddProject<Projects.ExpressRecipe_ScannerService>("scannerservice", launchProfileName: null)
    .WithReference(scanDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Shopping Service - Shopping list management
var shoppingService = builder.AddProject<Projects.ExpressRecipe_ShoppingService>("shoppingservice", launchProfileName: null)
    .WithReference(shoppingDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Meal Planning Service - Meal calendar and nutritional tracking
var mealPlanningService = builder.AddProject<Projects.ExpressRecipe_MealPlanningService>("mealplanningservice", launchProfileName: null)
    .WithReference(mealPlanDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Price Service - Price tracking and deal alerts
var priceService = builder.AddProject<Projects.ExpressRecipe_PriceService>("priceservice", launchProfileName: null)
    .WithReference(priceDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Recall Service - FDA/USDA recall monitoring
var recallService = builder.AddProject<Projects.ExpressRecipe_RecallService>("recallservice", launchProfileName: null)
    .WithReference(recallDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Notification Service - Push, email, and in-app notifications
var notificationService = builder.AddProject<Projects.ExpressRecipe_NotificationService>("notificationservice", launchProfileName: null)
    .WithReference(notificationDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Community Service - User contributions and reviews
var communityService = builder.AddProject<Projects.ExpressRecipe_CommunityService>("communityservice", launchProfileName: null)
    .WithReference(communityDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Sync Service - Local-first sync and conflict resolution
var syncService = builder.AddProject<Projects.ExpressRecipe_SyncService>("syncservice", launchProfileName: null)
    .WithReference(syncDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Search Service - Full-text search and recommendations
var searchService = builder.AddProject<Projects.ExpressRecipe_SearchService>("searchservice", launchProfileName: null)
    .WithReference(searchDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Analytics Service - Usage tracking and insights
var analyticsService = builder.AddProject<Projects.ExpressRecipe_AnalyticsService>("analyticsservice", launchProfileName: null)
    .WithReference(analyticsDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

logger.LogInformation("14 microservices configured");

// ========================================
// Frontend Applications
// ========================================

logger.LogInformation("Configuring frontend applications...");

// Blazor Web - Main web application
var webApp = builder.AddProject<Projects.ExpressRecipe_BlazorWeb>("webapp", launchProfileName: null)
    .WithReference(authService)
    .WithReference(userService)
    .WithReference(productService)
    .WithReference(recipeService)
    .WithReference(inventoryService)
    .WithReference(scannerService)
    .WithReference(shoppingService)
    .WithReference(mealPlanningService)
    .WithReference(priceService)
    .WithReference(recallService)
    .WithReference(notificationService)
    .WithReference(communityService)
    .WithReference(syncService)
    .WithReference(searchService)
    .WithReference(analyticsService)
    .WithReference(redis)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

logger.LogInformation("Blazor web app configured");

// ========================================
// Build and Run
// ========================================

logger.LogInformation("Building Aspire application...");
var app = builder.Build();

logger.LogInformation("=== Starting Aspire Orchestration ===");
logger.LogInformation("Dashboard will be available at: https://localhost:15000");
logger.LogInformation("Note: On first run, Docker will download container images (5-15 minutes)");
logger.LogInformation("Check Docker Desktop to monitor container download progress");

app.Run();
