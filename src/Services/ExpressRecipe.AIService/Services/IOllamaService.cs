using ExpressRecipe.Client.Shared.Models.AI;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        return ParseRecipeSuggestions(response, request.SuggestionsCount);
    }

    public async Task<IngredientSubstitutionDto> GenerateSubstitutionsAsync(IngredientSubstitutionRequest request)
    {
        var prompt = BuildSubstitutionPrompt(request);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.7);
        return ParseSubstitutions(response, request.OriginalIngredient);
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
                var aiResult = TryParseAiExtractionResponse(rawResponse);
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
        return ExtractRecipeLocally(text);
    }

    public async Task<MealPlanSuggestionDto> GenerateMealPlanAsync(MealPlanSuggestionRequest request)
    {
        var prompt = BuildMealPlanPrompt(request);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.8);
        return ParseMealPlan(response, request.DaysToPlans);
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
    // Local regex extraction (fallback when Ollama is unavailable)
    // ──────────────────────────────────────────────────────────────────────────

    private static ExtractedRecipeDto ExtractRecipeLocally(string text)
    {
        var result = new ExtractedRecipeDto();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var lines = text.Split('\n').Select(l => l.Trim()).ToArray();
        var nonEmpty = lines.Where(l => l.Length > 0).ToArray();
        if (nonEmpty.Length == 0) return result;

        // Title: first non-empty line
        result.Title = nonEmpty[0];

        // Servings
        var servMatch = Regex.Match(text, @"(?:serves|makes|yields|servings?)\s*:?\s*(\d+)", RegexOptions.IgnoreCase);
        if (servMatch.Success && int.TryParse(servMatch.Groups[1].Value, out var serv))
            result.Servings = serv;
        if (result.Servings == 0) result.Servings = 4;

        // Prep time
        var prepMatch = Regex.Match(text, @"prep(?:\s*time)?\s*:?\s*(\d+)\s*(?:min(?:utes?)?|hr?s?)", RegexOptions.IgnoreCase);
        if (prepMatch.Success && int.TryParse(prepMatch.Groups[1].Value, out var prepVal))
            result.PrepTimeMinutes = prepVal;
        else
        {
            var altPrep = Regex.Match(text, @"(\d+)\s*min(?:utes?)?\s+prep", RegexOptions.IgnoreCase);
            if (altPrep.Success && int.TryParse(altPrep.Groups[1].Value, out var ap))
                result.PrepTimeMinutes = ap;
        }

        // Cook time
        var cookMatch = Regex.Match(text, @"(?:cook|bake)(?:\s*time)?\s*:?\s*(\d+)\s*(?:min(?:utes?)?|hr?s?)", RegexOptions.IgnoreCase);
        if (cookMatch.Success && int.TryParse(cookMatch.Groups[1].Value, out var cookVal))
            result.CookTimeMinutes = cookVal;
        else
        {
            var altCook = Regex.Match(text, @"(\d+)\s*min(?:utes?)?\s+cook", RegexOptions.IgnoreCase);
            if (altCook.Success && int.TryParse(altCook.Groups[1].Value, out var ac))
                result.CookTimeMinutes = ac;
        }

        // Difficulty
        if (Regex.IsMatch(text, @"\b(?:easy|simple|beginner)\b", RegexOptions.IgnoreCase))
            result.Difficulty = "Easy";
        else if (Regex.IsMatch(text, @"\b(?:advanced|difficult|hard|complex|challenging)\b", RegexOptions.IgnoreCase))
            result.Difficulty = "Hard";
        else
            result.Difficulty = "Medium";

        // Cuisine & category
        result.Cuisine = DetectCuisine(text);
        result.Category = DetectCategory(text);

        // Dietary info
        var dietPatterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vegetarian"] = @"\bvegetarian\b",
            ["Vegan"]      = @"\bvegan\b",
            ["Gluten-Free"]= @"\bgluten[- ]free\b",
            ["Dairy-Free"] = @"\bdairy[- ]free\b",
            ["Keto"]       = @"\bketo\b",
            ["Paleo"]      = @"\bpaleo\b",
        };
        foreach (var (diet, pattern) in dietPatterns)
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                result.DietaryInfo.Add(diet);

        // Tags from cuisine + dietary (normalize to lowercase, keep hyphens for readability)
        if (!string.IsNullOrEmpty(result.Cuisine)) result.Tags.Add(result.Cuisine.ToLower());
        foreach (var d in result.DietaryInfo) result.Tags.Add(d.ToLower());

        // Parse ingredient and instruction sections
        var ingHeader    = new Regex(@"^ingredients?:?\s*$", RegexOptions.IgnoreCase);
        var stepHeader   = new Regex(@"^(?:instructions?|directions?|steps?|method):?\s*$", RegexOptions.IgnoreCase);
        var numberedLine = new Regex(@"^\d+[\.)\-]\s+(.+)$");
        var section = "none";

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (ingHeader.IsMatch(line))  { section = "ingredients"; continue; }
            if (stepHeader.IsMatch(line)) { section = "steps";       continue; }

            if (section == "ingredients")
            {
                var ing = TryParseIngredientLine(line);
                if (ing != null)
                    result.Ingredients.Add(ing);
            }
            else if (section == "steps")
            {
                var nm = numberedLine.Match(line);
                result.Instructions.Add(nm.Success ? nm.Groups[1].Value.Trim() : line);
            }
            else
            {
                var ing = TryParseIngredientLine(line);
                if (ing != null)
                    result.Ingredients.Add(ing);
                else
                {
                    var nm = numberedLine.Match(line);
                    if (nm.Success) result.Instructions.Add(nm.Groups[1].Value.Trim());
                }
            }
        }

        // Confidence proportional to how much was extracted
        double confidence = 0.1;
        if (!string.IsNullOrWhiteSpace(result.Title)) confidence += 0.1;
        if (result.Ingredients.Count > 0) confidence += 0.3;
        if (result.Instructions.Count > 0) confidence += 0.3;
        if (result.PrepTimeMinutes > 0 || result.CookTimeMinutes > 0) confidence += 0.1;
        if (result.Servings > 1) confidence += 0.1;
        result.ConfidenceScore = Math.Min(confidence, 0.85); // cap at 0.85 for regex

        return result;
    }

    private static string? DetectCuisine(string text)
    {
        var cuisines = new[] { "Italian", "Mexican", "Chinese", "Indian", "French", "American", "Japanese", "Mediterranean", "Thai", "Greek" };
        foreach (var c in cuisines)
            if (Regex.IsMatch(text, $@"\b{c}\b", RegexOptions.IgnoreCase))
                return c;
        return null;
    }

    private static string? DetectCategory(string text)
    {
        var cats = new (string Name, string[] Keywords)[]
        {
            ("Dessert",     new[] { "dessert", "cake", "cookie", "brownie", "pie", "tart", "pudding" }),
            ("Soup",        new[] { "soup", "stew", "chowder", "bisque" }),
            ("Salad",       new[] { "salad" }),
            ("Bread",       new[] { "bread", "roll", "bun", "loaf", "muffin" }),
            ("Breakfast",   new[] { "breakfast", "brunch", "pancake", "waffle" }),
            ("Beverage",    new[] { "smoothie", "juice", "cocktail", "drink", "beverage" }),
            ("Appetizer",   new[] { "appetizer", "starter", "dip" }),
            ("Snack",       new[] { "snack", "chips" }),
        };
        foreach (var (name, kws) in cats)
            if (kws.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return name;
        return null;
    }

    // Matches "2 cups flour", "1 1/2 tbsp sugar", "0.5 oz salt", etc.
    private static readonly Regex IngredientLinePattern =
        new(@"^(\d+\s+\d+/\d+|\d+[./]\d+|\d+)\s+([a-zA-Z]+\.?)\s+(.+)$", RegexOptions.Compiled);

    private static ExtractedIngredientDto? TryParseIngredientLine(string line)
    {
        var knownUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cup","cups","tsp","tbsp","oz","lb","lbs","g","kg","ml","l",
            "teaspoon","teaspoons","tablespoon","tablespoons","ounce","ounces",
            "pound","pounds","gram","grams","clove","cloves","large","medium",
            "small","pinch","dash","can","cans","package","packages",
            "stick","sticks","bunch","bunches","slice","slices","piece","pieces",
            "sprig","sprigs","head","heads","sheet","sheets"
        };

        var match = IngredientLinePattern.Match(line);
        if (!match.Success) return null;
        var unitRaw = match.Groups[2].Value.TrimEnd('.');
        if (!knownUnits.Contains(unitRaw)) return null;

        var name = match.Groups[3].Value.Trim();
        string? notes = null;
        var commaIdx = name.IndexOf(',');
        if (commaIdx > 0) { notes = name[(commaIdx + 1)..].Trim(); name = name[..commaIdx].Trim(); }

        return new ExtractedIngredientDto
        {
            Name     = name,
            Quantity = match.Groups[1].Value.Trim(),
            Unit     = unitRaw,
            Notes    = notes
        };
    }

    private ExtractedRecipeDto? TryParseAiExtractionResponse(string response)
    {
        try
        {
            // Extract the outermost JSON object by tracking brace depth, so we don't
            // accidentally grab trailing text or nested brace content outside the object.
            var jsonText = ExtractFirstJsonObject(response);
            if (jsonText == null) return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            return JsonSerializer.Deserialize<ExtractedRecipeDto>(jsonText, options);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse AI JSON response");
            return null;
        }
    }

    /// <summary>
    /// Extracts the first complete JSON object from a string by tracking brace depth.
    /// More reliable than a greedy regex when the AI includes extra text around the JSON.
    /// </summary>
    private static string? ExtractFirstJsonObject(string text)
    {
        int start = text.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        bool inString = false;
        bool escape = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (escape)           { escape = false; continue; }
            if (c == '\\')        { escape = true;  continue; }
            if (c == '"')         { inString = !inString; continue; }
            if (inString)         continue;
            if (c == '{')         depth++;
            else if (c == '}')  { depth--; if (depth == 0) return text[start..(i + 1)]; }
        }
        return null; // unterminated JSON
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
    // Response parsers (stubs – AI responses handled in TryParseAiExtractionResponse)
    // ──────────────────────────────────────────────────────────────────────────

    private static List<RecipeSuggestionDto> ParseRecipeSuggestions(string response, int count)
    {
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

    private static IngredientSubstitutionDto ParseSubstitutions(string response, string original) =>
        new() { OriginalIngredient = original, Substitutions = new List<SubstitutionOptionDto>() };

    private static MealPlanSuggestionDto ParseMealPlan(string response, int days) =>
        new() { Days = new List<DayMealPlanDto>() };

    private static AllergenDetectionResult ParseAllergenDetection(string response) =>
        new() { ConfidenceScore = 0.9 };

    private static DietaryAnalysisResult ParseDietaryAnalysis(string response) =>
        new() { HealthScore = 75 };

    private class OllamaResponse
    {
        public string? Response { get; set; }
    }
}
