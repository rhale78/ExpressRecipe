using ExpressRecipe.NotificationService.Data;
using ExpressRecipe.NotificationService.Hubs;
using ExpressRecipe.NotificationService.Services;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.Shared.Services;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"] ?? "http://localhost:5000";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

// Register database connection
var connectionString = builder.Configuration.GetConnectionString("notificationdb")
    ?? throw new InvalidOperationException("Database connection string 'notificationdb' not found");

// Register SignalR for real-time notifications
builder.Services.AddSignalR();

// Register broadcast service
builder.Services.AddScoped<NotificationBroadcastService>();

// Register repositories
builder.Services.AddScoped<INotificationRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<NotificationRepository>>();
    var broadcastService = sp.GetRequiredService<NotificationBroadcastService>();
    return new NotificationRepository(connectionString, logger, broadcastService);
});

// Register RabbitMQ for event subscription
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    return new ConnectionFactory
    {
        HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
        Port = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
        DispatchConsumersAsync = true
    };
});

// Register event publisher
builder.Services.AddSingleton<EventPublisher>();

// Register event subscriber as background service
builder.Services.AddHostedService<NotificationEventSubscriber>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ExpressRecipe.NotificationService API", Version = "v1" });
});

// CORS with SignalR support
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "Retry-After");
    });
});

var app = builder.Build();

// Run migrations on startup
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var migrator = new DatabaseMigrator(connectionString, logger);
        await migrator.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to run database migrations");
    }
}

// Configure middleware pipeline
app.MapDefaultEndpoints(); // Aspire health checks

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Add rate limiting middleware
app.UseRateLimiting(new RateLimitOptions
{
    Enabled = true,
    MaxRequestsPerWindow = 100,
    WindowSeconds = 60
});

app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapControllers();

app.Run();
