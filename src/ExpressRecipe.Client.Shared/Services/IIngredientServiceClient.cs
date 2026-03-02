using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// Result of ingredient validation indicating if it needs further processing
/// </summary>
public class IngredientValidationResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool NeedsFurtherProcessing { get; set; }
}

/// <summary>
/// Interface for high-performance ingredient microservice communication.
/// </summary>
public interface IIngredientServiceClient
{
    Task<Dictionary<string, Guid>> LookupIngredientIdsAsync(List<string> names);
    Task<Guid?> GetIngredientIdByNameAsync(string name);
    Task<IngredientDto?> GetIngredientAsync(Guid id);
    Task<List<IngredientDto>> GetAllIngredientsAsync();
    Task<Guid?> CreateIngredientAsync(CreateIngredientRequest request);
    Task<int> BulkCreateIngredientsAsync(List<string> names);

    // Parsing
    Task<List<string>> ParseIngredientListAsync(string ingredientsText);
    Task<Dictionary<string, List<string>>> BulkParseIngredientListsAsync(List<string> texts);
    Task<ParsedIngredientResult?> ParseIngredientStringAsync(string text);
    Task<Dictionary<string, ParsedIngredientResult>> BulkParseIngredientStringsAsync(List<string> texts);
    Task<IngredientValidationResult?> ValidateIngredientAsync(string ingredient);
}
