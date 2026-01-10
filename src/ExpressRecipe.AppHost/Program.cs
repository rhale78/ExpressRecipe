// MINIMAL APPHOST CONFIGURATION FOR DIAGNOSTICS
// 
// To use this file:
// 1. Rename Program.cs to Program.Full.cs
// 2. Rename this file to Program.cs
// 3. Run: dotnet run
// 4. When done, reverse the renames
//
// This minimal configuration starts only:
// - SQL Server (1 container)
// - Redis (1 container)  
// - AuthService (1 service)
//
// Use this to test if Docker and basic Aspire functionality work
// before trying the full 14-service configuration.

using Microsoft.Extensions.Hosting;
using Aspire.Hosting;

Console.WriteLine("Starting MINIMAL ExpressRecipe AppHost for diagnostics...");

var builder = DistributedApplication.CreateBuilder(args);

Console.WriteLine("Builder created");

//// Minimal setup - just infrastructure
//Console.WriteLine("Adding SQL Server...");
//var sqlServer = builder.AddSqlServer("sqlserver")
//    .WithLifetime(ContainerLifetime.Persistent);

//Console.WriteLine("Adding one test database...");
//var authDb = sqlServer.AddDatabase("authdb", "ExpressRecipe.Auth");

//Console.WriteLine("Adding Redis...");
//var redis = builder.AddRedis("redis")
//    .WithLifetime(ContainerLifetime.Persistent);

//Console.WriteLine("Adding one test service (AuthService)...");
//var authService = builder.AddProject<Projects.ExpressRecipe_AuthService>("authservice")
//    .WithReference(authDb)
//    .WithReference(redis)
//    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

Console.WriteLine("Building...");
var app = builder.Build();

Console.WriteLine("Starting... Check Docker Desktop to see if containers are starting.");
Console.WriteLine("Dashboard should be at: https://localhost:15000");

app.Run();
