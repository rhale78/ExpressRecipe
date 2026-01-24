using System.Text.RegularExpressions;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Detects if product text (name, ingredients) is in English or another language.
/// Used to filter out non-English products during import from international databases.
/// </summary>
public class LanguageDetector
{
    // Common non-English words that are strong indicators
    private static readonly HashSet<string> NonEnglishIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        // French
        "pour", "avec", "sans", "dans", "contient", "peuvent", "peut", "issus", "issu",
        "proviennent", "provient", "voir", "avant", "couvercle", "agricoles", "agriculture",
        "biologique", "naturelle", "origine", "fabriqué", "produit", "ingrédients",
        "poudre", "farine", "sucre", "sel", "huile", "grasse", "graisse", "beurre",
        "amidon", "correcteur", "acidité", "émulsifiant", "colorant", "arôme",
        "conservateur", "épaississant", "levure", "édulcorant", "gélifiants",

        // German
        "für", "mit", "ohne", "enthält", "zutaten", "mehl", "zucker", "salz",
        "fett", "butter", "stärke", "milch", "käse", "säuerungsmittel",
        "emulgator", "farbstoff", "aroma", "konservierungsstoff", "süßungsmittel",
        "backtriebmittel", "antioxidationsmittel", "geeignet", "vermeiden",

        // Spanish
        "para", "con", "sin", "contiene", "ingredientes", "harina", "azúcar",
        "sal", "aceite", "grasa", "mantequilla", "almidón", "leche", "queso",
        "conservador", "emulsionante", "colorante", "saborizante", "alérgico",

        // Italian
        "per", "con", "senza", "contiene", "ingredienti", "farina", "zucchero",
        "sale", "olio", "grasso", "burro", "amido", "latte", "formaggio",
        "conservante", "emulsionante", "colorante", "aroma", "prodotto",

        // Portuguese
        "para", "com", "sem", "contém", "ingredientes", "farinha", "açúcar",
        "sal", "óleo", "gordura", "manteiga", "amido", "leite", "queijo",

        // Dutch
        "voor", "met", "zonder", "bevat", "ingrediënten", "meel", "suiker",
        "zout", "olie", "vet", "boter", "zetmeel", "melk", "kaas",

        // Norwegian/Swedish/Danish
        "med", "uten", "inneholder", "ingredienser", "mel", "sukker", "salt",
        "olje", "fett", "smør", "stivelse", "melk", "ost", "aroma", "vann",
        "konserveringsmiddel", "emulgator", "fargestoff", "surhetsregulerende",

        // Romanian
        "pentru", "fără", "conține", "ingrediente", "făină", "zahăr", "sare",
        "ulei", "grăsime", "unt", "amidon", "lapte", "brânză", "produsator",

