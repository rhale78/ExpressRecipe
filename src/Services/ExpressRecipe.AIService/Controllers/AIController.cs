using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ExpressRecipe.Client.Shared.Models.AI;
using ExpressRecipe.AIService.Services;

namespace ExpressRecipe.AIService.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<AIController> _logger;

    public AIController(IOllamaService ollamaService, ILogger<AIController> logger)
    {
        _ollamaService = ollamaService;
        _logger = logger;
    }

    [HttpPost("recipes/suggest")]
    public async Task<ActionResult<List<RecipeSuggestionDto>>> GetRecipeSuggestions([FromBody] RecipeSuggestionRequest request)
    {
        try
        {
            var suggestions = await _ollamaService.GenerateRecipeSuggestionsAsync(request);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recipe suggestions");
            return StatusCode(500, "Failed to generate recipe suggestions");
        }
    }

    [HttpPost("ingredients/substitute")]
    public async Task<ActionResult<IngredientSubstitutionDto>> GetIngredientSubstitutions([FromBody] IngredientSubstitutionRequest request)
    {
        try
        {
            var substitutions = await _ollamaService.GenerateSubstitutionsAsync(request);
            return Ok(substitutions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ingredient substitutions");
            return StatusCode(500, "Failed to generate substitutions");
        }
    }

    [HttpPost("recipes/extract")]
    public async Task<ActionResult<ExtractedRecipeDto>> ExtractRecipe([FromBody] RecipeExtractionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.RecipeText))
            {
                return BadRequest("Recipe text is required");
            }

            var extractedRecipe = await _ollamaService.ExtractRecipeFromTextAsync(request.RecipeText);
            return Ok(extractedRecipe);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting recipe");
            return StatusCode(500, "Failed to extract recipe");
        }
    }

    [HttpPost("meal-plans/suggest")]
    public async Task<ActionResult<MealPlanSuggestionDto>> GetMealPlanSuggestions([FromBody] MealPlanSuggestionRequest request)
    {
        try
        {
            var mealPlan = await _ollamaService.GenerateMealPlanAsync(request);
            return Ok(mealPlan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating meal plan");
            return StatusCode(500, "Failed to generate meal plan");
        }
    }

    [HttpPost("allergens/detect")]
    public async Task<ActionResult<AllergenDetectionResult>> DetectAllergens([FromBody] AllergenDetectionRequest request)
    {
        try
        {
            var result = await _ollamaService.DetectAllergensAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting allergens");
            return StatusCode(500, "Failed to detect allergens");
        }
    }

    [HttpPost("shopping/optimize")]
    public async Task<ActionResult<ShoppingOptimizationResult>> OptimizeShoppingList([FromBody] ShoppingOptimizationRequest request)
    {
        try
        {
            // TODO: Implement shopping optimization
            return Ok(new ShoppingOptimizationResult());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing shopping list");
            return StatusCode(500, "Failed to optimize shopping list");
        }
    }

    [HttpPost("dietary/analyze")]
    public async Task<ActionResult<DietaryAnalysisResult>> AnalyzeDiet([FromBody] DietaryAnalysisRequest request)
    {
        try
        {
            var analysis = await _ollamaService.AnalyzeDietAsync(request);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing diet");
            return StatusCode(500, "Failed to analyze diet");
        }
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AIChatResponse>> Chat([FromBody] AIChatRequest request)
    {
        try
        {
            var response = await _ollamaService.ChatAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI chat");
            return StatusCode(500, "Failed to process chat message");
        }
    }
}
