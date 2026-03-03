using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.Demo.Messages;
using ExpressRecipe.Messaging.Demo.Workers;
using ExpressRecipe.Messaging.RabbitMQ.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .ConfigureServices((context, services) =>
    {
        // ─── Messaging Setup ────────────────────────────────────────────────────
        // Standalone setup (no Aspire). Provide a real AMQP URI to connect.
        // When no RabbitMQ is available, the app will fail at startup (runtime, not compile time).
        var rabbitUri = context.Configuration["RabbitMQ:Uri"] ?? "amqp://guest:guest@localhost/";

        services.AddRabbitMqMessaging(rabbitUri, opts =>
        {
            opts.ServiceName = "demo-service";
            opts.ExchangePrefix = "expressrecipe";
            opts.EnableDeadLetter = true;
        });

        // Register typed handlers so DI can resolve them
        services.AddScoped<InventoryWorker>();
        services.AddScoped<ProductQueryHandler>();

        // ─── Workers ────────────────────────────────────────────────────────────
        services.AddHostedService<PublisherWorker>();

        // Register subscriptions after the bus is built via IHostedService startup hook
        // We use IHostedService.StartAsync to wire subscriptions once the bus is ready.
        services.AddHostedService<SubscriptionSetupWorker>();
    })
    .Build();

await host.RunAsync();

// ─── SubscriptionSetupWorker ─────────────────────────────────────────────────
// Sets up all subscriptions at startup before the publisher runs.
internal sealed class SubscriptionSetupWorker : IHostedService
{
    private readonly ExpressRecipe.Messaging.Core.Abstractions.IMessageBus _bus;
    private readonly ILogger<SubscriptionSetupWorker> _logger;

    public SubscriptionSetupWorker(
        ExpressRecipe.Messaging.Core.Abstractions.IMessageBus bus,
        ILogger<SubscriptionSetupWorker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registering subscriptions...");

        // 1. Broadcast: receive ProductCreatedEvent
        await _bus.SubscribeAsync<ProductCreatedEvent>(
            async (msg, ctx, ct) =>
            {
                Console.WriteLine($"[BROADCAST] ProductCreated: {msg.Name} ({msg.Brand}) @ ${msg.Price}");
                await Task.CompletedTask;
            },
            new SubscribeOptions { RoutingMode = RoutingMode.Broadcast, QueueName = "expressrecipe.broadcast.productcreatedevent.demo-service.queue" },
            cancellationToken);

        // 2. Work queue: ProcessInventoryCommand (competing consumers)
        await _bus.SubscribeAsync<ProcessInventoryCommand, InventoryWorker>(
            new SubscribeOptions { RoutingMode = RoutingMode.CompetingConsumer },
            cancellationToken);

        // 3. Request/response: GetProductQuery → ProductQueryResponse
        await _bus.SubscribeRequestAsync<GetProductQuery, ProductQueryResponse, ProductQueryHandler>(
            new SubscribeOptions { RoutingMode = RoutingMode.CompetingConsumer },
            cancellationToken);

        // 4. Broadcast: receive RecipePublishedEvent (with TTL)
        await _bus.SubscribeAsync<RecipePublishedEvent>(
            async (msg, ctx, ct) =>
            {
                Console.WriteLine($"[BROADCAST] RecipePublished: '{msg.Title}' by {msg.AuthorName} tags=[{string.Join(", ", msg.Tags)}]");
                await Task.CompletedTask;
            },
            new SubscribeOptions { RoutingMode = RoutingMode.Broadcast, QueueName = "expressrecipe.broadcast.recipepublishedevent.demo-service.queue" },
            cancellationToken);

        // 5. ServiceName routing: receive AlertNotification sent to "notification-service"
        await _bus.SubscribeAsync<AlertNotification>(
            async (msg, ctx, ct) =>
            {
                Console.WriteLine($"[SERVICE] AlertNotification [{msg.Severity}]: {msg.Title} - {msg.Message}");
                await Task.CompletedTask;
            },
            new SubscribeOptions { RoutingMode = RoutingMode.ServiceName, ServiceName = "notification-service" },
            cancellationToken);

        _logger.LogInformation("All subscriptions registered.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
