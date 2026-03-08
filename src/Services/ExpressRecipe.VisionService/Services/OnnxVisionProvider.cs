using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExpressRecipe.VisionService.Services;

public class OnnxVisionOptions
{
    public bool Enabled { get; set; } = true;
    public string ModelPath { get; set; } = string.Empty;
    public float ConfidenceThreshold { get; set; } = 0.55f;
}

/// <summary>
/// ONNX-based vision provider using YOLOv8n model for product detection.
/// If model file is not found, returns empty result without throwing.
/// </summary>
public class OnnxVisionProvider : IVisionProvider, IDisposable
{
    private const int ModelInputSize = 640;
    private readonly OnnxVisionOptions _options;
    private readonly ILogger<OnnxVisionProvider> _logger;
    private InferenceSession? _session;
    private string[] _labels = Array.Empty<string>();

    public string ProviderName => "ONNX";
    public bool IsEnabled => _options.Enabled && _session != null;

    public OnnxVisionProvider(OnnxVisionOptions options, ILogger<OnnxVisionProvider> logger)
    {
        _options = options;
        _logger = logger;
        TryLoadModel();
    }

    private void TryLoadModel()
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ModelPath) || !File.Exists(_options.ModelPath))
        {
            _logger.LogWarning("ONNX model not found at path: {ModelPath}. Provider disabled.", _options.ModelPath);
            return;
        }

        try
        {
            _session = new InferenceSession(_options.ModelPath);
            TryLoadLabels();
            _logger.LogInformation("ONNX model loaded from {ModelPath}", _options.ModelPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ONNX model from {ModelPath}", _options.ModelPath);
        }
    }

    private void TryLoadLabels()
    {
        string labelsPath = Path.ChangeExtension(_options.ModelPath, ".labels.json");
        if (!File.Exists(labelsPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(labelsPath);
            string[]? labels = System.Text.Json.JsonSerializer.Deserialize<string[]>(json);
            if (labels != null)
            {
                _labels = labels;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load label map from {LabelsPath}", labelsPath);
        }
    }

    public Task<VisionResult> AnalyzeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        if (_session == null)
        {
            return Task.FromResult(new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = "Model not loaded" });
        }

        try
        {
            ct.ThrowIfCancellationRequested();
            List<BoundingBox> boxes = RunInference(imageBytes);

            if (boxes.Count == 0)
            {
                return Task.FromResult(new VisionResult { Success = false, ProviderUsed = ProviderName });
            }

            BoundingBox best = boxes[0];
            return Task.FromResult(new VisionResult
            {
                Success = true,
                Labels = boxes.Select(b => b.Label).Distinct().ToArray(),
                Confidence = best.Confidence,
                ProviderUsed = ProviderName,
                BoundingBox = best
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX inference failed");
            return Task.FromResult(new VisionResult { Success = false, ProviderUsed = ProviderName, ErrorMessage = ex.Message });
        }
    }

    public Task<List<VisionResult>> AnalyzeMultiItemAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        if (_session == null)
        {
            return Task.FromResult(new List<VisionResult>());
        }

        try
        {
            ct.ThrowIfCancellationRequested();
            List<BoundingBox> boxes = RunInference(imageBytes);
            List<VisionResult> results = boxes
                .Select(b => new VisionResult
                {
                    Success = true,
                    Labels = new[] { b.Label },
                    Confidence = b.Confidence,
                    ProviderUsed = ProviderName,
                    BoundingBox = b
                })
                .ToList();

            return Task.FromResult(results);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX multi-item inference failed");
            return Task.FromResult(new List<VisionResult>());
        }
    }

    private List<BoundingBox> RunInference(byte[] imageBytes)
    {
        float[] inputData = PreprocessImage(imageBytes);
        DenseTensor<float> tensor = new DenseTensor<float>(inputData, new[] { 1, 3, ModelInputSize, ModelInputSize });
        List<NamedOnnxValue> inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", tensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session!.Run(inputs);
        DisposableNamedOnnxValue? outputNode = outputs.FirstOrDefault();
        if (outputNode == null)
        {
            return new List<BoundingBox>();
        }

        Tensor<float> outputTensor = outputNode.AsTensor<float>();
        return ParseYoloV8Output(outputTensor);
    }

    private float[] PreprocessImage(byte[] imageBytes)
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(imageBytes);
        image.Mutate(x => x.Resize(ModelInputSize, ModelInputSize));

        float[] data = new float[3 * ModelInputSize * ModelInputSize];
        int channelSize = ModelInputSize * ModelInputSize;

        for (int y = 0; y < ModelInputSize; y++)
        {
            for (int x = 0; x < ModelInputSize; x++)
            {
                Rgb24 pixel = image[x, y];
                int idx = y * ModelInputSize + x;
                data[idx] = pixel.R / 255.0f;
                data[channelSize + idx] = pixel.G / 255.0f;
                data[2 * channelSize + idx] = pixel.B / 255.0f;
            }
        }

        return data;
    }

    private List<BoundingBox> ParseYoloV8Output(Tensor<float> output)
    {
        // YOLOv8 output shape: [1, 84, 8400] where 84 = 4 (box) + 80 (class scores)
        int numDetections = output.Dimensions[2];
        int numClasses = output.Dimensions[1] - 4;
        List<BoundingBox> boxes = new List<BoundingBox>();

        for (int i = 0; i < numDetections; i++)
        {
            float maxClassScore = 0f;
            int bestClass = 0;

            for (int c = 0; c < numClasses; c++)
            {
                float score = output[0, 4 + c, i];
                if (score > maxClassScore)
                {
                    maxClassScore = score;
                    bestClass = c;
                }
            }

            if (maxClassScore < _options.ConfidenceThreshold)
            {
                continue;
            }

            float cx = output[0, 0, i];
            float cy = output[0, 1, i];
            float w = output[0, 2, i];
            float h = output[0, 3, i];

            string label = (bestClass < _labels.Length) ? _labels[bestClass] : $"class_{bestClass}";

            boxes.Add(new BoundingBox
            {
                X = cx - w / 2,
                Y = cy - h / 2,
                Width = w,
                Height = h,
                Label = label,
                Confidence = maxClassScore
            });
        }

        return boxes.OrderByDescending(b => b.Confidence).ToList();
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
