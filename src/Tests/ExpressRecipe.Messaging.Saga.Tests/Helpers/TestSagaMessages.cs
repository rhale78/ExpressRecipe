using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.Messaging.Saga.Tests.Helpers;

// Commands (sent by steps)
public sealed record StartVirusScanCommand(string DocumentId) : IMessage;
public sealed record ValidateContentCommand(string DocumentId) : IMessage;
public sealed record GenerateThumbnailCommand(string DocumentId) : IMessage;
public sealed record IndexDocumentCommand(string DocumentId, string Title) : IMessage;

// Results (received by step handlers)
public sealed record VirusScanCompleted(string DocumentId, bool IsClean) : IMessage;
public sealed record ContentValidated(string DocumentId, bool IsValid) : IMessage;
public sealed record ThumbnailGenerated(string DocumentId, string ThumbnailUrl) : IMessage;
public sealed record DocumentIndexed(string DocumentId) : IMessage;

// Sample saga state
public sealed class DocumentProcessingState : ISagaState
{
    public string CorrelationId { get; set; } = string.Empty;
    public long CurrentMask { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public SagaStatus Status { get; set; }

    // Domain-specific properties
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsClean { get; set; }
    public bool IsValid { get; set; }
    public string? ThumbnailUrl { get; set; }
}
