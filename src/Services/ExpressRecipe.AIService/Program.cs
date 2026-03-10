using ExpressRecipe.Data.Common;
using ExpressRecipe.AIService.Data;
using ExpressRecipe.AIService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ExpressRecipe.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container
// Add controllers
builder.Services.AddControllers();

// Add Swagger
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new() { Title = "ExpressRecipe.AIService API", Version = "v1" });
// });

// Configure JWT Authentication
builder.AddExpressRecipeAuthentication();

// Add CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

// Configure HTTP Client for Ollama with proper settings
var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromMinutes(2); // AI inference can take time
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
        MaxConnectionsPerServer = 5
    };
});

// Register Ollama Service
builder.Services.AddScoped<IOllamaService, OllamaService>();

// Register AI provider factory and cooking assistant service
builder.Services.AddScoped<IAIProviderFactory, AIProviderFactory>();
builder.Services.AddScoped<ICookingAssistantService, CookingAssistantService>();

// Register grounding repository — optional DB connection (aidb)
var aiConnectionString = builder.Configuration.GetConnectionString("aidb");
if (!string.IsNullOrEmpty(aiConnectionString))
{
    builder.Services.AddSingleton<IGroundingRepository>(
        new GroundingRepository(aiConnectionString));
}
else
{
    builder.Services.AddSingleton<IGroundingRepository, NullGroundingRepository>();
}

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Run database management (drop db/tables if configured)
if (!string.IsNullOrEmpty(aiConnectionString))
{
    await app.RunDatabaseManagementAsync("AIService", "aidb");

    // Run migrations
    var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
    if (!Directory.Exists(migrationsPath))
    {
        migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
    }
    var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
    await app.RunMigrationsAsync(aiConnectionString, migrations);
}

// Configure middleware pipeline
app.MapDefaultEndpoints();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
