namespace ExpressRecipe.VisionService.Services;

public class VisionResult
{
    public bool Success { get; init; }
    public string? ProductName { get; init; }
    public string? Brand { get; init; }
    public string[] DetectedText { get; init; } = Array.Empty<string>();
    public string[] Labels { get; init; } = Array.Empty<string>();
    public double Confidence { get; init; }
    public string ProviderUsed { get; init; } = string.Empty;
    public BoundingBox? BoundingBox { get; init; }
    public string? ErrorMessage { get; init; }
}

public class BoundingBox
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public string Label { get; init; } = string.Empty;
    public double Confidence { get; init; }
}

public class VisionOptions
{
    public bool AllowOnnx { get; init; } = true;
    public bool AllowPaddleOcr { get; init; } = true;
    public bool AllowOllamaVision { get; init; } = true;
    public bool AllowAzureVision { get; init; } = false;
    public double MinConfidence { get; init; } = 0.55;
}

public class VisionHealthStatus
{
    public bool OnnxAvailable { get; init; }
    public bool PaddleOcrAvailable { get; init; }
    public bool OllamaVisionAvailable { get; init; }
    public bool AzureVisionAvailable { get; init; }
    public string OnnxModelPath { get; init; } = string.Empty;
}

public class VisionAnalyzeRequest
{
    public string? Base64Image { get; set; }
    public VisionOptions? Options { get; set; }
}

public class OcrRequest
{
    public string? Base64Image { get; set; }
}
