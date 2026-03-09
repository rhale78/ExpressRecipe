namespace ExpressRecipe.VisionService.Services;

/// <summary>
/// Future: pipeline to feed approved corrections into ONNX model retraining.
/// Stub only — not implemented in this phase.
/// </summary>
public interface IUserFeedbackTrainingService
{
    Task SubmitForRetrainingAsync(Guid captureId, string correctLabel, CancellationToken ct);
}

/// <summary>
/// Future: PaddleOCR pipeline specialized for grocery flyer images (Flipp integration).
/// Stub only — not implemented in this phase.
/// </summary>
public interface IFlyerOcrService
{
    Task<List<FlyerItem>> ParseFlyerImageAsync(byte[] imageBytes, CancellationToken ct);
}

public class FlyerItem
{
    public string ProductName { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public decimal? Price { get; init; }
    public string? Unit { get; init; }
    public string? StoreName { get; init; }
    public string[] DetectedText { get; init; } = Array.Empty<string>();
}
