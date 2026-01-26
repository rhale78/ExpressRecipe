IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// ========================================
// Infrastructure Services
// ========================================

// SQL Server - Primary cloud database
IResourceBuilder<SqlServerServerResource> sqlServer = builder.AddSqlServer("sqlserver", port: 1436)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

// Create databases for each service
IResourceBuilder<SqlServerDatabaseResource> authDb = sqlServer.AddDatabase("authdb", "ExpressRecipe.Auth");
IResourceBuilder<SqlServerDatabaseResource> userDb = sqlServer.AddDatabase("userdb", "ExpressRecipe.Users");
IResourceBuilder<SqlServerDatabaseResource> productDb = sqlServer.AddDatabase("productdb", "ExpressRecipe.Products");
IResourceBuilder<SqlServerDatabaseResource> recipeDb = sqlServer.AddDatabase("recipedb", "ExpressRecipe.Recipes");
IResourceBuilder<SqlServerDatabaseResource> inventoryDb = sqlServer.AddDatabase("inventorydb", "ExpressRecipe.Inventory");
IResourceBuilder<SqlServerDatabaseResource> scanDb = sqlServer.AddDatabase("scandb", "ExpressRecipe.Scans");
IResourceBuilder<SqlServerDatabaseResource> shoppingDb = sqlServer.AddDatabase("shoppingdb", "ExpressRecipe.Shopping");
IResourceBuilder<SqlServerDatabaseResource> mealPlanDb = sqlServer.AddDatabase("mealplandb", "ExpressRecipe.MealPlanning");
IResourceBuilder<SqlServerDatabaseResource> priceDb = sqlServer.AddDatabase("pricedb", "ExpressRecipe.Pricing");
IResourceBuilder<SqlServerDatabaseResource> recallDb = sqlServer.AddDatabase("recalldb", "ExpressRecipe.Recalls");
IResourceBuilder<SqlServerDatabaseResource> notificationDb = sqlServer.AddDatabase("notificationdb", "ExpressRecipe.Notifications");
IResourceBuilder<SqlServerDatabaseResource> communityDb = sqlServer.AddDatabase("communitydb", "ExpressRecipe.Community");
IResourceBuilder<SqlServerDatabaseResource> syncDb = sqlServer.AddDatabase("syncdb", "ExpressRecipe.Sync");
IResourceBuilder<SqlServerDatabaseResource> searchDb = sqlServer.AddDatabase("searchdb", "ExpressRecipe.Search");
IResourceBuilder<SqlServerDatabaseResource> analyticsDb = sqlServer.AddDatabase("analyticsdb", "ExpressRecipe.Analytics");

// Redis - Caching layer
IResourceBuilder<RedisResource> redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

// RabbitMQ - Message bus for async communication
IResourceBuilder<RabbitMQServerResource> messaging = builder.AddRabbitMQ("messaging")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithManagementPlugin(); // Enables RabbitMQ management UI at port 15672

// ========================================
// Microservices
// ========================================

// Auth Service - Authentication and authorization
IResourceBuilder<ProjectResource> authService = builder.AddProject<Projects.ExpressRecipe_AuthService>("authservice")
    .WithReference(authDb)
    .WithReference(redis);

// User Service - User profiles and dietary restrictions
IResourceBuilder<ProjectResource> userService = builder.AddProject<Projects.ExpressRecipe_UserService>("userservice")
    .WithReference(userDb)
    .WithReference(redis)
    .WithReference(messaging);

// Product Service - Product catalog and ingredients
IResourceBuilder<ProjectResource> productService = builder.AddProject<Projects.ExpressRecipe_ProductService>("productservice")
    .WithReference(productDb)
    .WithReference(redis)
    .WithReference(messaging);

// Recipe Service - Recipe management
IResourceBuilder<ProjectResource> recipeService = builder.AddProject<Projects.ExpressRecipe_RecipeService>("recipeservice")
    .WithReference(recipeDb)
    .WithReference(redis)
    .WithReference(messaging);

// Inventory Service - Inventory tracking and expiration alerts
IResourceBuilder<ProjectResource> inventoryService = builder.AddProject<Projects.ExpressRecipe_InventoryService>("inventoryservice")
    .WithReference(inventoryDb)
    .WithReference(redis)
    .WithReference(messaging);

// Scanner Service - Barcode scanning and allergen alerts
IResourceBuilder<ProjectResource> scannerService = builder.AddProject<Projects.ExpressRecipe_ScannerService>("scannerservice")
    .WithReference(scanDb)
    .WithReference(redis)
    .WithReference(messaging);

// Shopping Service - Shopping list management
IResourceBuilder<ProjectResource> shoppingService = builder.AddProject<Projects.ExpressRecipe_ShoppingService>("shoppingservice")
    .WithReference(shoppingDb)
    .WithReference(redis)
    .WithReference(messaging);

// Meal Planning Service - Meal calendar and nutritional tracking
IResourceBuilder<ProjectResource> mealPlanningService = builder.AddProject<Projects.ExpressRecipe_MealPlanningService>("mealplanningservice")
    .WithReference(mealPlanDb)
    .WithReference(redis)
    .WithReference(messaging);

// Price Service - Price tracking and deal alerts
IResourceBuilder<ProjectResource> priceService = builder.AddProject<Projects.ExpressRecipe_PriceService>("priceservice")
    .WithReference(priceDb)
    .WithReference(redis)
    .WithReference(messaging);

// Recall Service - FDA/USDA recall monitoring
IResourceBuilder<ProjectResource> recallService = builder.AddProject<Projects.ExpressRecipe_RecallService>("recallservice")
    .WithReference(recallDb)
    .WithReference(redis)
    .WithReference(messaging);

// Notification Service - Push, email, and in-app notifications
IResourceBuilder<ProjectResource> notificationService = builder.AddProject<Projects.ExpressRecipe_NotificationService>("notificationservice")
    .WithReference(notificationDb)
    .WithReference(redis)
    .WithReference(messaging);

// Community Service - User contributions and reviews
IResourceBuilder<ProjectResource> communityService = builder.AddProject<Projects.ExpressRecipe_CommunityService>("communityservice")
    .WithReference(communityDb)
    .WithReference(redis)
    .WithReference(messaging);

// Sync Service - Local-first sync and conflict resolution
IResourceBuilder<ProjectResource> syncService = builder.AddProject<Projects.ExpressRecipe_SyncService>("syncservice")
    .WithReference(syncDb)
    .WithReference(redis)
    .WithReference(messaging);

// Search Service - Full-text search and recommendations
IResourceBuilder<ProjectResource> searchService = builder.AddProject<Projects.ExpressRecipe_SearchService>("searchservice")
    .WithReference(searchDb)
    .WithReference(redis)
    .WithReference(messaging);

// Analytics Service - Usage tracking and insights
IResourceBuilder<ProjectResource> analyticsService = builder.AddProject<Projects.ExpressRecipe_AnalyticsService>("analyticsservice")
    .WithReference(analyticsDb)
    .WithReference(redis)
    .WithReference(messaging);

// AI Service - AI-powered features (recipe suggestions, meal planning)
IResourceBuilder<ProjectResource> aiService = builder.AddProject<Projects.ExpressRecipe_AIService>("aiservice")
    .WithReference(redis)
    .WithReference(messaging);

// ========================================
// Frontend Applications
// ========================================

// Blazor Web - Main web application
IResourceBuilder<ProjectResource> webApp = builder.AddProject<Projects.ExpressRecipe_BlazorWeb>("webapp")
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
    .WithReference(aiService)
    .WithReference(redis);

// ========================================
// Build and Run
// ========================================

builder.Build().Run();
