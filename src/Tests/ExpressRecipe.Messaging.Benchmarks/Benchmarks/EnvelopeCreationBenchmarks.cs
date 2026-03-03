using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.Core.Serialization;

namespace ExpressRecipe.Messaging.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for the overhead of constructing a <see cref="MessageEnvelope"/>.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class EnvelopeCreationBenchmarks
{
    private readonly JsonMessageSerializer _serializer = new();
    private readonly SampleEvent _message = new(Guid.NewGuid(), "Benchmark product", "BrandX", 9.99m);

    [Benchmark]
    public MessageEnvelope CreateEnvelopeManually()
    {
        return new MessageEnvelope
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = typeof(SampleEvent).FullName ?? typeof(SampleEvent).Name,
            MessageName = typeof(SampleEvent).Name,
            Payload = _serializer.Serialize(_message),
            Timestamp = DateTimeOffset.UtcNow,
            RoutingMode = RoutingMode.Broadcast,
            Headers = new Dictionary<string, string>()
        };
    }

    [Benchmark]
    public MessageEnvelope CreateEnvelopeWithHeaders()
    {
        return new MessageEnvelope
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            MessageType = typeof(SampleEvent).FullName ?? typeof(SampleEvent).Name,
            MessageName = typeof(SampleEvent).Name,
            Payload = _serializer.Serialize(_message),
            Timestamp = DateTimeOffset.UtcNow,
            RoutingMode = RoutingMode.Broadcast,
            Headers = new Dictionary<string, string>
            {
                ["x-source"] = "benchmark",
                ["x-version"] = "1.0"
            }
        };
    }

    private sealed record SampleEvent(Guid ProductId, string Name, string Brand, decimal Price) : IMessage;
}
