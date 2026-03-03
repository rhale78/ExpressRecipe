using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Serialization;

namespace ExpressRecipe.Messaging.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for JSON serialization and deserialization throughput.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private readonly JsonMessageSerializer _serializer = new();
    private byte[] _serializedBytes = Array.Empty<byte>();

    private readonly SimpleMsg _simpleMessage = new("Hello, World!", 42);
    private readonly ComplexMsg _complexMessage = new(
        Guid.NewGuid(), "Complex Message", [1, 2, 3, 4, 5],
        new Dictionary<string, string> { ["key1"] = "val1", ["key2"] = "val2" },
        DateTimeOffset.UtcNow);

    [GlobalSetup]
    public void Setup()
    {
        _serializedBytes = _serializer.Serialize(_complexMessage);
    }

    [Benchmark]
    public byte[] SerializeSimpleMessage()
        => _serializer.Serialize(_simpleMessage);

    [Benchmark]
    public byte[] SerializeComplexMessage()
        => _serializer.Serialize(_complexMessage);

    [Benchmark]
    public object DeserializeComplexMessageTyped()
        => _serializer.Deserialize<ComplexMsg>(_serializedBytes);

    [Benchmark]
    public object DeserializeNonGeneric()
        => _serializer.Deserialize(_serializedBytes, typeof(ComplexMsg));

    private sealed record SimpleMsg(string Text, int Value) : IMessage;
    private sealed record ComplexMsg(
        Guid Id,
        string Name,
        int[] Numbers,
        Dictionary<string, string> Metadata,
        DateTimeOffset Timestamp) : IMessage;
}
