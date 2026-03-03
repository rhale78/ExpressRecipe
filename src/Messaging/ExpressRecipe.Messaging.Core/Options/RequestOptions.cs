namespace ExpressRecipe.Messaging.Core.Options;

/// <summary>
/// Options that control the behaviour of a request/response operation.
/// </summary>
public sealed class RequestOptions
{
    /// <summary>Gets or sets how long to wait for a response before timing out. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets an optional correlation identifier. If null, one is generated automatically.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets optional custom headers to include with the request message.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets whether the request message is persisted to disk.
    /// Defaults to <c>false</c> because requests are typically transient.
    /// </summary>
    public bool Persistent { get; set; } = false;
}
