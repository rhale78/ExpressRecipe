using ExpressRecipe.Client.Shared.Models.AI;

namespace ExpressRecipe.AIService.Services;

public interface IOllamaService
{
    Task<string> GenerateCompletionAsync(string prompt, string? model = null, double temperature = 0.7);
    Task<List<RecipeSuggestionDto>> GenerateRecipeSuggestionsAsync(RecipeSuggestionRequest request);
    Task<IngredientSubstitutionDto> GenerateSubstitutionsAsync(IngredientSubstitutionRequest request);
    Task<ExtractedRecipeDto> ExtractRecipeFromTextAsync(string text);
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

    public OllamaService(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _defaultModel = configuration["AI:DefaultModel"] ?? "llama2";

        var ollamaEndpoint = configuration["AI:OllamaEndpoint"] ?? "http://localhost:11434";
        _httpClient.BaseAddress = new Uri(ollamaEndpoint);
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
            _logger.LogError(ex, "Error generating completion from Ollama");
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

    public async Task<ExtractedRecipeDto> ExtractRecipeFromTextAsync(string text)
    {
        var prompt = BuildRecipeExtractionPrompt(text);
        var response = await GenerateCompletionAsync(prompt, temperature: 0.3);

        return ParseExtractedRecipe(response);
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

        return new AIChatResponse
        {
            Message = response.Trim()
        };
    }

    // Prompt Builders
    private string BuildRecipeSuggestionPrompt(RecipeSuggestionRequest request)
    {
        var prompt = $@"You are an expert chef and nutritionist. Generate {request.SuggestionsCount} recipe suggestions based on the following criteria:

Available Ingredients: {string.Join(", ", request.AvailableIngredients)}
User Allergens (MUST AVOID): {string.Join(", ", request.UserAllergens)}
User Dislikes: {string.Join(", ", request.UserDislikes)}
Dietary Preferences: {string.Join(", ", request.DietaryPreferences)}
{(request.CuisinePreference != null ? $"Cuisine: {request.CuisinePreference}" : "")}
{(request.MaxCookTimeMinutes.HasValue ? $"Max Cook Time: {request.MaxCookTimeMinutes} minutes" : "")}
{(request.Difficulty != null ? $"Difficulty: {request.Difficulty}" : "")}

For each recipe, provide:
1. Recipe name
2. Brief description
3. Complete ingredient list with quantities
4. Which ingredients from the available list are used
5. Which ingredients are missing
6. Prep time and cook time
7. Difficulty level
8. Step-by-step instructions
9. A match score (0-100) indicating how well it uses available ingredients
10. Brief reasoning for the suggestion

CRITICAL: Do NOT suggest any recipes containing the allergens listed above.

Format the response as JSON with the following structure:
[
  {{
    ""recipeName"": ""name"",
    ""description"": ""description"",
    ""ingredients"": [""ingredient 1"", ""ingredient 2""],
    ""missingIngredients"": [""ingredient 1""],
    ""prepTimeMinutes"": 15,
    ""cookTimeMinutes"": 30,
    ""difficulty"": ""Easy"",
    ""instructions"": [""step 1"", ""step 2""],
    ""matchScore"": 85,
    ""reasoning"": ""explanation""
  }}
]";

        return prompt;
    }

    private string BuildSubstitutionPrompt(IngredientSubstitutionRequest request)
    {
        return $@"You are a culinary expert. Suggest ingredient substitutions for:

Original Ingredient: {request.OriginalIngredient}
Recipe Context: {request.RecipeContext}
User Allergens (MUST AVOID): {string.Join(", ", request.UserAllergens)}
Available Ingredients: {string.Join(", ", request.AvailableIngredients)}
{(request.PreferHealthier ? "Prefer healthier alternatives" : "")}

Provide 3-5 substitution options with:
1. Substitute ingredient
2. Substitution ratio (e.g., ""1:1"", ""1:2"")
3. Explanation of how it affects the recipe
4. Whether it's healthier
5. Whether it's available
6. Suitability score (1-10)

CRITICAL: Do NOT suggest substitutes containing the allergens listed above.

Format as JSON array of substitution objects.";
    }

    private string BuildRecipeExtractionPrompt(string text)
    {
        return $@"Extract recipe information from the following text and return as structured JSON:

{text}

Extract:
- Title
- Description
- Prep time (minutes)
- Cook time (minutes)
- Servings
- Ingredients (with quantities and units)
- Instructions (as numbered steps)
- Detected allergens (common allergens like nuts, dairy, eggs, gluten, etc.)
- Difficulty level
- Dietary information (vegetarian, vegan, gluten-free, etc.)
- Confidence score (0-1) indicating extraction accuracy

Return as JSON matching this structure:
{{
  ""title"": ""Recipe Name"",
  ""description"": ""Description"",
  ""prepTimeMinutes"": 15,
  ""cookTimeMinutes"": 30,
  ""servings"": 4,
  ""ingredients"": [
    {{""name"": ""flour"", ""quantity"": ""2"", ""unit"": ""cups"", ""notes"": """"}}
  ],
  ""instructions"": [""step 1"", ""step 2""],
  ""detectedAllergens"": [""gluten"", ""dairy""],
  ""difficulty"": ""Easy"",
  ""dietaryInfo"": [""vegetarian""],
  ""confidenceScore"": 0.95
}}";
    }

    private string BuildMealPlanPrompt(MealPlanSuggestionRequest request)
    {
        return $@"Create a {request.DaysToPlans}-day meal plan based on:

Available Ingredients: {string.Join(", ", request.AvailableIngredients)}
User Allergens (MUST AVOID): {string.Join(", ", request.UserAllergens)}
Dietary Preferences: {string.Join(", ", request.DietaryPreferences)}
{(request.DailyCalorieTarget.HasValue ? $"Daily Calorie Target: {request.DailyCalorieTarget}" : "")}
{(request.MinimizeWaste ? "Minimize food waste by reusing ingredients" : "")}
{(request.BalanceNutrition ? "Balance nutrition across meals" : "")}
{(request.WeeklyBudget.HasValue ? $"Weekly Budget: ${request.WeeklyBudget}" : "")}

For each day, suggest:
- Breakfast
- Lunch
- Dinner
- Optional snacks

Also include:
- Shopping list for missing ingredients
- Nutrition summary
- Estimated cost
- Reasoning for the plan

Format as JSON with days array, shopping list, nutrition summary, and cost.";
    }

    private string BuildAllergenDetectionPrompt(AllergenDetectionRequest request)
    {
        return $@"Analyze these ingredients for allergens:

Ingredients: {string.Join(", ", request.Ingredients)}
{(!string.IsNullOrEmpty(request.RecipeDescription) ? $"Recipe: {request.RecipeDescription}" : "")}

Detect and list:
1. All common food allergens (nuts, dairy, eggs, soy, wheat/gluten, fish, shellfish, sesame)
2. Source ingredient for each allergen
3. Confidence level (0-1)
4. Explanation
5. Potential cross-contamination risks

Format as JSON with detectedAllergens array, potentialCrossContamination array, and overall confidence score.";
    }

    private string BuildDietaryAnalysisPrompt(DietaryAnalysisRequest request)
    {
        var ingredients = request.Ingredients != null ? string.Join(", ", request.Ingredients) : "See recipe ID";

        return $@"Analyze this recipe for dietary suitability:

{(request.RecipeId.HasValue ? $"Recipe ID: {request.RecipeId}" : $"Ingredients: {ingredients}")}
User Health Goals: {string.Join(", ", request.UserHealthGoals)}
Dietary Restrictions: {string.Join(", ", request.DietaryRestrictions)}

Provide:
1. Whether it's suitable for the user
2. Health benefits
3. Health concerns
4. Nutrition highlights
5. Improvement suggestions with reasoning and impact scores
6. Overall health score (1-100)

Format as JSON with isSuitableForUser, healthBenefits, healthConcerns, nutritionHighlights, improvementSuggestions, and healthScore.";
    }

    private string BuildChatPrompt(AIChatRequest request)
    {
        var context = request.Context switch
        {
            "recipe" => "You are a helpful cooking assistant helping with recipe-related questions.",
            "nutrition" => "You are a helpful nutrition expert providing dietary advice.",
            "shopping" => "You are a helpful shopping assistant providing grocery shopping advice.",
            _ => "You are a helpful assistant for ExpressRecipe, a dietary management app."
        };

        var conversationHistory = request.ConversationHistory != null
            ? string.Join("\n", request.ConversationHistory.Select(m => $"{m.Role}: {m.Content}"))
            : "";

        return $@"{context}

{(!string.IsNullOrEmpty(conversationHistory) ? $"Conversation history:\n{conversationHistory}\n" : "")}

User message: {request.Message}

Provide a helpful, concise response.";
    }

    // Response Parsers (simplified - in production would use JSON parsing)
    private List<RecipeSuggestionDto> ParseRecipeSuggestions(string response, int count)
    {
        // TODO: Implement robust JSON parsing
        // For now, return mock data
        return Enumerable.Range(1, Math.Min(count, 3)).Select(i => new RecipeSuggestionDto
        {
            RecipeName = $"AI-Generated Recipe {i}",
            Description = "AI-generated recipe based on available ingredients",
            PrepTimeMinutes = 15,
            CookTimeMinutes = 30,
            Difficulty = "Medium",
            MatchScore = 75
        }).ToList();
    }

    private IngredientSubstitutionDto ParseSubstitutions(string response, string originalIngredient)
    {
        // TODO: Implement robust JSON parsing
        return new IngredientSubstitutionDto
        {
            OriginalIngredient = originalIngredient,
            Substitutions = new List<SubstitutionOptionDto>()
        };
    }

    private ExtractedRecipeDto ParseExtractedRecipe(string response)
    {
        // TODO: Implement robust JSON parsing
        return new ExtractedRecipeDto
        {
            Title = "Extracted Recipe",
            ConfidenceScore = 0.85
        };
    }

    private MealPlanSuggestionDto ParseMealPlan(string response, int days)
    {
        // TODO: Implement robust JSON parsing
        return new MealPlanSuggestionDto
        {
            Days = new List<DayMealPlanDto>()
        };
    }

    private AllergenDetectionResult ParseAllergenDetection(string response)
    {
        // TODO: Implement robust JSON parsing
        return new AllergenDetectionResult
        {
            ConfidenceScore = 0.9
        };
    }

    private DietaryAnalysisResult ParseDietaryAnalysis(string response)
    {
        // TODO: Implement robust JSON parsing
        return new DietaryAnalysisResult
        {
            HealthScore = 75
        };
    }

    private class OllamaResponse
    {
        public string? Response { get; set; }
    }
}
