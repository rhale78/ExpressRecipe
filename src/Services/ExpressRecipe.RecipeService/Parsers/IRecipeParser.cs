using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.Parsers;

/// <summary>
/// Interface for recipe parsers that convert various formats to our standard recipe format
/// </summary>
public interface IRecipeParser
{
    /// <summary>
    /// Parser name (should match RecipeImportSource.ParserClassName)
    /// </summary>
    string ParserName { get; }

    /// <summary>
    /// Supported source types
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// Parse recipe(s) from content
    /// </summary>
    /// <param name="content">Raw content to parse (file content, URL, etc.)</param>
    /// <param name="context">Additional context (filename, URL, etc.)</param>
    /// <returns>List of parsed recipes</returns>
    Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context);

    /// <summary>
    /// Validate if content can be parsed by this parser
    /// </summary>
    bool CanParse(string content, ParserContext context);
}

/// <summary>
/// Context information for parsing
/// </summary>
public class ParserContext
{
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
    public string? SourceUrl { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Parsed recipe result
/// </summary>
public class ParsedRecipe
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }

    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? TotalTimeMinutes { get; set; }
    public int? Servings { get; set; }

    public List<ParsedIngredient> Ingredients { get; set; } = new();
    public List<ParsedInstruction> Instructions { get; set; } = new();

    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? ImageUrl { get; set; }

    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Parsed ingredient
/// </summary>
public class ParsedIngredient
{
    public int Order { get; set; }
    public string? SectionName { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string? Preparation { get; set; }
    public string? Notes { get; set; }
    public bool IsOptional { get; set; }

    /// <summary>
    /// Original unparsed text
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;
}

/// <summary>
/// Parsed instruction
/// </summary>
public class ParsedInstruction
{
    public int StepNumber { get; set; }
    public string? SectionName { get; set; }
    public string InstructionText { get; set; } = string.Empty;
    public int? TimeMinutes { get; set; }
    public int? Temperature { get; set; }
    public string? TemperatureUnit { get; set; } // F or C
}

/// <summary>
/// Parse error for tracking failures
/// </summary>
public class ParseError
{
    public string Message { get; set; } = string.Empty;
    public string? LineNumber { get; set; }
    public string? Context { get; set; }
}
