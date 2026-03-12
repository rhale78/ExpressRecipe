using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Middleware;
using ExpressRecipe.VisionService.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load layered configuration (global + env + local)
builder.AddLayeredConfiguration(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add authentication
builder.AddExpressRecipeAuthentication();

// Bind configuration sections
OnnxVisionOptions onnxOptions = new OnnxVisionOptions();
builder.Configuration.GetSection("Vision:Onnx").Bind(onnxOptions);

PaddleOcrOptions paddleOptions = new PaddleOcrOptions();
builder.Configuration.GetSection("Vision:PaddleOcr").Bind(paddleOptions);

OllamaVisionOptions ollamaOptions = new OllamaVisionOptions();
builder.Configuration.GetSection("Vision:OllamaVision").Bind(ollamaOptions);

AzureVisionOptions azureOptions = new AzureVisionOptions();
builder.Configuration.GetSection("Vision:AzureVision").Bind(azureOptions);

VisionServiceOptions serviceOptions = new VisionServiceOptions();
builder.Configuration.GetSection("Vision").Bind(serviceOptions);

// Register options as singletons
builder.Services.AddSingleton(onnxOptions);
builder.Services.AddSingleton(paddleOptions);
builder.Services.AddSingleton(ollamaOptions);
builder.Services.AddSingleton(azureOptions);
builder.Services.AddSingleton(serviceOptions);

// Named client keys for providers
const string OllamaClientName = "OllamaVisionProvider";
const string AzureClientName = "AzureVisionProvider";

// Register HTTP client for Ollama
string ollamaEndpoint = ollamaOptions.Endpoint;
builder.Services.AddHttpClient(OllamaClientName, client =>
{
    client.BaseAddress = new Uri(ollamaEndpoint);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Register HTTP client for Azure
builder.Services.AddHttpClient(AzureClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register HTTP client for ProductNameMatcher
string productServiceUrl = builder.Configuration["Vision:ProductServiceBaseUrl"] ?? "https+http://productservice";
builder.Services.AddHttpClient<IProductNameMatcher, ProductServiceNameMatcher>(client =>
{
    client.BaseAddress = new Uri(productServiceUrl);
});

// Register providers as singletons (ONNX model loaded once)
builder.Services.AddSingleton<OnnxVisionProvider>();
builder.Services.AddSingleton<PaddleOcrProvider>(sp =>
    new PaddleOcrProvider(
        sp.GetRequiredService<PaddleOcrOptions>(),
        sp.GetRequiredService<IProductNameMatcher>(),
        sp.GetRequiredService<ILogger<PaddleOcrProvider>>()));
builder.Services.AddSingleton<OllamaVisionProvider>(sp =>
{
    IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
    HttpClient client = factory.CreateClient(OllamaClientName);
    return new OllamaVisionProvider(
        sp.GetRequiredService<OllamaVisionOptions>(),
        client,
        sp.GetRequiredService<ILogger<OllamaVisionProvider>>());
});
builder.Services.AddSingleton<AzureVisionProvider>(sp =>
{
    IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
    HttpClient client = factory.CreateClient(AzureClientName);
    return new AzureVisionProvider(
        sp.GetRequiredService<AzureVisionOptions>(),
        client,
        sp.GetRequiredService<ILogger<AzureVisionProvider>>());
});

// Register VisionService orchestrator
builder.Services.AddScoped<IVisionService, ExpressRecipe.VisionService.Services.VisionService>();

// Add controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddServiceCors(builder.Environment, builder.Configuration);

WebApplication app = builder.Build();

// Configure middleware pipeline
app.MapDefaultEndpoints();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors();
app.UseRateLimiting(new RateLimitOptions
{
    Enabled = true,
    MaxRequestsPerWindow = 100,
    WindowSeconds = 60
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
