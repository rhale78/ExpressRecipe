using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.Messaging.Core.Serialization;

/// <summary>
/// A high-performance <see cref="IMessageSerializer"/> implementation backed by <c>System.Text.Json</c>.
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly JsonSerializerOptions _options;

    /// <summary>Initializes a new instance with default JSON options.</summary>
    public JsonMessageSerializer() : this(DefaultOptions) { }

    /// <summary>Initializes a new instance with custom JSON options.</summary>
    public JsonMessageSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public byte[] Serialize<T>(T message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, _options);
    }

    /// <inheritdoc />
    public T Deserialize<T>(byte[] data)
    {
        return JsonSerializer.Deserialize<T>(data, _options)
            ?? throw new InvalidOperationException($"Deserialization returned null for type {typeof(T).FullName}.");
    }

    /// <inheritdoc />
    public object Deserialize(byte[] data, Type type)
    {
        return JsonSerializer.Deserialize(data, type, _options)
            ?? throw new InvalidOperationException($"Deserialization returned null for type {type.FullName}.");
    }
}
