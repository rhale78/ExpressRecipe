using ExpressRecipe.Data.Common;
using ExpressRecipe.AIService.Configuration;
using ExpressRecipe.AIService.Data;
using ExpressRecipe.AIService.Providers;
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

// ── AI Provider infrastructure ───────────────────────────────────────────────

// Local-mode flag (APP_LOCAL_MODE=true disables cloud provider HTTP calls)
builder.Services.AddSingleton<ILocalModeConfig, LocalModeConfig>();

// Register all AI providers
builder.Services.AddSingleton<IAIProvider, OllamaProvider>();
builder.Services.AddSingleton<IAIProvider, ClaudeProvider>();
builder.Services.AddSingleton<IAIProvider, OpenAIProvider>();
builder.Services.AddSingleton<IAIProvider, GeminiProvider>();
builder.Services.AddSingleton<IAIProvider, AzureOpenAIProvider>();

// Provider factory (config-driven per use case with HybridCache)
builder.Services.AddSingleton<IAIProviderFactory, AIProviderFactory>();

// Data repositories for provider config and approval queue
var aiConnectionString = builder.Configuration.GetConnectionString("aidb")
    ?? builder.Configuration.GetConnectionString("SqlServer");

if (string.IsNullOrWhiteSpace(aiConnectionString))
{
    if (builder.Environment.IsDevelopment())
    {
        // Safe development-only fallback using LocalDB with integrated security (no hardcoded password)
        aiConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=ExpressRecipe;Trusted_Connection=True;TrustServerCertificate=True;";
    }
    else
    {
        throw new InvalidOperationException(
            "Database connection string for 'aidb' or 'SqlServer' is not configured. " +
            "Please configure it via configuration files, environment variables, or a secure secrets store.");
    }
}

builder.Services.AddSingleton<IAIProviderConfigRepository>(
    _ => new AIProviderConfigRepository(aiConnectionString));

builder.Services.AddSingleton<IApprovalQueueRepository>(
    _ => new ApprovalQueueRepository(aiConnectionString));

// HttpClient for NotificationService used by ApprovalQueueService
builder.Services.AddHttpClient("NotificationService", client =>
{
    string? baseUrl = builder.Configuration["Services:NotificationService"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
        client.BaseAddress = new Uri(baseUrl);

    string? internalApiKey = builder.Configuration["InternalApi:Key"];
    if (!string.IsNullOrWhiteSpace(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
});

// Approval queue service
builder.Services.AddScoped<IApprovalQueueService, ApprovalQueueService>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Run DB management and migrations (same pattern as other services)
await app.RunDatabaseManagementAsync("AIService", "aidb");

string migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");

var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(aiConnectionString, migrations);

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
