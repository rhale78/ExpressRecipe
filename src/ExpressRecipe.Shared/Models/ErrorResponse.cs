using System.Text.Json.Serialization;

namespace ExpressRecipe.Shared.Models;

/// <summary>
/// Standardized error response for all API services
/// </summary>
public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? TraceId { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ValidationError>? Errors { get; set; }

    public static ErrorResponse Create(string message, string? code = null, string? traceId = null)
    {
        return new ErrorResponse
        {
            Message = message,
            Code = code,
            TraceId = traceId
        };
    }
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
