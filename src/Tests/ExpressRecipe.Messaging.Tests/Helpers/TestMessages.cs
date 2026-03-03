using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Messaging.Tests.Helpers;

// Simple test message types shared across test classes

public sealed record SimpleMessage(string Text, int Value) : IMessage;
public sealed record AnotherMessage(Guid Id, string Name) : IMessage;
public sealed record RequestMessage(string Query) : IMessage;
public sealed record ResponseMessage(string Answer, bool Success) : IMessage;
public sealed record ComplexMessage(
    Guid Id,
    string Name,
    int[] Numbers,
    Dictionary<string, string> Metadata,
    DateTimeOffset Timestamp) : IMessage;
