using System.Text.RegularExpressions;

namespace ExpressRecipe.IngredientService.Services.Parsing;

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
/// Advanced ingredient list parser that handles complex ingredient lists with subingredients,
/// parentheticals, and multi-level nesting.
/// Parses the full ingredient list text (e.g., "Water, Sugar, Contains 2% or less of: Salt")
/// into individual ingredient names.
/// </summary>
public interface IIngredientListParser
{
    List<string> ParseIngredients(string ingredientsText);
    Dictionary<string, List<string>> BulkParseIngredients(IEnumerable<string> ingredientTexts);
    IngredientValidationResult ValidateIngredient(string ingredient);
}

public class AdvancedIngredientParser : IIngredientListParser
{
    // COMPILED REGEX PATTERNS for performance (called 50K+ times per import)
    private static readonly Regex ContainsPatternRegex = new(
        @"(contains?\s+(?:\d+%?\s+)?(?:or\s+)?(?:less|more)\s+(?:than\s+)?(?:\d+%?\s+)?of[:\s]*)(.*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParenthesesPatternRegex = new(
        @"^([^(]+)\s*\(([^)]+)\)(.*)$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> StopPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "contains 2% or less of",
        "contains 2% or less",
        "contains less than 2% of",
        "contains less than 2%",
        "contains 2% of",
        "contains",
        "and the following",
        "one or more of",
        "or less of",
        "less than"
    };

