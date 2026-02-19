using System.Text.RegularExpressions;

namespace ExpressRecipe.RecipeService.Parsers;

/// <summary>
/// Parser for extracting recipes from YouTube video descriptions
/// Handles common formats used by cooking channels
/// </summary>
public class YouTubeDescriptionParser : RecipeParserBase
{
    public override string ParserName => "YouTubeDescriptionParser";
    public override string SourceType => "YouTube";

    public override bool CanParse(string content, ParserContext context)
    {
        // Check if this is a YouTube URL or if content looks like a YouTube description
        if (context.SourceUrl?.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) == true ||
            context.SourceUrl?.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        // Check for common YouTube recipe description patterns
        return content.Contains("RECIPE", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("INGREDIENTS", StringComparison.OrdinalIgnoreCase);
    }

    public override Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
    {
        var recipe = new ParsedRecipe
        {
            SourceUrl = context.SourceUrl
        };

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
        {
            return Task.FromResult(new List<ParsedRecipe>());
        }

        // Extract recipe name (usually in first few lines or after "RECIPE:" label)
        recipe.Name = ExtractRecipeName(lines);

        var currentSection = "unknown";
        var ingredientOrder = 0;
        var stepNumber = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            // Skip common YouTube description noise
            if (IsNoiseContent(line))
                continue;

            // Detect section headers
            var newSection = DetectSection(line);
            if (newSection != "unknown")
            {
                currentSection = newSection;
                continue;
            }

            // Parse based on current section
            if (currentSection == "ingredients" || IsLikelyIngredient(line))
            {
                var ingredient = ParseIngredientLine(line, ingredientOrder++);
                if (ingredient != null)
                {
                    recipe.Ingredients.Add(ingredient);
                    currentSection = "ingredients";
                }
            }
            else if (currentSection == "instructions" || IsLikelyInstruction(line))
            {
                var instruction = ParseInstructionLine(line, ++stepNumber);
                if (instruction != null)
                {
                    recipe.Instructions.Add(instruction);
                    currentSection = "instructions";
                }
            }
            else if (line.Contains("serves", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("servings", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("yield", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"(\d+)\s*(?:servings?|serves|yield)", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var servings))
                {
                    recipe.Servings = servings;
                }
            }
            else if (line.Contains("prep", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("preparation", StringComparison.OrdinalIgnoreCase))
            {
                recipe.PrepTimeMinutes = ParseTime(line);
            }
            else if (line.Contains("cook", StringComparison.OrdinalIgnoreCase) &&
                     !line.Contains("cookbook", StringComparison.OrdinalIgnoreCase) &&
                     !line.Contains("cooker", StringComparison.OrdinalIgnoreCase))
            {
                recipe.CookTimeMinutes = ParseTime(line);
            }
            else if (string.IsNullOrEmpty(recipe.Description) && line.Length > 20 && !line.StartsWith("http"))
            {
                // Use first substantial non-URL line as description
                recipe.Description = line;
            }
        }

        // Calculate total time
        if (recipe.PrepTimeMinutes.HasValue || recipe.CookTimeMinutes.HasValue)
        {
            recipe.TotalTimeMinutes = (recipe.PrepTimeMinutes ?? 0) + (recipe.CookTimeMinutes ?? 0);
        }

        // If no name was found, use a default
        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            recipe.Name = "YouTube Recipe";
        }

        return Task.FromResult(new List<ParsedRecipe> { recipe });
    }

    private string ExtractRecipeName(List<string> lines)
    {
        // Look for explicit recipe name markers
        foreach (var line in lines.Take(10))
        {
            // Check for "RECIPE:" or "RECIPE -" patterns
            var recipeMatch = Regex.Match(line, @"RECIPE\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase);
            if (recipeMatch.Success)
            {
                return CleanText(recipeMatch.Groups[1].Value);
            }

            // Check for recipe name in title case or all caps (but not other section headers)
            if (line.Length > 5 && line.Length < 100 && 
                !line.Contains("INGREDIENT", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("INSTRUCTION", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("STEP", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                (line.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c)) || 
                 Regex.IsMatch(line, @"^[A-Z][a-z]+")))
            {
                return CleanText(line);
            }
        }

        // Default to first non-empty line if nothing better found
        return CleanText(lines.FirstOrDefault() ?? "YouTube Recipe");
    }

    private bool IsNoiseContent(string line)
    {
        var noisePhrases = new[]
        {
            "subscribe", "like", "comment", "follow", "social media",
            "instagram", "twitter", "facebook", "tiktok", "patreon",
            "buy my book", "merch", "sponsored", "affiliate", 
            "click here", "link in description", "watch next",
            "support", "donate", "channel", "video"
        };

        var lower = line.ToLower();
        return noisePhrases.Any(phrase => lower.Contains(phrase)) || 
               line.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    private string DetectSection(string line)
    {
        var lower = line.ToLower().Trim();

        // Remove common decorators
        lower = Regex.Replace(lower, @"^[\-=*#]+\s*|\s*[\-=*#]+$", "");

        if (Regex.IsMatch(lower, @"^ingredients?:?\s*$"))
            return "ingredients";

        if (Regex.IsMatch(lower, @"^(?:instructions?|directions?|steps?|method|procedure|how\s+to\s+make):?\s*$"))
            return "instructions";

        if (Regex.IsMatch(lower, @"^notes?:?\s*$"))
            return "notes";

        return "unknown";
    }

    private bool IsLikelyIngredient(string line)
    {
        // YouTube descriptions often use bullet points or dashes
        if (line.StartsWith("•") || line.StartsWith("-") || line.StartsWith("*"))
            return true;

        // Ingredients typically start with a quantity
        if (char.IsDigit(line[0]))
            return true;

        // Or common quantity words
        if (Regex.IsMatch(line, @"^\s*(?:a|an|the|some|half|quarter)\s+", RegexOptions.IgnoreCase))
            return true;

        // Check for measurement units
        var units = new[] { "cup", "tsp", "tbsp", "oz", "lb", "gram", "liter", "ml", "kg", "pinch", "dash", "clove" };
        return units.Any(unit => line.Contains(unit, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsLikelyInstruction(string line)
    {
        // Instructions often start with step numbers
        if (Regex.IsMatch(line, @"^\d+[\.)]\s"))
            return true;

        // Or action verbs
        var verbs = new[]
        {
            "preheat", "heat", "cook", "bake", "boil", "simmer", "fry", "sauté", "sear",
            "mix", "stir", "whisk", "blend", "combine", "add", "pour", "spread",
            "place", "arrange", "transfer", "remove", "drain", "rinse", "wash",
            "chop", "dice", "mince", "slice", "cut", "peel", "grate", "shred",
            "season", "garnish", "serve"
        };

        var lower = line.ToLower();
        return verbs.Any(verb => lower.StartsWith(verb));
    }

    private ParsedIngredient? ParseIngredientLine(string line, int order)
    {
        // Remove bullet points or dashes
        line = Regex.Replace(line, @"^[•\-*]\s+", "");

        var (quantity, unit, remaining) = ParseQuantityAndUnit(line);

        if (string.IsNullOrWhiteSpace(remaining))
            remaining = line;

        var (ingredient, preparation) = ExtractPreparation(remaining);

        if (string.IsNullOrWhiteSpace(ingredient) || ingredient.Length < 2)
            return null;

        return new ParsedIngredient
        {
            Order = order,
            Quantity = quantity,
            Unit = unit,
            IngredientName = ingredient,
            Preparation = preparation,
            IsOptional = IsOptionalIngredient(line),
            OriginalText = line
        };
    }

    private ParsedInstruction? ParseInstructionLine(string line, int stepNumber)
    {
        // Remove step numbers if present
        line = Regex.Replace(line, @"^\d+[\.)]\s+", "");

        if (string.IsNullOrWhiteSpace(line) || line.Length < 10)
            return null;

        var instruction = new ParsedInstruction
        {
            StepNumber = stepNumber,
            InstructionText = CleanText(line)
        };

        // Extract temperature if present
        var tempMatch = Regex.Match(line, @"(\d+)\s*°?\s*([FfCc])", RegexOptions.IgnoreCase);
        if (tempMatch.Success)
        {
            instruction.Temperature = int.Parse(tempMatch.Groups[1].Value);
            instruction.TemperatureUnit = tempMatch.Groups[2].Value.ToUpper();
        }

        // Extract time if present (e.g., "for 30 minutes", "until golden, about 5-7 minutes")
        var timeMatch = Regex.Match(line, @"(?:for|about|approximately)\s+(\d+)(?:\s*-\s*(\d+))?\s*(?:minute|min)", RegexOptions.IgnoreCase);
        if (timeMatch.Success)
        {
            var time1 = int.Parse(timeMatch.Groups[1].Value);
            var time2 = timeMatch.Groups[2].Success ? int.Parse(timeMatch.Groups[2].Value) : time1;
            instruction.TimeMinutes = (time1 + time2) / 2; // Average if range
        }

        return instruction;
    }
}
