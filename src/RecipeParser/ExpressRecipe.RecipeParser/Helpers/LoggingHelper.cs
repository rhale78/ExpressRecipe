using ExpressRecipe.RecipeParser.Models;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.RecipeParser.Helpers;

public static class LoggingHelper
{
    public static void LogBatchError(ILogger? logger, List<ParseError> errors, string message, Exception? ex = null)
    {
        var error = new ParseError { Level = "batch", Message = message, Exception = ex };
        errors.Add(error);
        logger?.LogError(ex, "Batch parse error: {Message}", message);
    }

    public static void LogRecipeError(ILogger? logger, List<ParseError> errors, int index, string? title, string message, Exception? ex = null)
    {
        var error = new ParseError { Level = "recipe", RecipeIndex = index, RecipeTitle = title, Message = message, Exception = ex };
        errors.Add(error);
        logger?.LogWarning(ex, "Recipe #{Index} ({Title}) parse error: {Message}", index, title ?? "unknown", message);
    }
}