    public List<string> ParseIngredients(string ingredientsText)
    {
        if (string.IsNullOrWhiteSpace(ingredientsText))
            return new List<string>();

        var ingredients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Normalize the text
        var normalized = NormalizeText(ingredientsText);

        // Extract all ingredients including those in parentheses
        ExtractIngredients(normalized, ingredients);

        // Clean and filter
        var cleaned = ingredients
            .Select(CleanIngredientName)
            .Where(ingredient =>
            {
                if (!IsValidIngredient(ingredient))
                    return false;

                // Validate for parsing issues
                var validation = ValidateIngredient(ingredient);
                if (validation.NeedsFurtherProcessing)
                {
                    // Log or handle ingredients that need further processing
                    // For now, exclude them from results
                    return false;
                }

                return validation.IsValid;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(i => i)
            .ToList();

        return cleaned;
    }

    public Dictionary<string, List<string>> BulkParseIngredients(IEnumerable<string> ingredientTexts)
    {
        var results = new Dictionary<string, List<string>>();
        foreach (var text in ingredientTexts.Distinct())
        {
            results[text] = ParseIngredients(text);
        }
        return results;
    }

    /// <summary>
    /// Validates an ingredient for parsing issues that require further processing
    /// </summary>
    public IngredientValidationResult ValidateIngredient(string ingredient)
    {
        if (string.IsNullOrWhiteSpace(ingredient))
        {
            return new IngredientValidationResult
            {
                IsValid = false,
                Reason = "Empty or whitespace",
                NeedsFurtherProcessing = false
            };
        }

        // Check for commas (indicates multiple ingredients)
        if (ingredient.Contains(','))
        {
            return new IngredientValidationResult
            {
                IsValid = false,
                Reason = "Contains comma - likely multiple ingredients",
                NeedsFurtherProcessing = true
            };
        }

        // Check for unbalanced parentheses with mismatch > 1
        int openParen = ingredient.Count(c => c == '(');
        int closeParen = ingredient.Count(c => c == ')');
        int parenMismatch = Math.Abs(openParen - closeParen);

        if (parenMismatch > 1)
        {
            return new IngredientValidationResult
            {
                IsValid = false,
                Reason = $"Unbalanced parentheses (mismatch: {parenMismatch})",
                NeedsFurtherProcessing = true
            };
        }

        // Check for unbalanced braces/brackets
        int openBrace = ingredient.Count(c => c == '{' || c == '[');
        int closeBrace = ingredient.Count(c => c == '}' || c == ']');
        if (openBrace != closeBrace)
        {
            return new IngredientValidationResult
            {
                IsValid = false,
                Reason = "Unbalanced braces or brackets",
                NeedsFurtherProcessing = true
            };
        }

        // Check length (>40 characters is suspicious for a single ingredient)
        if (ingredient.Length > 40)
        {
            // Allow if it's a recognized pattern (e.g., enriched flour with vitamins)
            if (!IsRecognizedLongIngredient(ingredient))
            {
                return new IngredientValidationResult
                {
                    IsValid = false,
                    Reason = $"Too long ({ingredient.Length} chars) - may need parsing",
                    NeedsFurtherProcessing = true
                };
            }
        }

        // Check for repeated separators (e.g., "- -", ". .", "/ /")
        if (Regex.IsMatch(ingredient, @"([.\-/])\s+\1"))
        {
            return new IngredientValidationResult
            {
                IsValid = false,
                Reason = "Contains repeated separator characters",
                NeedsFurtherProcessing = true
            };
        }

        // Check for multiple different separators (may indicate parsing issue)
        var separatorCount = 0;
        if (ingredient.Contains('-')) separatorCount++;
        if (ingredient.Contains('/')) separatorCount++;
        if (ingredient.Contains('.') && !ingredient.EndsWith('.')) separatorCount++;
        if (ingredient.Contains(':')) separatorCount++;

        if (separatorCount >= 2)
        {
            return new IngredientValidationResult
            {
                IsValid = false,
                Reason = "Multiple separator types - may need reparsing",
                NeedsFurtherProcessing = true
            };
        }

        // Check for starting with closing parenthesis (incomplete parse)
        if (ingredient.StartsWith(')'))
        {
            return new IngredientValidationResult
            {
                IsValid = false,
                Reason = "Starts with closing parenthesis - incomplete parse",
                NeedsFurtherProcessing = true
            };
        }

        // Check for ending with opening parenthesis without closing
        if (ingredient.EndsWith('(') || (ingredient.Contains('(') && !ingredient.Contains(')')))
        {
            return new IngredientValidationResult
            {
                IsValid = false,
                Reason = "Unclosed parenthesis - incomplete parse",
                NeedsFurtherProcessing = true
            };
        }

        // Check for "and" or "or" in middle (may indicate multiple ingredients)
        if (Regex.IsMatch(ingredient, @"\s+and\s+", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(ingredient, @"\s+or\s+", RegexOptions.IgnoreCase))
        {
            // Allow certain patterns like "mono and diglycerides"
            if (!IsRecognizedCompoundIngredient(ingredient))
            {
                return new IngredientValidationResult
                {
                    IsValid = false,
                    Reason = "Contains 'and' or 'or' - may be multiple ingredients",
                    NeedsFurtherProcessing = true
                };
            }
        }

        return new IngredientValidationResult
        {
            IsValid = true,
            Reason = "Valid",
            NeedsFurtherProcessing = false
        };
    }

    private bool IsRecognizedLongIngredient(string ingredient)
    {
        // Common legitimate long ingredient patterns
        var patterns = new[]
        {
            @"enriched\s+(wheat\s+)?flour",
            @"bleached\s+(wheat\s+)?flour",
            @"unbleached\s+(wheat\s+)?flour",
            @"enriched\s+durum\s+(wheat\s+)?semolina",
            @"low\s+moisture\s+part[- ]skim\s+mozzarella",
            @"pasteurized\s+process(ed)?\s+cheese",
            @"mono\s+and\s+diglycerides",
            @"sodium\s+acid\s+pyrophosphate",
            @"calcium\s+disodium\s+edta",
            @"high\s+fructose\s+corn\s+syrup"
        };

        var lowerIngredient = ingredient.ToLowerInvariant();
        return patterns.Any(pattern => Regex.IsMatch(lowerIngredient, pattern));
    }

    private bool IsRecognizedCompoundIngredient(string ingredient)
    {
        // Known compound ingredients that contain "and" or "or"
        var lowerIngredient = ingredient.ToLowerInvariant();

        var compoundPatterns = new[]
        {
            @"mono\s+and\s+diglycerides",
            @"mono-\s+and\s+diglycerides",
            @"salt\s+and\s+pepper",
            @"peanut\s+and/or\s+",
            @"canola\s+and/or\s+",
            @"soybean\s+and/or\s+",
            @"contains\s+one\s+or\s+more",
            @"red\s+and\s+green",
            @"black\s+and\s+white",
            @"\w+\s+oil\s+and/or\s+\w+\s+oil"
        };

        return compoundPatterns.Any(pattern => Regex.IsMatch(lowerIngredient, pattern));
    }

    private string NormalizeText(string text)
    {
        // Replace bullet points with commas
        text = text.Replace('•', ',');
        text = text.Replace('●', ',');
        text = text.Replace('·', ',');

        // Replace various bracket types with parentheses
        text = text.Replace('[', '(').Replace(']', ')');
        text = text.Replace('{', '(').Replace('}', ')');

        // Normalize separators
        text = text.Replace(';', ',');

        // Remove asterisks and other symbols used for annotations
        text = Regex.Replace(text, @"\*+|\†+|‡+", "");

        // Remove URLs
        text = Regex.Replace(text, @"https?://[^\s,)]+", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"www\.[^\s,)]+", "", RegexOptions.IgnoreCase);

        // Remove common non-ingredient patterns
        text = RemoveNonIngredientContent(text);

        // Balance parentheses before processing
        text = BalanceParentheses(text);

        // Replace multiple spaces with single space
        text = Regex.Replace(text, @"\s+", " ");

        // Remove excessive commas
        text = Regex.Replace(text, @",\s*,+", ",");

        return text.Trim();
    }

    private string RemoveNonIngredientContent(string text)
    {
        // Remove questions/comments prompts
        text = Regex.Replace(text, @"\b(questions?\s+or\s+comments?\??[^,.)]*)", "", RegexOptions.IgnoreCase);

        // Remove website/contact info
        text = Regex.Replace(text, @"\b(call|visit|email|contact|careline)\s+[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove packaging/recycling info
        text = Regex.Replace(text, @"\b(how2recycle|recyclable|raccolta\s+differenziata|plastica|metal\s+can)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove nutritional facts patterns
        text = Regex.Replace(text, @"%\s*daily\s+value[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"serving\s+size[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bcalories\b[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"total\s+(fat|carb|protein)\s+\d+g", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"reference\s+intake\s+of[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"valeurs\s+nutritionnelles[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"nutrition\s+facts?[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove barcode/UPC patterns
        text = Regex.Replace(text, @"\b\d{12,13}\b", "");

        // Remove product codes and SKUs
        text = Regex.Replace(text, @"\bsku#?\s*\d+", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b[A-Z]{2,}\d{4,}", ""); // Codes like TX78704, EC1N2HT

        // Remove storage instructions (multi-language)
        text = Regex.Replace(text, @"\b(rinse\s+and\s+insert|conservar|conserver|lagern|store\s+in)[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(à\s+consommer\s+avant|consume\s+within|termen\s+limită)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove country/origin info
        text = Regex.Replace(text, @"\b(product\s+of|made\s+in|manufactured\s+in|produit\s+en|origine|origin)\s*:?\s*\w+", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(turkey|france|italia|deutschland|nederland|republica\s+moldova)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove certification info
        text = Regex.Replace(text, @"\bcertified\s+(organic|gluten\s+free)[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(bio|fr-bio-\d+|agriculture\s+ue)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove "includes" added sugars pattern
        text = Regex.Replace(text, @"includes\s+\d+g?\s+added\s+sugars[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove addresses and postal codes
        text = Regex.Replace(text, @"\b\d{1,5}\s+[A-Z][a-z]+\s+(st|street|ave|avenue|rd|road|blvd|boulevard)[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bp\.?o\.?\s+box\s+\d+", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b\d{5}(-\d{4})?\s+(paris|dublin|milano|luxembourg|chisinau|lakes)", "", RegexOptions.IgnoreCase);

        // Remove directions for use
        text = Regex.Replace(text, @"\b(directions?|conseils?|indicații)[^,.)]*:", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(to\s+ensure|pour\s+assurer|shake\s+well|drink|mix)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove allergy warnings
        text = Regex.Replace(text, @"\b(allergy\s+advice|allergen\s+information|pour\s+les\s+allergènes)[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(may\s+contain|peut\s+contenir|puede\s+contener|può\s+contenere)[^,.)]*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(for\s+allergens|see\s+ingredients\s+in\s+bold)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove serving suggestions
        text = Regex.Replace(text, @"\b(serving\s+suggestion|suggestion\s+de\s+présentation)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove promotional text
        text = Regex.Replace(text, @"\b(scan\s+to|get\s+in\s+touch|refill\s+your)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove medical disclaimers
        text = Regex.Replace(text, @"\b(not\s+intended\s+to|prevent\s+any\s+disease|medicinale|farmaco)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove packaging info
        text = Regex.Replace(text, @"\b(packaged\s+in|packed\s+in|jar\s+recycle|lid\s+recycle)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove manufacturing info
        text = Regex.Replace(text, @"\b(manufactured\s+for|distributed\s+by|importat\s+si\s+distribuit)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove expiry date info
        text = Regex.Replace(text, @"\b(best\s+before|use\s+by|fecha\s+de\s+caducidad|data\s+di\s+scadenza)[^,.)]*", "", RegexOptions.IgnoreCase);

        // Remove weight/quantity info
        text = Regex.Replace(text, @"poids\s+net\s*:\s*\d+\s*g", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"net\s+wt\.?\s*\d+", "", RegexOptions.IgnoreCase);

        // Remove HTML entities
        text = text.Replace("&quot;", "").Replace("&lt;", "").Replace("&gt;", "");

        return text;
    }

    private string BalanceParentheses(string text)
    {
        int openCount = text.Count(c => c == '(');
        int closeCount = text.Count(c => c == ')');

        if (openCount == closeCount)
            return text;

        // More closing than opening - remove extra closing parentheses from the end
        if (closeCount > openCount)
        {
            int toRemove = closeCount - openCount;
            var sb = new System.Text.StringBuilder(text);
            for (int i = sb.Length - 1; i >= 0 && toRemove > 0; i--)
            {
                if (sb[i] == ')')
                {
                    sb.Remove(i, 1);
                    toRemove--;
                }
            }
            text = sb.ToString();
        }
        // More opening than closing - add closing parentheses at the end
        else if (openCount > closeCount)
        {
            int toAdd = openCount - closeCount;
            text += new string(')', toAdd);
        }

        return text;
    }

    private void ExtractIngredients(string text, HashSet<string> ingredients)
    {
        // Handle "contains X% or less of: ..." pattern (using compiled regex)
        var containsMatch = ContainsPatternRegex.Match(text);

        if (containsMatch.Success)
        {
            // Process the part before "contains"
            var beforeContains = text.Substring(0, containsMatch.Groups[1].Index);
            ExtractFromSegment(beforeContains, ingredients);

            // Process the part after "contains" (these are typically minor ingredients)
            var afterContains = containsMatch.Groups[2].Value;
            ExtractFromSegment(afterContains, ingredients);
        }
        else
        {
            // Process the entire text
            ExtractFromSegment(text, ingredients);
        }
    }

    private void ExtractFromSegment(string segment, HashSet<string> ingredients)
    {
        // Split by commas, but be aware of nested parentheses
        var parts = SplitRespectingParentheses(segment, ',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Check if this part has parentheses with subingredients (using compiled regex)
            var parenMatch = ParenthesesPatternRegex.Match(trimmed);

            if (parenMatch.Success)
            {
                // Main ingredient before parentheses
                var mainIngredient = parenMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(mainIngredient))
                {
                    ingredients.Add(mainIngredient);
                }

                // Sub-ingredients inside parentheses
                var subIngredients = parenMatch.Groups[2].Value;
                var subParts = SplitRespectingParentheses(subIngredients, ',');

                foreach (var subPart in subParts)
                {
                    var cleanSub = subPart.Trim();
                    if (!string.IsNullOrWhiteSpace(cleanSub) && !IsMetaInformation(cleanSub))
                    {
                        ingredients.Add(cleanSub);
                    }
                }

                // Anything after the closing parenthesis
                var after = parenMatch.Groups[3].Value.Trim();
                if (!string.IsNullOrWhiteSpace(after) && after != ".")
                {
                    ingredients.Add(after);
                }
            }
            else
            {
                // No parentheses, just add the ingredient
                if (!IsMetaInformation(trimmed))
                {
                    ingredients.Add(trimmed);
                }
            }
        }
    }

    private List<string> SplitRespectingParentheses(string text, char separator)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        int parenDepth = 0;

        foreach (char c in text)
        {
            if (c == '(')
            {
                parenDepth++;
                current.Append(c);
            }
            else if (c == ')')
            {
                parenDepth--;
                current.Append(c);
            }
            else if (c == separator && parenDepth == 0)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private string CleanIngredientName(string ingredient)
    {
        // Remove "issu de" and similar origin phrases (French)
        ingredient = Regex.Replace(ingredient, @"^issu\s+de\s+", "", RegexOptions.IgnoreCase);
        ingredient = Regex.Replace(ingredient, @"^proviennent\s+de\s+", "", RegexOptions.IgnoreCase);

        // Remove "édulcorant de source naturelle" type phrases
        ingredient = Regex.Replace(ingredient, @"^édulcorant\s+de\s+source\s+naturelle", "", RegexOptions.IgnoreCase);

        // Remove quantity indicators at the start (but not E-numbers like E450)
        ingredient = Regex.Replace(ingredient, @"^(?!E\d+)\d+%?\s*", "", RegexOptions.IgnoreCase);

        // Remove "and" or "or" at the start
        ingredient = Regex.Replace(ingredient, @"^(?:and|or|et)\s+", "", RegexOptions.IgnoreCase);

        // Remove trailing periods, colons, etc.
        ingredient = ingredient.Trim(' ', '.', ',', ':', '-', '_', ')');

        // Clean up "includes" pattern
        ingredient = Regex.Replace(ingredient, @"\bincludes\s+og\b", "", RegexOptions.IgnoreCase);

        // Remove "Traces éventuelles" and similar
        ingredient = Regex.Replace(ingredient, @"^traces?\s+(éventuelles?\s+)?d['e]\s*", "", RegexOptions.IgnoreCase);

        // Remove any remaining parenthetical content that's meta-information
        ingredient = Regex.Replace(ingredient, @"\s*\([^)]*\)\s*", match =>
        {
            var content = match.Value.Trim('(', ')', ' ');

            // Keep subingredients (ingredients inside parentheses)
            // But remove meta-information clauses
            if (Regex.IsMatch(content, @"\b(for|as|to|from|with|containing|added|no)\b", RegexOptions.IgnoreCase))
                return " ";

            // Keep it if it looks like actual ingredients (has commas or common ingredient words)
            if (content.Contains(',') || Regex.IsMatch(content, @"\b(milk|salt|sugar|water|oil)\b", RegexOptions.IgnoreCase))
                return match.Value;

            return " ";
        }).Trim();

        // Remove double spaces
        ingredient = Regex.Replace(ingredient, @"\s{2,}", " ");

        // Don't capitalize E-numbers or all-caps abbreviations
        if (ingredient.Length > 0)
        {
            // Check if it's an E-number (E followed by digits)
            if (!Regex.IsMatch(ingredient, @"^E\d+", RegexOptions.IgnoreCase))
            {
                // Check if it's not all uppercase (acronym)
                if (ingredient != ingredient.ToUpperInvariant() || ingredient.Length < 5)
                {
                    ingredient = char.ToUpper(ingredient[0]) + ingredient.Substring(1).ToLowerInvariant();
                }
            }
            else
            {
                // Normalize E-numbers to uppercase
                ingredient = Regex.Replace(ingredient, @"^e(\d+)", "E$1", RegexOptions.IgnoreCase);
            }
        }

        return ingredient;
    }

    private bool IsMetaInformation(string text)
    {
        // Check if it's just a number or percentage
        if (Regex.IsMatch(text, @"^\d+%?\.?$"))
            return true;

        // Check if it ends with "etc" or "etc." - indicates incomplete list
        if (Regex.IsMatch(text, @"\betc\.?$", RegexOptions.IgnoreCase))
            return true;

        // Check for truncated/incomplete patterns (no vowels except at start, or very short)
        if (text.Length <= 3 && !Regex.IsMatch(text, @"[aeiou]", RegexOptions.IgnoreCase))
            return true;

        var lowerText = text.ToLowerInvariant();

        // Check for common meta-information phrases
        var metaPhrases = new[]
        {
            "for color", "color added", "colored with", "artificial color",
            "for texture", "for tartness", "for flavor", "flavoring",
            "as preservative", "preservative", "antioxidant",
            "to maintain", "to preserve", "to retain", "to prevent",
            "adds a trivial", "adds a dietary", "adds negligible",
            "emulsifier", "stabilizer", "thickener",
            "processing aid", "anticaking", "anti-caking",
            "no nitrites", "no nitrates", "added except",
            "naturally occurring", "added as", "promote color",
            "firming agent", "color retention",
            "may contain", "peut contenir", "traces", "traces de",
            "live and active cultures", "cultures",
            "questions", "comments", "call", "visit",
            "gluten free", "certified", "association",
            "daily value", "nutrient in a serving",
            "raccolta", "plastica", "flacone", "capsula",
            "no added sugars", "added sugars"
        };

        foreach (var phrase in metaPhrases)
        {
            if (lowerText.Contains(phrase))
                return true;
        }

        // Check if it's ONLY a meta descriptor (e.g., just "emulsifier")
        var descriptorOnlyPatterns = new[]
        {
            @"^emulsifier$",
            @"^stabilizer$",
            @"^thickener$",
            @"^preservative$",
            @"^antioxidant$",
            @"^coloring$",
            @"^flavoring$",
            @"^sweetener$",
            @"^enzymes?$",
            @"^cultures$",
            @"^protein\s+\d+g$",
            @"^sodium\s+\d+mg$",
            @"^vitamin\s+[a-z](\s+and\s+[a-z])?$"
        };

        foreach (var pattern in descriptorOnlyPatterns)
        {
            if (Regex.IsMatch(lowerText, pattern))
                return true;
        }

        return false;
    }

    private bool IsValidIngredient(string ingredient)
    {
        if (string.IsNullOrWhiteSpace(ingredient))
            return false;

        // Must be at least 2 characters
        if (ingredient.Length < 2)
            return false;

        // Must not be longer than 100 characters (likely a sentence, not an ingredient)
        if (ingredient.Length > 100)
            return false;

        // Must not be just numbers
        if (Regex.IsMatch(ingredient, @"^\d+\.?\d*%?$"))
            return false;

        var lowerIngredient = ingredient.ToLowerInvariant();

        // Filter out packaging/recycling terms
        var rejectPatterns = new[]
        {
            @"^(pp|pet|hdpe|ldpe)\s*\d*$",  // Plastic codes
            @"^c/for\b",                     // Packaging instructions
            @"\b(riduttore|capsula|flacone|contenitori|confezione)\b",  // Italian packaging terms
            @"^(legno|plastica|raccolta)\b",     // Italian recycling terms
            @"^bo\s+italy$",                     // Country codes
            @"bush\s+brothers",                  // Company names
            @"^p\.?o\.?\s+box",                  // PO Box
            @"^\d{5}(-\d{4})?$",                 // ZIP codes
            @"^(questions?|comments?|call|visit)\b",  // Contact prompts
            @"^www\.",                           // Websites
            @"^\d{3}-\d{3}-\d{4}$",             // Phone numbers
            @"^(made|product)\s+of\b",          // Origin statements
            @"^(hri|do|la)\s+(chalo|do)\b",     // Garbled text
            @"^(vann|aroma|middel|vitamin)\s+[a-z]\s+(og|et|and)\s+[a-z]$",  // Nordic single vitamins
            @"^important\b",                     // Warning labels
            @"convient\s+pas",                   // French warnings
            @"risques\s+d'étouffement",         // Choking hazard
            @"^the\s+%\s+daily",                // Nutritional info start
            @"how\s+much\s+a\s+nutrient",       // Nutritional info
            @"contributes\s+to\s+a\s+daily",    // Nutritional info
            @"^\d+\s*calories?\b",              // Calorie info
            @"^serving",                         // Serving size
            @"^u\.?s\.?a\.?\s*$",               // USA alone
            @"^(ingredients?|ingredienti|ingrédients|zutaten|ingredientes)\b",  // Just the word "ingredients"
            @"^\d+%\s+(ar|dv|daily)",           // Percentage daily values
            @"^per\s+(serving|portion|100g)",   // Per serving info
            @"^valeurs?\s+nutritionnelles",     // French nutritional values
            @"^informati(on|e)\s+nutri",        // Italian/Dutch nutritional info
            @"^therapeutic[- ]grade",            // Medical grade claims
            @"^essential\s+oil\b",              // Essential oil (not food)
            @"^(drink|mix|shake|consume)\b",    // Directions
            @"^(recommended|conseil)\s+(daily|d'utilisation)",  // Recommendations
            @"^suitable\s+for\s+vegetarians",   // Dietary labels
            @"^(halal|kosher|vegan|gluten[- ]free)\s*$",  // Certifications alone
            @"^reference\s+intake",             // Nutritional reference
            @"^prodotto\s+(in|issu)",           // Product origin
            @"^(agriculture|farming)\s+(bio|organic)",  // Farming type
            @"^\d+\s*(mg|mcg|g)\s+\d+%",       // Nutrient amounts with percentages
            @"^directions?\s+for\s+use",        // Directions
            @"^advice\s+for\s+use",             // Usage advice
            @"^(not|ne)\s+(suitable|convient)", // Not suitable warnings
            @"^(allergen|allergènes)\b",        // Allergen headers
            @"^(see|voir|siehe)\s+",            // See instructions
            @"^container\s+is\b",               // Container info
            @"^freshness\s+preserved",          // Preservation method
            @"^(manufactured|produced|distributed)\s+(for|by|in)",  // Manufacturing info
            @"^\d+\s+grams?\s+per\b",           // Per serving measurements
            @"^portions?\s+par\b",              // Portions per
            @"^amount\s+per\s+serving",         // Amount per serving
            @"^(storage|conseils\s+de\s+conservation)",  // Storage info
            @"^keep\s+(refrigerated|frozen)",   // Storage instructions
            @"^(freeze|conge|surgel)",          // Freezing info
            @"^best\s+(before|used\s+by)",      // Best before
            @"^(expiry|expiration)\s+date",     // Expiry date
            @"^produced?\s+on\s+equipment",     // Cross-contamination warning
            @"^this\s+product\s+(contains|is)", // Product disclaimers
            @"^no\s+(artificial|preservatives|additives)\s*$",  // Marketing claims alone
            @"^(low|reduced|high|rich\s+in)\s+(fat|sugar|protein|sodium)\s*$",  // Nutritional claims alone
            @"^source\s+of\b",                  // Source of claims
            @"^free\s+from\b",                  // Free from claims
            @"^(sans|ohne|sin|senza)\s+(gluten|lactose)",  // Foreign "without" claims
            @"^(warnung|warning|avertissement|avvertenza)",  // Warnings
            @"^(clinical|medical)\s+(research|use)",  // Medical info
            @"^(registered|trademark)\b",       // Trademark info
            @"^tel\.?\s*:",                     // Telephone
            @"^\d{2,5}\s+(paris|dublin|milano|luxembourg|chisinau)",  // Postal addresses
            @"^(scan|visit|check)\s+(to|for|at)",  // Promotional directives
            @"^get\s+in\s+touch",               // Contact us
            @"^(ltd|inc|gmbh|sa|srl|llc)\.?\s*$",  // Company suffixes alone
            @"^(herbalife|mars|nestle|unilever)\b",  // Brand names (add more as needed)
            @"^\d+\s*%?\s+(ar|dv)\s*$",         // Just percentage values
            @"^enriched?\s+according\s+to",     // Enrichment statements
            @"^fortificado?\s+según",           // Spanish fortification
            @"^volgens\s+wet\b",                // Dutch legal compliance
            @"^gemäß\s+gesetz",                 // German legal compliance
            @"^(reprodu|produc|manufac)\s+in\s+\w+\s+de", // Manufactured in statements
            @"^(chicken|pork|beef)\s+(breast|meat)\s+(contain|with\s+up\s+to)\s+\d+%",  // Solution percentages
            @"^\d+%\s+(solution|absorbed)\b",   // Solution statements
            @"^fully\s+cooked\b",               // Cooking state
            @"^(natural|artificial)\s+flavor(ing|s)?\s*$",  // Just flavoring alone
            @"^color\s+added\s*$",              // Just color added alone
            @"^preservative\s+added\s*$",       // Just preservative alone
            @"^\d{8,}\s*$",                     // Long number sequences (barcodes)
            @"^(batch|lot)\s*(number|no\.?)\s*:",  // Batch numbers
            @"^sku\s*#?\s*\d+",                 // SKU numbers
            @"^barcode\s*:?"                    // Barcode labels
        };

        foreach (var pattern in rejectPatterns)
        {
            if (Regex.IsMatch(lowerIngredient, pattern))
                return false;
        }

        // Reject if it's all uppercase and longer than 50 chars (likely legal/nutritional text)
        if (ingredient.Length > 50 && ingredient == ingredient.ToUpperInvariant())
        {
            // Unless it looks like an actual ingredient (has common food words)
            var foodWords = new[] { "cheese", "milk", "wheat", "corn", "soy", "salt", "sugar" };
            if (!foodWords.Any(word => lowerIngredient.Contains(word)))
                return false;
        }

        // Check against stop phrases
        foreach (var stopPhrase in StopPhrases)
        {
            if (ingredient.Equals(stopPhrase, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Reject if it has too many special characters (likely corrupt data)
        int specialCharCount = ingredient.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && c != '-' && c != '(' && c != ')' && c != ',');
        if (specialCharCount > ingredient.Length / 3)
            return false;

        return true;
    }
}
