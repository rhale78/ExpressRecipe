using ExpressRecipe.Messaging.Core.Serialization;
using ExpressRecipe.Messaging.Tests.Helpers;

namespace ExpressRecipe.Messaging.Tests.Serialization;

public class JsonMessageSerializerTests
{
    private readonly JsonMessageSerializer _serializer = new();

    [Fact]
    public void Serialize_SimpleMessage_ReturnsNonEmptyBytes()
    {
        var msg = new SimpleMessage("hello", 42);
        var bytes = _serializer.Serialize(msg);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Deserialize_Generic_ReturnsOriginalMessage()
    {
        var original = new SimpleMessage("round-trip", 99);
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<SimpleMessage>(bytes);
        Assert.Equal(original.Text, result.Text);
        Assert.Equal(original.Value, result.Value);
    }

    [Fact]
    public void Deserialize_NonGeneric_ReturnsCorrectType()
    {
        var original = new AnotherMessage(Guid.NewGuid(), "test");
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize(bytes, typeof(AnotherMessage));
        var typed = Assert.IsType<AnotherMessage>(result);
        Assert.Equal(original.Name, typed.Name);
    }

    [Fact]
    public void Serialize_ComplexMessage_RoundTrips()
    {
        var original = new ComplexMessage(
            Guid.NewGuid(),
            "Complex",
            [1, 2, 3],
            new Dictionary<string, string> { ["key"] = "value" },
            DateTimeOffset.UtcNow);

        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<ComplexMessage>(bytes);

        Assert.Equal(original.Id, result.Id);
        Assert.Equal(original.Name, result.Name);
        Assert.Equal(original.Numbers, result.Numbers);
        Assert.Equal(original.Metadata["key"], result.Metadata["key"]);
    }

    [Fact]
    public void Deserialize_InvalidBytes_ThrowsException()
    {
        var invalidBytes = "not-json"u8.ToArray();
        Assert.Throws<System.Text.Json.JsonException>(
            () => _serializer.Deserialize<SimpleMessage>(invalidBytes));
    }

    [Fact]
    public void Serialize_NullableProperties_HandledCorrectly()
    {
        var msg = new AnotherMessage(Guid.Empty, string.Empty);
        var bytes = _serializer.Serialize(msg);
        Assert.NotNull(bytes);
        var result = _serializer.Deserialize<AnotherMessage>(bytes);
        Assert.Equal(Guid.Empty, result.Id);
    }
}