        // Polish
        "dla", "bez", "zawiera", "składniki", "mąka", "cukier", "sól",
        "olej", "tłuszcz", "masło", "skrobia", "mleko", "ser"
    };

    // Non-Latin scripts are always non-English
    private static readonly Regex NonLatinScript = new Regex(
        @"[\u0400-\u04FF\u0370-\u03FF\u4E00-\u9FFF\u3040-\u309F\u30A0-\u30FF\u0E00-\u0E7F]",
        RegexOptions.Compiled);

    // Pre-compiled regex patterns
    private static readonly Regex[] CompiledNonEnglishPatterns = new[]
    {
        // Common foreign phrases
        new Regex(@"\b(pour|avec|sans|contient|peut|peuvent)\s+\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\b(für|mit|ohne|enthält|zutaten)\s+\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\b(para|con|sin|contiene|ingredientes)\s+\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\b(per|con|senza|contiene|ingredienti)\s+\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\b(voor|met|zonder|bevat|ingrediënten)\s+\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Accented characters clusters (strong indicator)
        new Regex(@"[àáâãäåæçèéêëìíîïñòóôõöøùúûüýÿ]{3,}", RegexOptions.Compiled),

        // Common foreign endings
        new Regex(@"\w+(ción|sión|tés|dés|ità|età|heid|lijk|schap)$", RegexOptions.Compiled),

        // Foreign measurements/codes
        new Regex(@"\b(kcal|kca)\b", RegexOptions.Compiled),
        new Regex(@"\be\s*\d{3}\b", RegexOptions.Compiled), // E-numbers with spaces (foreign style)

        // Multi-language indicators
        new Regex(@"nl\s+\w+|de\s+\w+|fr\s+\w+|it\s+\w+|es\s+\w+", RegexOptions.Compiled),

        // Foreign country codes
        new Regex(@"\b(luxembourg|republica\s+moldova|danmark|sverige|norge|polska)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    /// <summary>
    /// Checks if the given text is likely in English.
    /// Returns true if English, false if another language is detected.
    /// </summary>
    public static bool IsEnglish(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true; // Assume English for empty text

        var lowerText = text.ToLowerInvariant();

        // Check for non-Latin scripts (Cyrillic, Greek, CJK, Thai, etc.)
        if (NonLatinScript.IsMatch(text))
            return false;

        // Count non-English word indicators
        int nonEnglishWordCount = 0;
        var words = lowerText.Split(new[] { ' ', ',', '.', ';', ':', '(', ')', '[', ']' },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (NonEnglishIndicators.Contains(word))
            {
                nonEnglishWordCount++;
            }
        }

        // If more than 15% of words are non-English indicators, it's probably not English
        if (words.Length > 0 && ((double)nonEnglishWordCount / words.Length) > 0.15)
            return false;

        // Check for non-English patterns
        foreach (var pattern in CompiledNonEnglishPatterns)
        {
            if (pattern.IsMatch(lowerText))
                return false;
        }

        // Check for excessive accented characters
        int accentedCharCount = text.Count(c => "àáâãäåæçèéêëìíîïñòóôõöøùúûüýÿÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÑÒÓÔÕÖØÙÚÛÜÝŸ".Contains(c));
        if (text.Length > 20 && ((double)accentedCharCount / text.Length) > 0.1)
            return false;

        return true;
    }

    /// <summary>
    /// Checks if a product should be imported based on language detection.
    /// Analyzes product name, brand, and ingredients.
    /// </summary>
    public static bool ShouldImportProduct(string productName, string brand, string ingredients)
    {
        // Check product name
        if (!string.IsNullOrWhiteSpace(productName) && !IsEnglish(productName))
            return false;

        // Check brand (less strict, brands can have foreign names)
        // Skip brand check for now

        // Check ingredients (most important)
        if (!string.IsNullOrWhiteSpace(ingredients) && !IsEnglish(ingredients))
            return false;

        return true;
    }

    /// <summary>
    /// Gets a confidence score (0-100) that the text is in English.
    /// Higher score = more confident it's English.
    /// </summary>
    public static int GetEnglishConfidence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 100;

        var lowerText = text.ToLowerInvariant();
        int score = 100;

        // Non-Latin script = 0 confidence
        if (NonLatinScript.IsMatch(text))
            return 0;

        // Count non-English indicators
        var words = lowerText.Split(new[] { ' ', ',', '.', ';', ':', '(', ')', '[', ']' },
            StringSplitOptions.RemoveEmptyEntries);
        int nonEnglishWordCount = words.Count(w => NonEnglishIndicators.Contains(w));

        if (words.Length > 0)
        {
            double nonEnglishRatio = (double)nonEnglishWordCount / words.Length;
            score -= (int)(nonEnglishRatio * 100);
        }

        // Penalty for accented characters
        int accentedCharCount = text.Count(c => "àáâãäåæçèéêëìíîïñòóôõöøùúûüýÿ".Contains(c));
        if (text.Length > 0)
        {
            double accentRatio = (double)accentedCharCount / text.Length;
            score -= (int)(accentRatio * 50);
        }

        // Penalty for non-English patterns
        int patternMatches = CompiledNonEnglishPatterns.Count(p => p.IsMatch(lowerText));
        score -= patternMatches * 10;

        return Math.Max(0, score);
    }
}
