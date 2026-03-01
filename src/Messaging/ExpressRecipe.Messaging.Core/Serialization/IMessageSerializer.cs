namespace ExpressRecipe.Messaging.Core.Serialization;

/// <summary>
/// Abstracts the serialization and deserialization of message payloads.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>Serializes a message to a byte array.</summary>
    byte[] Serialize<T>(T message);

    /// <summary>Deserializes a byte array to a strongly-typed message.</summary>
    T Deserialize<T>(byte[] data);

    /// <summary>Deserializes a byte array to a message of the given runtime type.</summary>
    object Deserialize(byte[] data, Type type);
}
