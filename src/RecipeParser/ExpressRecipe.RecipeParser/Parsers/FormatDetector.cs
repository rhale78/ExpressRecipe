namespace ExpressRecipe.RecipeParser.Parsers;

public static class FormatDetector
{
    public static string? Detect(string text, string? fileExtension = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        string? ext = fileExtension?.ToLowerInvariant()?.TrimStart('.');
        switch (ext)
        {
            case "mmf": return "MealMaster";
            case "cook": return "CookLang";
            case "mxp": return "MasterCook";
            case "mx2": return "MasterCook";
            case "rml": return "RecipeML";
            case "fdx": return "LivingCookbook";
            case "mgrx": return "MacGourmet";
            case "paprika": case "paprikarecipe": case "paprikarecipes": return "Paprika";
            case "yaml": case "yml":
                // Prefer ORF for files containing ORF-specific keys
                if (text.Contains("steps:") || text.Contains("oven_temp:") || text.Contains("notes_from_file:"))
                    return "OpenRecipeFormat";
                return "Yaml";
            case "json": return "Json";
            case "html": case "htm": return "GoogleStructuredData";
        }

        var trimmed = text.TrimStart();

        if (trimmed.StartsWith("MMMMM", StringComparison.Ordinal)) return "MealMaster";
        if (trimmed.StartsWith("-----", StringComparison.Ordinal) && text.Contains("MMMMM")) return "MealMaster";

        if (trimmed.Contains("\"@type\"") && (trimmed.Contains("\"Recipe\"") || trimmed.Contains("schema.org/Recipe")))
            return "GoogleStructuredData";
        if (trimmed.Contains("<script type=\"application/ld+json\">") || trimmed.Contains("<script type='application/ld+json'>"))
            return "GoogleStructuredData";

        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            return "Json";

        if (trimmed.StartsWith("<?xml") || trimmed.StartsWith("<"))
        {
            if (trimmed.Contains("<RecipeML") || trimmed.Contains("<recipeml")) return "RecipeML";
            if (trimmed.Contains("<mx2") || trimmed.Contains("<MX2")) return "MasterCook";
            if (trimmed.Contains("<fdxz") || trimmed.Contains("<FDX") || trimmed.Contains("Living Cookbook")) return "LivingCookbook";
            if (trimmed.Contains("<mgrx") || trimmed.Contains("<MacGourmet") || trimmed.Contains("MacGourmet")) return "MacGourmet";
            if (trimmed.Contains("<RXOL") || trimmed.Contains("<rxol")) return "Rxol";
            if (trimmed.Contains("<REML") || trimmed.Contains("<reml")) return "Reml";
            if (trimmed.Contains("<RecipeBook") || trimmed.Contains("<cookbook")) return "RecipeBookXml";
            if (trimmed.Contains("<BigOven") || trimmed.Contains("<Recipes>")) return "BigOvenXml";
            if (trimmed.Contains("<cooknrecipes") || trimmed.Contains("<CooknRecipes") || trimmed.Contains("Cook'n")) return "Cookn";
            if (trimmed.Contains("<YumRecipes") || trimmed.Contains("<yum")) return "Yum";
            if (trimmed.Contains("<HomeCookin") || trimmed.Contains("<homecookin")) return "HomeCookin";
            if (trimmed.Contains("<ChickenPing")) return "ChickenPing";
            return "GenericXml";
        }

        if (trimmed.StartsWith(">>") || (text.Contains("@") && !trimmed.StartsWith("<") && !trimmed.StartsWith("{")))
        {
            if (text.Contains("@") || text.Contains(">>")) return "CookLang";
        }

        if (trimmed.Contains(":") && !trimmed.StartsWith("<") && !trimmed.StartsWith("{"))
        {
            if (text.Contains("ingredients:") || text.Contains("instructions:") || text.Contains("title:") || text.Contains("name:"))
                return "Yaml";
        }

        if (text.Contains("* Exported from MasterCook") || text.Contains("RECIPE"))
            return "MasterCook";

        if (text.Contains("@@@@@") || text.StartsWith("@@@"))
            return "Connoisseur";

        if (text.Contains("RECIPE:") || (text.Contains("INGREDIENTS:") && text.Contains("DIRECTIONS:")))
            return "EatDrinkFeedGood";

        return null;
    }
}
