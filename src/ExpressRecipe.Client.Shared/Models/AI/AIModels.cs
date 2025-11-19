namespace ExpressRecipe.Client.Shared.Models.AI;

// AI Recipe Suggestions
public class RecipeSuggestionRequest
{
    public List<string> AvailableIngredients { get; set; } = new();
    public List<string> UserAllergens { get; set; } = new();
    public List<string> UserDislikes { get; set; } = new();
    public List<string> DietaryPreferences { get; set; } = new();
    public string? CuisinePreference { get; set; }
    public int? MaxCookTimeMinutes { get; set; }
    public string? Difficulty { get; set; }
    public int SuggestionsCount { get; set; } = 5;
}

public class RecipeSuggestionDto
{
    public string RecipeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Ingredients { get; set; } = new();
    public List<string> MissingIngredients { get; set; } = new();
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public List<string> Instructions { get; set; } = new();
    public double MatchScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

// Ingredient Substitution
public class IngredientSubstitutionRequest
{
    public string OriginalIngredient { get; set; } = string.Empty;
    public string RecipeContext { get; set; } = string.Empty;
    public List<string> UserAllergens { get; set; } = new();
    public List<string> AvailableIngredients { get; set; } = new();
    public bool PreferHealthier { get; set; }
}

public class IngredientSubstitutionDto
{
    public string OriginalIngredient { get; set; } = string.Empty;
    public List<SubstitutionOptionDto> Substitutions { get; set; } = new();
}

public class SubstitutionOptionDto
{
    public string Ingredient { get; set; } = string.Empty;
    public string Ratio { get; set; } = "1:1";
    public string Explanation { get; set; } = string.Empty;
    public bool IsHealthier { get; set; }
    public bool IsAvailable { get; set; }
    public int SuitabilityScore { get; set; } // 1-10
}

// Recipe Extraction from Text/URL
public class RecipeExtractionRequest
{
    public string? RecipeText { get; set; }
    public string? RecipeUrl { get; set; }
    public byte[]? RecipeImage { get; set; }
}

public class ExtractedRecipeDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; }
    public List<ExtractedIngredientDto> Ingredients { get; set; } = new();
    public List<string> Instructions { get; set; } = new();
    public List<string> DetectedAllergens { get; set; } = new();
    public string Difficulty { get; set; } = string.Empty;
    public List<string> DietaryInfo { get; set; } = new();
    public double ConfidenceScore { get; set; }
}

public class ExtractedIngredientDto
{
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

// Meal Planning Suggestions
public class MealPlanSuggestionRequest
{
    public int DaysToPlans { get; set; } = 7;
    public List<string> AvailableIngredients { get; set; } = new();
    public List<string> UserAllergens { get; set; } = new();
    public List<string> DietaryPreferences { get; set; } = new();
    public int? DailyCalorieTarget { get; set; }
    public bool MinimizeWaste { get; set; } = true;
    public bool BalanceNutrition { get; set; } = true;
    public decimal? WeeklyBudget { get; set; }
}

public class MealPlanSuggestionDto
{
    public List<DayMealPlanDto> Days { get; set; } = new();
    public ShoppingListSuggestionDto ShoppingList { get; set; } = new();
    public NutritionSummaryDto NutritionSummary { get; set; } = new();
    public decimal EstimatedCost { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class DayMealPlanDto
{
    public DateTime Date { get; set; }
    public string? Breakfast { get; set; }
    public string? Lunch { get; set; }
    public string? Dinner { get; set; }
    public List<string>? Snacks { get; set; }
}

public class ShoppingListSuggestionDto
{
    public List<ShoppingItemDto> Items { get; set; } = new();
    public decimal EstimatedTotal { get; set; }
}

public class ShoppingItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? EstimatedPrice { get; set; }
    public bool IsAlreadyOwned { get; set; }
}

public class NutritionSummaryDto
{
    public int AverageDailyCalories { get; set; }
    public decimal AverageDailyProtein { get; set; }
    public decimal AverageDailyCarbs { get; set; }
    public decimal AverageDailyFat { get; set; }
}

// Allergen Detection
public class AllergenDetectionRequest
{
    public List<string> Ingredients { get; set; } = new();
    public string? RecipeDescription { get; set; }
}

public class AllergenDetectionResult
{
    public List<DetectedAllergenDto> DetectedAllergens { get; set; } = new();
    public List<string> PotentialCrossContamination { get; set; } = new();
    public double ConfidenceScore { get; set; }
}

public class DetectedAllergenDto
{
    public string Allergen { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

// Shopping List Optimization
public class ShoppingOptimizationRequest
{
    public List<string> RequiredItems { get; set; } = new();
    public List<string> InventoryItems { get; set; } = new();
    public string? PreferredStore { get; set; }
    public decimal? Budget { get; set; }
}

public class ShoppingOptimizationResult
{
    public List<OptimizedShoppingItemDto> OptimizedList { get; set; } = new();
    public List<string> StoreRecommendations { get; set; } = new();
    public decimal EstimatedTotal { get; set; }
    public List<string> MoneyOptimizationTips { get; set; } = new();
}

public class OptimizedShoppingItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string RecommendedStore { get; set; } = string.Empty;
    public decimal? EstimatedPrice { get; set; }
    public string? AlternativeSuggestion { get; set; }
}

// Dietary Analysis
public class DietaryAnalysisRequest
{
    public Guid? RecipeId { get; set; }
    public List<string>? Ingredients { get; set; }
    public List<string> UserHealthGoals { get; set; } = new();
    public List<string> DietaryRestrictions { get; set; } = new();
}

public class DietaryAnalysisResult
{
    public bool IsSuitableForUser { get; set; }
    public List<string> HealthBenefits { get; set; } = new();
    public List<string> HealthConcerns { get; set; } = new();
    public List<string> NutritionHighlights { get; set; } = new();
    public List<ImprovementSuggestionDto> ImprovementSuggestions { get; set; } = new();
    public int HealthScore { get; set; } // 1-100
}

public class ImprovementSuggestionDto
{
    public string Suggestion { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public int ImpactScore { get; set; } // 1-10
}

// AI Chat/Assistant
public class AIChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string Context { get; set; } = "general"; // general, recipe, nutrition, shopping
    public List<ChatMessageDto>? ConversationHistory { get; set; }
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty; // user, assistant
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class AIChatResponse
{
    public string Message { get; set; } = string.Empty;
    public List<string>? SuggestedActions { get; set; }
    public object? StructuredData { get; set; }
}

// AI Configuration
public class AIModelConfig
{
    public string Provider { get; set; } = "Ollama"; // Ollama, OpenAI, AzureOpenAI
    public string Model { get; set; } = "llama2"; // llama2, mistral, codellama, gpt-4, etc.
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
}
