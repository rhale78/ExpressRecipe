using ExpressRecipe.Client.Shared.Models.AI;

namespace ExpressRecipe.AIService.Services;

public interface IOllamaService
{
    Task<string> GenerateCompletionAsync(string prompt, string? model = null, double temperature = 0.7);
    Task<List<RecipeSuggestionDto>> GenerateRecipeSuggestionsAsync(RecipeSuggestionRequest request);
    Task<IngredientSubstitutionDto> GenerateSubstitutionsAsync(IngredientSubstitutionRequest request);
    /// <summary>
    /// Extract recipe from text using the configured AI model.
    /// <param name="mode">"quick" = fast model for live typing; "deep" = accurate model for full extraction.</param>
    /// </summary>
    Task<ExtractedRecipeDto> ExtractRecipeFromTextAsync(string text, string mode = "quick");
    Task<MealPlanSuggestionDto> GenerateMealPlanAsync(MealPlanSuggestionRequest request);
    Task<AllergenDetectionResult> DetectAllergensAsync(AllergenDetectionRequest request);
    Task<DietaryAnalysisResult> AnalyzeDietAsync(DietaryAnalysisRequest request);
    Task<AIChatResponse> ChatAsync(AIChatRequest request);
}

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _defaultModel;
    private readonly string _quickModel;
    private readonly string _deepModel;

    public OllamaService(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        // Consistent config key lookup: AI:DefaultModel takes precedence, then Ollama:DefaultModel, then llama3.2
        _defaultModel = configuration["AI:DefaultModel"]
            ?? configuration["Ollama:DefaultModel"]
            ?? "llama3.2";

        // Quick model: low-latency for live typing feedback
        _quickModel = configuration["AI:QuickModel"] ?? _defaultModel;

        // Deep model: high-accuracy for full structured extraction
        _deepModel = configuration["AI:DeepModel"] ?? _defaultModel;

        // Consistent endpoint lookup: AI:OllamaEndpoint takes precedence, then Ollama:BaseUrl
        var ollamaEndpoint = configuration["AI:OllamaEndpoint"]
            ?? configuration["Ollama:BaseUrl"]
            ?? "http://localhost:11434";

        _httpClient.BaseAddress = new Uri(ollamaEndpoint);
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<string> GenerateCompletionAsync(string prompt, string? model = null, double temperature = 0.7)
    {
        try
        {
            var requestBody = new
            {
                model = model ?? _defaultModel,
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = temperature,
                    num_predict = 2000
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Ollama model '{Model}'", model ?? _defaultModel);
            throw;
        }
    }

    public async Task<List<RecipeSuggestionDto>> GenerateRecipeSuggestionsAsync(RecipeSuggestionRequest request)
    {
        var prompt = BuildRecipeSuggestionPrompt(request);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.8);
        return ParseRecipeSuggestions(response, request.SuggestionsCount, _logger);
    }

    public async Task<IngredientSubstitutionDto> GenerateSubstitutionsAsync(IngredientSubstitutionRequest request)
    {
        var prompt = BuildSubstitutionPrompt(request);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.7);
        return ParseSubstitutions(response, request.OriginalIngredient, _logger);
    }

    /// <summary>
    /// Tries Ollama AI extraction first; falls back to local regex parsing when Ollama is
    /// unavailable (e.g. model not installed, service down, or timeout).
    /// </summary>
    /// <param name="mode">"quick" = fast model (low-latency, live typing); "deep" = accurate model (full extraction).</param>
    public async Task<ExtractedRecipeDto> ExtractRecipeFromTextAsync(string text, string mode = "quick")
    {
        var model  = mode == "deep" ? _deepModel  : _quickModel;
        var temp   = mode == "deep" ? 0.3         : 0.1;
        var prompt = mode == "deep"
            ? BuildDeepRecipeExtractionPrompt(text)
            : BuildQuickRecipeExtractionPrompt(text);

        try
        {
            var rawResponse = await GenerateCompletionAsync(prompt, model, temp);

            if (!string.IsNullOrWhiteSpace(rawResponse))
            {
                var aiResult = RecipeTextParser.TryParseAiExtractionResponse(rawResponse);
                if (aiResult != null && !string.IsNullOrWhiteSpace(aiResult.Title))
                {
                    aiResult.ConfidenceScore = Math.Max(aiResult.ConfidenceScore, mode == "deep" ? 0.85 : 0.70);
                    _logger.LogInformation("AI extraction succeeded (model: {Model}, mode: {Mode})", model, mode);
                    return aiResult;
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Ollama model '{Model}' not found (404). Run 'ollama pull {Model}' to install it. Falling back to regex.",
                model, model);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama HTTP error (mode: {Mode}). Falling back to regex extraction.", mode);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Ollama request timed out (mode: {Mode}). Falling back to regex extraction.", mode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI extraction failed (mode: {Mode}). Falling back to regex extraction.", mode);
        }

        _logger.LogInformation("Using local regex extraction as fallback (mode: {Mode})", mode);
        return RecipeTextParser.ExtractRecipeLocally(text);
    }

    public async Task<MealPlanSuggestionDto> GenerateMealPlanAsync(MealPlanSuggestionRequest request)
    {
        var prompt = BuildMealPlanPrompt(request);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.8);
        return ParseMealPlan(response, request.DaysToPlans, _logger);
    }

    public async Task<AllergenDetectionResult> DetectAllergensAsync(AllergenDetectionRequest request)
    {
        var prompt = BuildAllergenDetectionPrompt(request);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.3);
        return ParseAllergenDetection(response);
    }

    public async Task<DietaryAnalysisResult> AnalyzeDietAsync(DietaryAnalysisRequest request)
    {
        var prompt = BuildDietaryAnalysisPrompt(request);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.7);
        return ParseDietaryAnalysis(response);
    }

    public async Task<AIChatResponse> ChatAsync(AIChatRequest request)
    {
        var prompt = BuildChatPrompt(request);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.7);
        return new AIChatResponse { Message = response.Trim() };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Prompt builders
    // ──────────────────────────────────────────────────────────────────────────

    private string BuildRecipeSuggestionPrompt(RecipeSuggestionRequest request)
    {
        return $@"You are an expert chef. Generate {request.SuggestionsCount} recipe suggestions.

Available Ingredients: {string.Join(", ", request.AvailableIngredients)}
User Allergens (MUST AVOID): {string.Join(", ", request.UserAllergens)}
Dietary Preferences: {string.Join(", ", request.DietaryPreferences)}
{(request.CuisinePreference != null ? $"Cuisine: {request.CuisinePreference}" : "")}
{(request.MaxCookTimeMinutes.HasValue ? $"Max Cook Time: {request.MaxCookTimeMinutes} min" : "")}

Return JSON array:
[{{""recipeName"":"""",""description"":"""",""prepTimeMinutes"":0,""cookTimeMinutes"":0,""difficulty"":""Easy"",""matchScore"":0,""reasoning"":""""}}]";
    }

    private string BuildSubstitutionPrompt(IngredientSubstitutionRequest request)
    {
        return $@"Suggest substitutions for: {request.OriginalIngredient}
Context: {request.RecipeContext}
Avoid allergens: {string.Join(", ", request.UserAllergens)}

Return JSON array of substitution options.";
    }

    /// <summary>
    /// Quick prompt: minimal fields, fast response for live-typing feedback.
    /// </summary>
    private static string BuildQuickRecipeExtractionPrompt(string text)
    {
        return $@"Extract recipe data. Return ONLY valid JSON, no extra text.

TEXT:
{text}

JSON:
{{
  ""title"": """",
  ""servings"": 4,
  ""prepTimeMinutes"": 0,
  ""cookTimeMinutes"": 0,
  ""difficulty"": ""Medium"",
  ""ingredients"": [{{""name"":"""",""quantity"":"""",""unit"":""""}}],
  ""instructions"": [""""],
  ""confidenceScore"": 0.9
}}";
    }

    /// <summary>
    /// Deep prompt: full extraction including description, allergens, dietary info, tags, cuisine, category.
    /// </summary>
    private static string BuildDeepRecipeExtractionPrompt(string text)
    {
        return $@"You are a recipe extraction expert. Extract ALL recipe information from the text below.
Return ONLY valid JSON — no explanations, no markdown fences, just the JSON object.

TEXT:
{text}

JSON structure (fill every field accurately):
{{
  ""title"": """",
  ""description"": """",
  ""prepTimeMinutes"": 0,
  ""cookTimeMinutes"": 0,
  ""servings"": 4,
  ""difficulty"": ""Easy|Medium|Hard"",
  ""cuisine"": """",
  ""category"": """",
  ""ingredients"": [{{""name"": """", ""quantity"": """", ""unit"": """", ""notes"": """"}}],
  ""instructions"": [""""],
  ""detectedAllergens"": [],
  ""dietaryInfo"": [],
  ""tags"": [],
  ""confidenceScore"": 0.95
}}";
    }

    private string BuildMealPlanPrompt(MealPlanSuggestionRequest request)
    {
        return $@"Create a {request.DaysToPlans}-day meal plan.
Available: {string.Join(", ", request.AvailableIngredients)}
Avoid: {string.Join(", ", request.UserAllergens)}
Dietary: {string.Join(", ", request.DietaryPreferences)}

Return JSON with days array, shopping list, and nutrition summary.";
    }

    private string BuildAllergenDetectionPrompt(AllergenDetectionRequest request)
    {
        return $@"Detect allergens in: {string.Join(", ", request.Ingredients)}

Return JSON with detectedAllergens array and confidenceScore.";
    }

    private string BuildDietaryAnalysisPrompt(DietaryAnalysisRequest request)
    {
        var ingredients = request.Ingredients != null ? string.Join(", ", request.Ingredients) : "";
        return $@"Analyze dietary suitability.
Ingredients: {ingredients}
Goals: {string.Join(", ", request.UserHealthGoals)}
Restrictions: {string.Join(", ", request.DietaryRestrictions)}

Return JSON with isSuitableForUser, healthBenefits, healthConcerns, and healthScore.";
    }

    private string BuildChatPrompt(AIChatRequest request)
    {
        var context = request.Context switch
        {
            "recipe"    => "You are a helpful cooking assistant.",
            "nutrition" => "You are a helpful nutrition expert.",
            "shopping"  => "You are a helpful shopping assistant.",
            _           => "You are a helpful assistant for ExpressRecipe."
        };

        var history = request.ConversationHistory != null
            ? string.Join("\n", request.ConversationHistory.Select(m => $"{m.Role}: {m.Content}"))
            : "";

        return $@"{context}
{(history.Length > 0 ? $"\nHistory:\n{history}\n" : "")}
User: {request.Message}";
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Response parsers
    // ──────────────────────────────────────────────────────────────────────────

    private static List<RecipeSuggestionDto> ParseRecipeSuggestions(string response, int count, ILogger? logger = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(response))
            {
                // Strip markdown fences if present
                var json = StripMarkdownFences(response);
                // Try to find JSON array
                var start = json.IndexOf('[');
                var end   = json.LastIndexOf(']');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];

                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var items = System.Text.Json.JsonSerializer.Deserialize<List<RecipeSuggestionJsonItem>>(json, options);
                if (items != null && items.Count > 0)
                {
                    return items.Take(count).Select(i => new RecipeSuggestionDto
                    {
                        RecipeName       = i.RecipeName     ?? i.Name             ?? "Unnamed Recipe",
                        Description      = i.Description    ?? string.Empty,
                        PrepTimeMinutes  = i.PrepTimeMinutes,
                        CookTimeMinutes  = i.CookTimeMinutes,
                        Difficulty       = i.Difficulty     ?? "Medium",
                        MatchScore       = i.MatchScore,
                        Reasoning        = i.Reasoning      ?? string.Empty,
                        Ingredients      = i.Ingredients    ?? new List<string>(),
                        Instructions     = i.Instructions   ?? new List<string>()
                    }).ToList();
                }
            }
        }
        catch (Exception ex) { logger?.LogWarning(ex, "Failed to parse recipe suggestions from AI response"); }

        // Fallback: return placeholder items
        return Enumerable.Range(1, Math.Min(count, 3)).Select(i => new RecipeSuggestionDto
        {
            RecipeName = $"Suggested Recipe {i}",
            Description = "AI-generated suggestion",
            PrepTimeMinutes = 15,
            CookTimeMinutes = 30,
            Difficulty = "Medium",
            MatchScore = 75
        }).ToList();
    }

    private static IngredientSubstitutionDto ParseSubstitutions(string response, string original, ILogger? logger = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(response))
            {
                var json = StripMarkdownFences(response);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Try array of substitution options first
                var arrayStart = json.IndexOf('[');
                var arrayEnd   = json.LastIndexOf(']');
                if (arrayStart >= 0 && arrayEnd > arrayStart)
                {
                    var arrayJson = json[arrayStart..(arrayEnd + 1)];
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<SubstitutionOptionJsonItem>>(arrayJson, options);
                    if (items != null && items.Count > 0)
                    {
                        return new IngredientSubstitutionDto
                        {
                            OriginalIngredient = original,
                            Substitutions = items.Select(s => new SubstitutionOptionDto
                            {
                                Ingredient       = s.Ingredient     ?? s.Name         ?? string.Empty,
                                Ratio            = s.Ratio          ?? "1:1",
                                Explanation      = s.Explanation    ?? s.Notes        ?? string.Empty,
                                IsHealthier      = s.IsHealthier,
                                IsAvailable      = s.IsAvailable,
                                SuitabilityScore = s.SuitabilityScore > 0 ? s.SuitabilityScore : 7
                            }).ToList()
                        };
                    }
                }

                // Try object with substitutions property
                var objStart = json.IndexOf('{');
                var objEnd   = json.LastIndexOf('}');
                if (objStart >= 0 && objEnd > objStart)
                {
                    var objJson = json[objStart..(objEnd + 1)];
                    var wrapper = System.Text.Json.JsonSerializer.Deserialize<SubstitutionWrapperJsonItem>(objJson, options);
                    if (wrapper?.Substitutions != null && wrapper.Substitutions.Count > 0)
                    {
                        return new IngredientSubstitutionDto
                        {
                            OriginalIngredient = original,
                            Substitutions = wrapper.Substitutions.Select(s => new SubstitutionOptionDto
                            {
                                Ingredient       = s.Ingredient     ?? s.Name         ?? string.Empty,
                                Ratio            = s.Ratio          ?? "1:1",
                                Explanation      = s.Explanation    ?? s.Notes        ?? string.Empty,
                                IsHealthier      = s.IsHealthier,
                                IsAvailable      = s.IsAvailable,
                                SuitabilityScore = s.SuitabilityScore > 0 ? s.SuitabilityScore : 7
                            }).ToList()
                        };
                    }
                }
            }
        }
        catch (Exception ex) { logger?.LogWarning(ex, "Failed to parse substitutions from AI response for ingredient '{Ingredient}'", original); }

        return new() { OriginalIngredient = original, Substitutions = new List<SubstitutionOptionDto>() };
    }

    private static MealPlanSuggestionDto ParseMealPlan(string response, int days, ILogger? logger = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(response))
            {
                var json = StripMarkdownFences(response);
                var start = json.IndexOf('{');
                var end   = json.LastIndexOf('}');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];

                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed  = System.Text.Json.JsonSerializer.Deserialize<MealPlanJsonItem>(json, options);
                if (parsed?.Days != null && parsed.Days.Count > 0)
                {
                    return new MealPlanSuggestionDto
                    {
                        Days = parsed.Days.Select((d, i) => new DayMealPlanDto
                        {
                            Date      = DateTime.UtcNow.Date.AddDays(i),
                            Breakfast = d.Breakfast,
                            Lunch     = d.Lunch,
                            Dinner    = d.Dinner,
                            Snacks    = d.Snacks
                        }).ToList(),
                        Reasoning     = parsed.Reasoning ?? string.Empty,
                        EstimatedCost = parsed.EstimatedCost
                    };
                }
            }
        }
        catch (Exception ex) { logger?.LogWarning(ex, "Failed to parse meal plan from AI response"); }

        return new() { Days = new List<DayMealPlanDto>() };
    }

    private static AllergenDetectionResult ParseAllergenDetection(string response) =>
        new() { ConfidenceScore = 0.9 };

    private static DietaryAnalysisResult ParseDietaryAnalysis(string response) =>
        new() { HealthScore = 75 };

    private static string StripMarkdownFences(string text)
    {
        // Remove ```json ... ``` or ``` ... ``` wrappers
        var t = text.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = t.IndexOf('\n');
            if (firstNewline >= 0)
                t = t[(firstNewline + 1)..];
        }
        if (t.EndsWith("```", StringComparison.Ordinal))
            t = t[..^3];
        return t.Trim();
    }

    // ── private JSON mapping helpers ──────────────────────────────────────────

    private sealed class RecipeSuggestionJsonItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("recipeName")]
        public string? RecipeName { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int PrepTimeMinutes { get; set; }
        public int CookTimeMinutes { get; set; }
        public string? Difficulty { get; set; }
        public double MatchScore { get; set; }
        public string? Reasoning { get; set; }
        public List<string>? Ingredients { get; set; }
        public List<string>? Instructions { get; set; }
    }

    private sealed class SubstitutionOptionJsonItem
    {
        public string? Ingredient { get; set; }
        public string? Name { get; set; }
        public string? Ratio { get; set; }
        public string? Explanation { get; set; }
        public string? Notes { get; set; }
        public bool IsHealthier { get; set; }
        public bool IsAvailable { get; set; }
        public int SuitabilityScore { get; set; }
    }

    private sealed class SubstitutionWrapperJsonItem
    {
        public List<SubstitutionOptionJsonItem>? Substitutions { get; set; }
    }

    private sealed class MealPlanDayJsonItem
    {
        public string? Breakfast { get; set; }
        public string? Lunch { get; set; }
        public string? Dinner { get; set; }
        public List<string>? Snacks { get; set; }
    }

    private sealed class MealPlanJsonItem
    {
        public List<MealPlanDayJsonItem>? Days { get; set; }
        public string? Reasoning { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    private class OllamaResponse
    {
        public string? Response { get; set; }
    }
}
