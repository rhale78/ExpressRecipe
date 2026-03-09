using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ExpressRecipe.VisionService.Services;

public class VisionServiceOptions
{
    public int ImageMaxDimensionPx { get; set; } = 1280;
}

/// <summary>
/// Vision orchestrator: runs ONNX + PaddleOCR in parallel (Task.WhenAll).
/// Falls back to Ollama then Azure only if parallel result confidence &lt; MinConfidence.
/// Always returns a result — never throws.
/// </summary>
public class VisionService : IVisionService
{
    private readonly OnnxVisionProvider _onnx;
    private readonly PaddleOcrProvider _paddle;
    private readonly OllamaVisionProvider _ollama;
    private readonly AzureVisionProvider _azure;
    private readonly VisionServiceOptions _options;
    private readonly ILogger<VisionService> _logger;

    public VisionService(
        OnnxVisionProvider onnx,
        PaddleOcrProvider paddle,
        OllamaVisionProvider ollama,
        AzureVisionProvider azure,
        VisionServiceOptions options,
        ILogger<VisionService> logger)
    {
        _onnx = onnx;
        _paddle = paddle;
        _ollama = ollama;
        _azure = azure;
        _options = options;
        _logger = logger;
    }

    public async Task<VisionResult> AnalyzeAsync(byte[] imageBytes, VisionOptions options, CancellationToken ct = default)
    {
        byte[] resized = ResizeImage(imageBytes);

        // Run ONNX and PaddleOCR in parallel
        Task<VisionResult> onnxTask = (options.AllowOnnx && _onnx.IsEnabled)
            ? _onnx.AnalyzeAsync(resized, ct)
            : Task.FromResult(new VisionResult { ProviderUsed = "ONNX" });

        Task<VisionResult> paddleTask = (options.AllowPaddleOcr && _paddle.IsEnabled)
            ? _paddle.AnalyzeAsync(resized, ct)
            : Task.FromResult(new VisionResult { ProviderUsed = "PaddleOCR" });

        await Task.WhenAll(onnxTask, paddleTask);

        VisionResult onnxResult = await onnxTask;
        VisionResult paddleResult = await paddleTask;

        VisionResult best = SelectBestResult(onnxResult, paddleResult);

        if (best.Success && best.Confidence >= options.MinConfidence)
        {
            _logger.LogDebug("Vision result from parallel providers: {Provider} confidence {Confidence}", best.ProviderUsed, best.Confidence);
            return best;
        }

        // Fallback to Ollama
        if (options.AllowOllamaVision && _ollama.IsEnabled)
        {
            _logger.LogDebug("Parallel providers below MinConfidence ({Min}), trying Ollama", options.MinConfidence);
            VisionResult ollamaResult = await _ollama.AnalyzeAsync(resized, ct);
            if (ollamaResult.Success && ollamaResult.Confidence >= options.MinConfidence)
            {
                return ollamaResult;
            }
        }

        // Fallback to Azure
        if (options.AllowAzureVision && _azure.IsEnabled)
        {
            _logger.LogDebug("Trying Azure Vision as final fallback");
            VisionResult azureResult = await _azure.AnalyzeAsync(resized, ct);
            if (azureResult.Success)
            {
                return azureResult;
            }
        }

        // Return best even if below threshold
        return best.Success ? best : new VisionResult
        {
            Success = false,
            ProviderUsed = "none",
            ErrorMessage = "All providers failed or returned low confidence"
        };
    }

    public async Task<List<VisionResult>> AnalyzeMultiItemAsync(byte[] imageBytes, VisionOptions options, CancellationToken ct = default)
    {
        byte[] resized = ResizeImage(imageBytes);

        Task<List<VisionResult>> onnxTask = (options.AllowOnnx && _onnx.IsEnabled)
            ? _onnx.AnalyzeMultiItemAsync(resized, ct)
            : Task.FromResult(new List<VisionResult>());

        Task<List<VisionResult>> paddleTask = (options.AllowPaddleOcr && _paddle.IsEnabled)
            ? _paddle.AnalyzeMultiItemAsync(resized, ct)
            : Task.FromResult(new List<VisionResult>());

        await Task.WhenAll(onnxTask, paddleTask);

        List<VisionResult> combined = new List<VisionResult>();
        combined.AddRange(await onnxTask);
        combined.AddRange(await paddleTask);
        return combined;
    }

    public async Task<VisionResult> ExtractTextAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        byte[] resized = ResizeImage(imageBytes);
        return await _paddle.AnalyzeAsync(resized, ct);
    }

    public Task<VisionHealthStatus> GetHealthAsync(CancellationToken ct = default)
    {
        VisionHealthStatus status = new VisionHealthStatus
        {
            OnnxAvailable = _onnx.IsEnabled,
            PaddleOcrAvailable = _paddle.IsEnabled,
            OllamaVisionAvailable = _ollama.IsEnabled,
            AzureVisionAvailable = _azure.IsEnabled,
            OnnxModelPath = string.Empty
        };

        return Task.FromResult(status);
    }

    private static VisionResult SelectBestResult(VisionResult a, VisionResult b)
    {
        if (!a.Success)
        {
            return b;
        }

        if (!b.Success)
        {
            return a;
        }

        return a.Confidence >= b.Confidence ? a : b;
    }

    private byte[] ResizeImage(byte[] imageBytes)
    {
        try
        {
            using Image image = Image.Load(imageBytes);
            int maxDim = _options.ImageMaxDimensionPx;

            if (image.Width <= maxDim && image.Height <= maxDim)
            {
                return imageBytes;
            }

            double ratio = Math.Min((double)maxDim / image.Width, (double)maxDim / image.Height);
            int newWidth = (int)(image.Width * ratio);
            int newHeight = (int)(image.Height * ratio);

            image.Mutate(x => x.Resize(newWidth, newHeight));

            using System.IO.MemoryStream ms = new System.IO.MemoryStream();
            image.SaveAsJpeg(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resize image, using original");
            return imageBytes;
        }
    }
}
