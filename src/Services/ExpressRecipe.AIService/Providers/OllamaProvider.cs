using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.AIService.Providers;

public sealed class OllamaProvider : IAIProvider
{
    public string ProviderName => "Ollama";

    private readonly IHttpClientFactory _http;
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly ILogger<OllamaProvider> _logger;

    public OllamaProvider(IHttpClientFactory http, IConfiguration config,
        ILogger<OllamaProvider> logger)
    {
        _http         = http;
        _baseUrl      = config["AI:Ollama:BaseUrl"]
                        ?? config["AI:OllamaEndpoint"]
                        ?? config["Ollama:BaseUrl"]
                        ?? "http://localhost:11434";
        _defaultModel = config["AI:Ollama:DefaultModel"]
                        ?? config["AI:DefaultModel"]
                        ?? "llama3.2";
        _logger       = logger;
    }

    public async Task<AITextResult> GenerateAsync(string prompt,
        AIRequestOptions? options = null, CancellationToken ct = default)
    {
        options ??= new AIRequestOptions();
        HttpClient client = _http.CreateClient("Ollama");
        using CancellationTokenSource cts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(options.Timeout);

        try
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                $"{_baseUrl}/api/generate",
                new
                {
                    model  = _defaultModel,
                    prompt = options.SystemPrompt is null
                        ? prompt
                        : $"{options.SystemPrompt}\n\n{prompt}",
                    stream = false,
                    options = new
                    {
                        temperature = (double)options.Temperature,
                        num_predict = options.MaxTokens
                    }
                }, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return new AITextResult
                {
                    Success      = false,
                    ErrorMessage = $"Ollama HTTP {response.StatusCode}",
                    ProviderName = ProviderName
                };
            }

            OllamaGenerateResponse? body =
                await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(ct);
            return new AITextResult
            {
                Success      = true,
                Text         = body?.Response ?? string.Empty,
                ProviderName = ProviderName,
                TokensUsed   = body?.EvalCount ?? 0
            };
        }
        catch (OperationCanceledException)
        {
            return new AITextResult
            {
                Success      = false,
                ErrorMessage = "Timeout",
                ProviderName = ProviderName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama GenerateAsync failed");
            return new AITextResult
            {
                Success      = false,
                ErrorMessage = ex.Message,
                ProviderName = ProviderName
            };
        }
    }

    public async Task<AIApprovalResult> ScoreForApprovalAsync(string content,
        string entityType, AIRequestOptions? options = null, CancellationToken ct = default)
    {
        string prompt = $$"""
            You are a content moderator for a food and recipe app.
            Review the following {{entityType}} submission and rate its quality and appropriateness.
            Respond ONLY with valid JSON in this exact format:
            {"score": 0.0-1.0, "reasoning": "brief explanation", "kick_to_human": true/false}

            Score guide: 1.0=excellent/approve, 0.5=borderline, 0.0=reject.
            Set kick_to_human=true if you are uncertain (score between 0.4-0.6).

            Content to review:
            {{content}}
            """;

        AITextResult result = await GenerateAsync(prompt,
            new AIRequestOptions { MaxTokens = 200, Temperature = 0.1m }, ct);

        if (!result.Success)
        {
            return new AIApprovalResult
            {
                Success      = false,
                KickToHuman  = true,
                ErrorMessage = result.ErrorMessage
            };
        }

        return ParseApprovalJson(result.Text);
    }

    public async Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default)
    {
        string classListStr = string.Join(", ", possibleClasses);
        string fullPrompt   = $"{prompt}\n\nRespond with only one of these options: {classListStr}";
        AITextResult result = await GenerateAsync(fullPrompt, options, ct);
        if (!result.Success)
        {
            return new AIClassifyResult { Success = false, ErrorMessage = result.ErrorMessage };
        }

        string chosen = possibleClasses
            .FirstOrDefault(c => result.Text.Contains(c, StringComparison.OrdinalIgnoreCase))
            ?? possibleClasses[0];
        return new AIClassifyResult { Success = true, ChosenClass = chosen, Confidence = 0.8m };
    }

    private static AIApprovalResult ParseApprovalJson(string json)
    {
        try
        {
            string cleaned = json.Replace("```json", "").Replace("```", "").Trim();
            using JsonDocument doc = JsonDocument.Parse(cleaned);
            JsonElement root = doc.RootElement;
            decimal score   = root.GetProperty("score").GetDecimal();
            string reason   = root.GetProperty("reasoning").GetString() ?? string.Empty;
            bool kick       = root.GetProperty("kick_to_human").GetBoolean();
            return new AIApprovalResult
            {
                Success    = true,
                Score      = score,
                Reasoning  = reason,
                KickToHuman = kick
            };
        }
        catch
        {
            return new AIApprovalResult
            {
                Success      = false,
                KickToHuman  = true,
                ErrorMessage = "Could not parse AI response"
            };
        }
    }

    private sealed record OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; init; } = string.Empty;
        [JsonPropertyName("eval_count")]
        public int EvalCount { get; init; }
    }
}
