using ExpressRecipe.Data.Common;
using ExpressRecipe.RecallService.Data;
using ExpressRecipe.RecallService.Services;
using ExpressRecipe.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication (shared JWT bearer configuration)
builder.AddExpressRecipeAuthentication();

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("recalldb")
    ?? throw new InvalidOperationException("Database connection string 'recalldb' not found");

// Register repositories
builder.Services.AddScoped<IRecallRepository>(sp =>
    new RecallRepository(connectionString, sp.GetRequiredService<ILogger<RecallRepository>>()));

// Configure HttpClient for FDA API
builder.Services.AddHttpClient("FDA", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:FDA:BaseUrl"] ?? "https://api.fda.gov/");
    // Set to infinite when using resilience handler - Polly will manage timeouts
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.ConfigureHttpClient((sp, client) =>
{
    // Add default headers if needed
    client.DefaultRequestHeaders.Add("User-Agent", "ExpressRecipe-RecallService/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 10,
        ConnectTimeout = TimeSpan.FromSeconds(15)
    };
})
.AddStandardResilienceHandler(options =>
{
    // Configure retry policy
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.UseJitter = true;

    // Configure timeout - FDA API is typically fast
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(1);
    
    // Circuit breaker - SamplingDuration must be at least 2x AttemptTimeout
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(90); // 3x AttemptTimeout
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 3;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
});

// NOTE: USDA FSIS no longer provides a public API or RSS feed
// Meat/poultry recalls are imported from FDA API instead
// Keeping "USDA" client name for backward compatibility, but it's not actually used
// The FDA client is used for all recall imports including meat/poultry products

// Register import services
builder.Services.AddScoped<FDARecallImportService>();

// HTTP client for cross-service notification calls
builder.Services.AddHttpClient("notificationservice", client =>
{
    client.BaseAddress = new Uri("http://notificationservice/");
});

// Register background workers
builder.Services.AddHostedService<RecallMonitorWorker>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new() { Title = "ExpressRecipe.RecallService API", Version = "v1" });
// });

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

var app = builder.Build();

// Run database management (drop db/tables if configured)
await app.RunDatabaseManagementAsync("RecallService", "recalldb");

// Run migrations using shared MigrationRunner
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
await app.RunMigrationsAsync(connectionString, migrations);

// Configure middleware pipeline
app.MapDefaultEndpoints(); // Aspire health checks
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

app.Run();
