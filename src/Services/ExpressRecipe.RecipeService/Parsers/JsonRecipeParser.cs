using System.Text.Json;

namespace ExpressRecipe.RecipeService.Parsers;

/// <summary>
/// Parser for generic JSON recipe format
/// Supports common JSON recipe schemas
/// </summary>
public class JsonRecipeParser : RecipeParserBase
{
    public override string ParserName => "JsonRecipeParser";
    public override string SourceType => "JSON";

    public override bool CanParse(string content, ParserContext context)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            // Check if it has recipe-like properties
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                   (root.TryGetProperty("name", out _) || root.TryGetProperty("title", out _));
        }
        catch
        {
            return false;
        }
    }

    public override async Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
    {
        var recipes = new List<ParsedRecipe>();

        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Handle array of recipes
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    var recipe = ParseRecipeElement(element);
                    if (recipe != null)
                    {
                        recipes.Add(recipe);
                    }
                }
            }
            // Handle single recipe
            else if (root.ValueKind == JsonValueKind.Object)
            {
                var recipe = ParseRecipeElement(root);
                if (recipe != null)
                {
                    recipes.Add(recipe);
                }
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse JSON recipe: {ex.Message}", ex);
        }

        return recipes;
    }

    private ParsedRecipe? ParseRecipeElement(JsonElement element)
    {
        var recipe = new ParsedRecipe();

        // Name/Title
        recipe.Name = GetStringValue(element, "name", "title", "recipeName") ?? "Untitled Recipe";

        // Description
        recipe.Description = GetStringValue(element, "description", "summary", "intro");

        // Author
        recipe.Author = GetStringValue(element, "author", "by", "creator");

        // Source
        recipe.Source = GetStringValue(element, "source", "sourceName");
        recipe.SourceUrl = GetStringValue(element, "sourceUrl", "url", "link");

        // Times
        recipe.PrepTimeMinutes = GetTimeValue(element, "prepTime", "preparationTime", "prep_time");
        recipe.CookTimeMinutes = GetTimeValue(element, "cookTime", "cookingTime", "cook_time");
        recipe.TotalTimeMinutes = GetTimeValue(element, "totalTime", "total_time") ??
                                  ((recipe.PrepTimeMinutes ?? 0) + (recipe.CookTimeMinutes ?? 0));

        // Servings
        recipe.Servings = GetIntValue(element, "servings", "yield", "serves");

        // Image
        recipe.ImageUrl = GetStringValue(element, "image", "imageUrl", "photo", "thumbnail");

        // Categories/Tags
        if (element.TryGetProperty("categories", out var categories))
        {
            recipe.Categories.AddRange(GetStringArray(categories));
        }
        if (element.TryGetProperty("tags", out var tags))
        {
            recipe.Tags.AddRange(GetStringArray(tags));
        }

        // Ingredients
        if (element.TryGetProperty("ingredients", out var ingredientsElement))
        {
            var ingredientOrder = 0;
            ParseIngredients(ingredientsElement, recipe, ref ingredientOrder);
        }

        // Instructions
        if (element.TryGetProperty("instructions", out var instructionsElement))
        {
            ParseInstructions(instructionsElement, recipe);
        }
        else if (element.TryGetProperty("steps", out var stepsElement))
        {
            ParseInstructions(stepsElement, recipe);
        }

        return string.IsNullOrWhiteSpace(recipe.Name) ? null : recipe;
    }

    private void ParseIngredients(JsonElement ingredientsElement, ParsedRecipe recipe, ref int order)
    {
        if (ingredientsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ingredientsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    // Simple string ingredient
                    var ingredient = ParseIngredientString(item.GetString() ?? "", order++);
                    if (ingredient != null)
                    {
                        recipe.Ingredients.Add(ingredient);
                    }
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    // Structured ingredient object
                    var ingredient = new ParsedIngredient
                    {
                        Order = order++,
                        Quantity = GetDecimalValue(item, "quantity", "amount"),
                        Unit = GetStringValue(item, "unit", "measure"),
                        IngredientName = GetStringValue(item, "name", "ingredient", "item") ?? "",
                        Preparation = GetStringValue(item, "preparation", "prep"),
                        Notes = GetStringValue(item, "notes", "note"),
                        IsOptional = GetBoolValue(item, "optional", "isOptional") ?? false,
                        OriginalText = GetStringValue(item, "text", "original") ??
                                      $"{GetDecimalValue(item, "quantity")} {GetStringValue(item, "unit")} {GetStringValue(item, "name")}"
                    };
                    recipe.Ingredients.Add(ingredient);
                }
            }
        }
    }

    private void ParseInstructions(JsonElement instructionsElement, ParsedRecipe recipe)
    {
        var stepNumber = 0;

        if (instructionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in instructionsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    recipe.Instructions.Add(new ParsedInstruction
                    {
                        StepNumber = ++stepNumber,
                        InstructionText = CleanText(item.GetString() ?? "")
                    });
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    recipe.Instructions.Add(new ParsedInstruction
                    {
                        StepNumber = GetIntValue(item, "step", "number") ?? ++stepNumber,
                        InstructionText = GetStringValue(item, "text", "instruction", "description") ?? "",
                        TimeMinutes = GetTimeValue(item, "time", "duration"),
                        Temperature = GetIntValue(item, "temperature", "temp"),
                        TemperatureUnit = GetStringValue(item, "temperatureUnit", "tempUnit")
                    });
                }
            }
        }
        else if (instructionsElement.ValueKind == JsonValueKind.String)
        {
            // Single string with all instructions
            var text = instructionsElement.GetString() ?? "";
            var lines = text.Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    recipe.Instructions.Add(new ParsedInstruction
                    {
                        StepNumber = ++stepNumber,
                        InstructionText = CleanText(line)
                    });
                }
            }
        }
    }

    private ParsedIngredient? ParseIngredientString(string text, int order)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var (quantity, unit, remaining) = ParseQuantityAndUnit(text);
        var (ingredient, preparation) = ExtractPreparation(remaining);

        return new ParsedIngredient
        {
            Order = order,
            Quantity = quantity,
            Unit = unit,
            IngredientName = ingredient,
            Preparation = preparation,
            IsOptional = IsOptionalIngredient(text),
            OriginalText = text
        };
    }

    // Helper methods for JSON parsing
    private string? GetStringValue(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private int? GetIntValue(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                    return value;
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
                    return value;
            }
        }
        return null;
    }

    private decimal? GetDecimalValue(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var value))
                    return value;
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out value))
                    return value;
            }
        }
        return null;
    }

    private bool? GetBoolValue(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True)
                    return true;
                if (prop.ValueKind == JsonValueKind.False)
                    return false;
            }
        }
        return null;
    }

    private int? GetTimeValue(JsonElement element, params string[] propertyNames)
    {
        var timeStr = GetStringValue(element, propertyNames);
        return ParseTime(timeStr);
    }

    private List<string> GetStringArray(JsonElement element)
    {
        var result = new List<string>();
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(value);
                    }
                }
            }
        }
        return result;
    }
}
