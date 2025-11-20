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
/// Uses XML parsing for MasterCook's proprietary XML format
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
        var recipes = new List<ParsedRecipe>();

        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(content);

            // MasterCook files can contain multiple recipes
            var recipeElements = doc.Descendants("Recipe");

            foreach (var recipeElement in recipeElements)
            {
                var recipe = new ParsedRecipe
                {
                    Source = "MasterCook"
                };

                // Extract recipe name
                recipe.Name = recipeElement.Element("Name")?.Value ?? "Untitled Recipe";

                // Extract description/comments
                recipe.Description = recipeElement.Element("Description")?.Value ??
                                   recipeElement.Element("Comments")?.Value;

                // Extract category
                recipe.Category = recipeElement.Element("Category")?.Value;

                // Extract cuisine
                recipe.Cuisine = recipeElement.Element("Cuisine")?.Value;

                // Extract servings
                var servingsText = recipeElement.Element("Servings")?.Value ??
                                 recipeElement.Element("Yield")?.Value;
                if (!string.IsNullOrWhiteSpace(servingsText))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(servingsText, @"\d+");
                    if (match.Success && int.TryParse(match.Value, out var servings))
                    {
                        recipe.Servings = servings;
                    }
                }

                // Extract prep time
                recipe.PrepTimeMinutes = ParseTime(recipeElement.Element("PrepTime")?.Value);

                // Extract cook time
                recipe.CookTimeMinutes = ParseTime(recipeElement.Element("CookTime")?.Value);

                // Extract ingredients
                var ingredientsElement = recipeElement.Element("Ingredients");
                if (ingredientsElement != null)
                {
                    var order = 0;
                    foreach (var ingredientElement in ingredientsElement.Elements())
                    {
                        var ingredientText = ingredientElement.Value.Trim();
                        if (string.IsNullOrWhiteSpace(ingredientText))
                            continue;

                        // Check if it's a section header
                        if (ingredientElement.Name.LocalName == "IngredientSection" ||
                            ingredientElement.Name.LocalName == "Section")
                        {
                            // Section header - could add as a comment or skip
                            continue;
                        }

                        // Parse ingredient text
                        var (quantity, unit, remaining) = ParseQuantityAndUnit(ingredientText);
                        var (ingredient, preparation) = ExtractPreparation(remaining);

                        recipe.Ingredients.Add(new ParsedIngredient
                        {
                            Order = order++,
                            Quantity = quantity,
                            Unit = unit,
                            IngredientName = ingredient,
                            Preparation = preparation,
                            IsOptional = IsOptionalIngredient(ingredientText),
                            OriginalText = ingredientText
                        });
                    }
                }

                // Extract instructions
                var instructionsElement = recipeElement.Element("Instructions") ??
                                        recipeElement.Element("Directions") ??
                                        recipeElement.Element("Steps");

                if (instructionsElement != null)
                {
                    var instructionText = instructionsElement.Value;

                    // Split into steps (by newline, numbered steps, or step elements)
                    var stepElements = instructionsElement.Elements("Step");
                    if (stepElements.Any())
                    {
                        var stepNumber = 0;
                        foreach (var stepElement in stepElements)
                        {
                            var text = CleanText(stepElement.Value);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                recipe.Instructions.Add(new ParsedInstruction
                                {
                                    StepNumber = ++stepNumber,
                                    InstructionText = text
                                });
                            }
                        }
                    }
                    else
                    {
                        // Split by paragraph or numbered list
                        var lines = instructionText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        var stepNumber = 0;

                        foreach (var line in lines)
                        {
                            var cleanLine = CleanText(line);
                            if (!string.IsNullOrWhiteSpace(cleanLine) && cleanLine.Length > 5)
                            {
                                // Remove leading numbers (e.g., "1. " or "1) ")
                                cleanLine = System.Text.RegularExpressions.Regex.Replace(
                                    cleanLine, @"^\d+[\.\)]\s*", "");

                                recipe.Instructions.Add(new ParsedInstruction
                                {
                                    StepNumber = ++stepNumber,
                                    InstructionText = cleanLine
                                });
                            }
                        }
                    }
                }

                // Extract notes
                recipe.Notes = recipeElement.Element("Notes")?.Value ??
                             recipeElement.Element("ChefNotes")?.Value;

                // Extract nutrition if available
                var nutritionElement = recipeElement.Element("Nutrition");
                if (nutritionElement != null)
                {
                    recipe.Nutrition = nutritionElement.Value;
                }

                recipes.Add(recipe);
            }
        }
        catch (Exception ex)
        {
            // If XML parsing fails, return a placeholder
            recipes.Add(new ParsedRecipe
            {
                Name = "MasterCook Recipe",
                Description = $"Failed to parse MasterCook XML: {ex.Message}",
                Source = "MasterCook"
            });
        }

        return Task.FromResult(recipes);
    }
}
