using System.Text.Json;

namespace ExpressRecipe.RecipeService.Parsers;

/// <summary>
/// Parser for Recipe Keeper export format
/// Recipe Keeper uses a specific JSON structure
/// </summary>
public class RecipeKeeperParser : JsonRecipeParser
{
    public override string ParserName => "RecipeKeeperParser";
    public override string SourceType => "JSON";

    public override bool CanParse(string content, ParserContext context)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Recipe Keeper exports typically have these fields
            return root.TryGetProperty("recipeName", out _) ||
                   root.TryGetProperty("recipeIngredients", out _) ||
                   (root.TryGetProperty("version", out var version) &&
                    version.GetString()?.Contains("RecipeKeeper") == true);
        }
        catch
        {
            return false;
        }
    }

    public override async Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
    {
        // Recipe Keeper format is similar to generic JSON but with specific field names
        // We can leverage the base JsonRecipeParser but might need custom handling

        var recipes = await base.ParseAsync(content, context);

        // Post-process to handle Recipe Keeper specifics
        foreach (var recipe in recipes)
        {
            recipe.Source = "Recipe Keeper";
        }

        return recipes;
    }
}

/// <summary>
/// Parser for Paprika recipe manager format
/// Paprika uses .paprikarecipe files (JSON-based)
/// </summary>
public class PaprikaParser : JsonRecipeParser
{
    public override string ParserName => "PaprikaParser";
    public override string SourceType => "JSON";

    public override bool CanParse(string content, ParserContext context)
    {
        // Check for .paprikarecipe extension
        if (context.FileName?.EndsWith(".paprikarecipe", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Paprika recipes have specific fields
            return root.TryGetProperty("uid", out _) &&
                   root.TryGetProperty("name", out _) &&
                   root.TryGetProperty("ingredients", out _);
        }
        catch
        {
            return false;
        }
    }

    public override async Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
    {
        var recipes = await base.ParseAsync(content, context);

        // Post-process to handle Paprika specifics
        foreach (var recipe in recipes)
        {
            recipe.Source = "Paprika";

            // Paprika stores ingredients as single string, need to split
            if (recipe.Ingredients.Count == 1 &&
                recipe.Ingredients[0].IngredientName.Contains('\n'))
            {
                var ingredients = new List<ParsedIngredient>();
                var lines = recipe.Ingredients[0].IngredientName.Split('\n');
                var order = 0;

                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var (quantity, unit, remaining) = ParseQuantityAndUnit(line.Trim());
                        var (ingredient, preparation) = ExtractPreparation(remaining);

                        ingredients.Add(new ParsedIngredient
                        {
                            Order = order++,
                            Quantity = quantity,
                            Unit = unit,
                            IngredientName = ingredient,
                            Preparation = preparation,
                            IsOptional = IsOptionalIngredient(line),
                            OriginalText = line.Trim()
                        });
                    }
                }

                recipe.Ingredients = ingredients;
            }

            // Paprika stores directions as single string, need to split
            if (recipe.Instructions.Count == 1 &&
                recipe.Instructions[0].InstructionText.Contains('\n'))
            {
                var instructions = new List<ParsedInstruction>();
                var lines = recipe.Instructions[0].InstructionText.Split('\n');
                var stepNumber = 0;

                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        instructions.Add(new ParsedInstruction
                        {
                            StepNumber = ++stepNumber,
                            InstructionText = CleanText(line)
                        });
                    }
                }

                recipe.Instructions = instructions;
            }
        }

        return recipes;
    }
}

/// <summary>
/// Parser for MasterCook MX2/MXP format
/// This is an XML-based format
/// </summary>
public class MasterCookParser : RecipeParserBase
{
    public override string ParserName => "MasterCookParser";
    public override string SourceType => "MealMaster";

    public override bool CanParse(string content, ParserContext context)
    {
        // Check for XML structure or .mx2/.mxp extension
        return (context.FileName?.EndsWith(".mx2", StringComparison.OrdinalIgnoreCase) == true ||
                context.FileName?.EndsWith(".mxp", StringComparison.OrdinalIgnoreCase) == true ||
                content.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)) &&
               content.Contains("<Recipe>", StringComparison.OrdinalIgnoreCase);
    }

    public override Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
    {
        // In a real implementation, this would use XML parsing
        // For now, return a placeholder that indicates XML support is needed

        var recipe = new ParsedRecipe
        {
            Name = "MasterCook Recipe",
            Description = "MasterCook XML import requires XML parsing library",
            Source = "MasterCook"
        };

        // TODO: Implement XML parsing when XML library is available
        // Would parse <Recipe>, <Name>, <Ingredients>, <Instructions>, etc.

        return Task.FromResult(new List<ParsedRecipe> { recipe });
    }
}
