var builder = DistributedApplication.CreateBuilder(args);

// ========================================
// Infrastructure Services
// ========================================

// SQL Server - Primary cloud database
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

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

// Redis - Caching layer
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

// RabbitMQ - Message bus for async communication
var messaging = builder.AddRabbitMQ("messaging")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithManagementPlugin(); // Enables RabbitMQ management UI at port 15672

// ========================================
// Microservices
// ========================================

// Auth Service - Authentication and authorization
var authService = builder.AddProject<Projects.ExpressRecipe_AuthService>("authservice")
    .WithReference(authDb)
    .WithReference(redis)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// User Service - User profiles and dietary restrictions
var userService = builder.AddProject<Projects.ExpressRecipe_UserService>("userservice")
    .WithReference(userDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Product Service - Product catalog and ingredients
var productService = builder.AddProject<Projects.ExpressRecipe_ProductService>("productservice")
    .WithReference(productDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Recipe Service - Recipe management
var recipeService = builder.AddProject<Projects.ExpressRecipe_RecipeService>("recipeservice")
    .WithReference(recipeDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Inventory Service - Inventory tracking and expiration alerts
var inventoryService = builder.AddProject<Projects.ExpressRecipe_InventoryService>("inventoryservice")
    .WithReference(inventoryDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Scanner Service - Barcode scanning and allergen alerts
var scannerService = builder.AddProject<Projects.ExpressRecipe_ScannerService>("scannerservice")
    .WithReference(scanDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Shopping Service - Shopping list management
var shoppingService = builder.AddProject<Projects.ExpressRecipe_ShoppingService>("shoppingservice")
    .WithReference(shoppingDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Meal Planning Service - Meal calendar and nutritional tracking
var mealPlanningService = builder.AddProject<Projects.ExpressRecipe_MealPlanningService>("mealplanningservice")
    .WithReference(mealPlanDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Price Service - Price tracking and deal alerts
var priceService = builder.AddProject<Projects.ExpressRecipe_PriceService>("priceservice")
    .WithReference(priceDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Recall Service - FDA/USDA recall monitoring
var recallService = builder.AddProject<Projects.ExpressRecipe_RecallService>("recallservice")
    .WithReference(recallDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Notification Service - Push, email, and in-app notifications
var notificationService = builder.AddProject<Projects.ExpressRecipe_NotificationService>("notificationservice")
    .WithReference(notificationDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Community Service - User contributions and reviews
var communityService = builder.AddProject<Projects.ExpressRecipe_CommunityService>("communityservice")
    .WithReference(communityDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Sync Service - Local-first sync and conflict resolution
var syncService = builder.AddProject<Projects.ExpressRecipe_SyncService>("syncservice")
    .WithReference(syncDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Search Service - Full-text search and recommendations
var searchService = builder.AddProject<Projects.ExpressRecipe_SearchService>("searchservice")
    .WithReference(searchDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Analytics Service - Usage tracking and insights
var analyticsService = builder.AddProject<Projects.ExpressRecipe_AnalyticsService>("analyticsservice")
    .WithReference(analyticsDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// ========================================
// Frontend Applications
// ========================================

// Blazor Web - Main web application
var webApp = builder.AddProject<Projects.ExpressRecipe_BlazorWeb>("webapp")
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
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// ========================================
// Build and Run
// ========================================

builder.Build().Run();
