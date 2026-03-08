namespace ExpressRecipe.VisionService.Services;

public class PaddleOcrOptions
{
    public bool Enabled { get; set; } = true;
    public string ModelDirectory { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
}

/// <summary>
/// PaddleOCR-based text extraction provider.
/// Native PaddleSharp integration is stubbed; text extraction logic is in place.
/// Returns extracted text blocks for downstream product name matching.
/// </summary>
public class PaddleOcrProvider : IVisionProvider
{
    private readonly PaddleOcrOptions _options;
    private readonly IProductNameMatcher _productMatcher;
    private readonly ILogger<PaddleOcrProvider> _logger;

    public string ProviderName => "PaddleOCR";
    public bool IsEnabled => _options.Enabled;

    public PaddleOcrProvider(PaddleOcrOptions options, IProductNameMatcher productMatcher, ILogger<PaddleOcrProvider> logger)
    {
        _options = options;
        _productMatcher = productMatcher;
        _logger = logger;
    }

    public async Task<VisionResult> AnalyzeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "PaddleOCR disabled" };
        }

        try
        {
            // PaddleSharp native integration is a future dependency; stub returns empty for now
            // When integrated: var engine = new PaddleOCREngine(det, rec, cls); var result = engine.Run(imageBytes);
            string[] textBlocks = ExtractTextBlocks(imageBytes);

            if (textBlocks.Length == 0)
            {
                return new VisionResult { Success = false, ProviderUsed = ProviderName };
            }

            string combinedText = string.Join(" ", textBlocks);
            ProductMatchResult? match = await _productMatcher.MatchAsync(combinedText, ct);

            return new VisionResult
            {
                Success = match != null,
                ProductName = match?.ProductName,
                Brand = match?.Brand,
                DetectedText = textBlocks,
                Confidence = match?.Score ?? 0.0,
                ProviderUsed = ProviderName
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PaddleOCR analysis failed");
            return new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = ex.Message };
        }
    }

    public async Task<List<VisionResult>> AnalyzeMultiItemAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        VisionResult single = await AnalyzeAsync(imageBytes, ct);
        return single.Success ? new List<VisionResult> { single } : new List<VisionResult>();
    }

    /// <summary>
    /// Stub: in production, calls PaddleOCR det+rec pipeline.
    /// Returns empty array until native library is integrated.
    /// </summary>
    private string[] ExtractTextBlocks(byte[] imageBytes)
    {
        _ = imageBytes; // parameter reserved for native OCR integration
        return Array.Empty<string>();
    }
}
