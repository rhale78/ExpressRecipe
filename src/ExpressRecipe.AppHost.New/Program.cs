var builder = DistributedApplication.CreateBuilder(args);

// ========================================
// Infrastructure Services
// ========================================

// SQL Server - Primary cloud database
var sqlServer = builder.AddSqlServer("sqlserver", port: 1436)
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
var ingredientDb = sqlServer.AddDatabase("ingredientdb", "ExpressRecipe.Ingredients");
var groceryStoreDb = sqlServer.AddDatabase("grocerystoredb", "ExpressRecipe.GroceryStores");

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
    .WithReference(redis);

// Ingredient Service - Centralized ingredient and allergen data
var ingredientService = builder.AddProject<Projects.ExpressRecipe_IngredientService>("ingredientservice")
    .WithReference(ingredientDb)
    .WithReference(redis);

// Grocery Store Location Service - Store locator and location data
var groceryStoreService = builder.AddProject<Projects.ExpressRecipe_GroceryStoreLocationService>("grocerystoreservice")
    .WithReference(groceryStoreDb)
    .WithReference(redis);

// User Service - User profiles and dietary restrictions
var userService = builder.AddProject<Projects.ExpressRecipe_UserService>("userservice")
    .WithReference(userDb)
    .WithReference(redis)
    .WithReference(messaging);

// Product Service - Product catalog and ingredients
var productService = builder.AddProject<Projects.ExpressRecipe_ProductService>("productservice")
    .WithReference(productDb)
    .WithReference(ingredientDb)
    .WithReference(ingredientService)
    .WithReference(groceryStoreService)
    .WithReference(redis)
    .WithReference(messaging);

// Recipe Service - Recipe management
var recipeService = builder.AddProject<Projects.ExpressRecipe_RecipeService>("recipeservice")
    .WithReference(recipeDb)
    .WithReference(ingredientDb)
    .WithReference(ingredientService)
    .WithReference(redis)
    .WithReference(messaging);

// Inventory Service - Inventory tracking and expiration alerts
var inventoryService = builder.AddProject<Projects.ExpressRecipe_InventoryService>("inventoryservice")
    .WithReference(inventoryDb)
    .WithReference(redis)
    .WithReference(messaging);

// Scanner Service - Barcode scanning and allergen alerts
var scannerService = builder.AddProject<Projects.ExpressRecipe_ScannerService>("scannerservice")
    .WithReference(scanDb)
    .WithReference(redis)
    .WithReference(messaging);

// Shopping Service - Shopping list management
var shoppingService = builder.AddProject<Projects.ExpressRecipe_ShoppingService>("shoppingservice")
    .WithReference(shoppingDb)
    .WithReference(redis)
    .WithReference(messaging);

// Meal Planning Service - Meal calendar and nutritional tracking
var mealPlanningService = builder.AddProject<Projects.ExpressRecipe_MealPlanningService>("mealplanningservice")
    .WithReference(mealPlanDb)
    .WithReference(redis)
    .WithReference(messaging);

// Price Service - Price tracking and deal alerts
var priceService = builder.AddProject<Projects.ExpressRecipe_PriceService>("priceservice")
    .WithReference(priceDb)
    .WithReference(productService)
    .WithReference(groceryStoreService)
    .WithReference(redis)
    .WithReference(messaging);

// Recall Service - FDA/USDA recall monitoring
var recallService = builder.AddProject<Projects.ExpressRecipe_RecallService>("recallservice")
    .WithReference(recallDb)
    .WithReference(redis)
    .WithReference(messaging);

// Notification Service - Push, email, and in-app notifications
var notificationService = builder.AddProject<Projects.ExpressRecipe_NotificationService>("notificationservice")
    .WithReference(notificationDb)
    .WithReference(redis)
    .WithReference(messaging);

// Community Service - User contributions and reviews
var communityService = builder.AddProject<Projects.ExpressRecipe_CommunityService>("communityservice")
    .WithReference(communityDb)
    .WithReference(redis)
    .WithReference(messaging);

// Sync Service - Local-first sync and conflict resolution
var syncService = builder.AddProject<Projects.ExpressRecipe_SyncService>("syncservice")
    .WithReference(syncDb)
    .WithReference(redis)
    .WithReference(messaging);

// Search Service - Full-text search and recommendations
var searchService = builder.AddProject<Projects.ExpressRecipe_SearchService>("searchservice")
    .WithReference(searchDb)
    .WithReference(redis)
    .WithReference(messaging);

// Analytics Service - Usage tracking and insights
var analyticsService = builder.AddProject<Projects.ExpressRecipe_AnalyticsService>("analyticsservice")
    .WithReference(analyticsDb)
    .WithReference(redis)
    .WithReference(messaging);

// AI Service - AI-powered features (recipe suggestions, meal planning)
var aiService = builder.AddProject<Projects.ExpressRecipe_AIService>("aiservice")
    .WithReference(redis)
    .WithReference(messaging);

// ========================================
// Frontend Applications
// ========================================

// Blazor Web - Main web application
var webApp = builder.AddProject<Projects.ExpressRecipe_BlazorWeb>("webapp")
    .WithReference(authService)
    .WithReference(userService)
    .WithReference(ingredientService)
    .WithReference(productService)
    .WithReference(recipeService)
    .WithReference(inventoryService)
    .WithReference(scannerService)
    .WithReference(shoppingService)
    .WithReference(mealPlanningService)
    .WithReference(priceService)
    .WithReference(groceryStoreService)
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
