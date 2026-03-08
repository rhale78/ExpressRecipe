namespace ExpressRecipe.VisionService.Services;

public interface IVisionService
{
    Task<VisionResult> AnalyzeAsync(byte[] imageBytes, VisionOptions options, CancellationToken ct = default);
    Task<List<VisionResult>> AnalyzeMultiItemAsync(byte[] imageBytes, VisionOptions options, CancellationToken ct = default);
    Task<VisionResult> ExtractTextAsync(byte[] imageBytes, CancellationToken ct = default);
    Task<VisionHealthStatus> GetHealthAsync(CancellationToken ct = default);
}
