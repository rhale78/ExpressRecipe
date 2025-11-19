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

// ========================================
// Frontend Applications
// ========================================

// Blazor Web - Main web application
var webApp = builder.AddProject<Projects.ExpressRecipe_BlazorWeb>("webapp")
    .WithReference(authService)
    .WithReference(userService)
    .WithReference(productService)
    .WithReference(recipeService)
    .WithReference(redis)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// ========================================
// Build and Run
// ========================================

builder.Build().Run();
