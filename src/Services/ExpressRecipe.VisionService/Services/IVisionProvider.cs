namespace ExpressRecipe.VisionService.Services;

public interface IVisionProvider
{
    string ProviderName { get; }
    bool IsEnabled { get; }
    Task<VisionResult> AnalyzeAsync(byte[] imageBytes, CancellationToken ct = default);
    Task<List<VisionResult>> AnalyzeMultiItemAsync(byte[] imageBytes, CancellationToken ct = default);
}
