using System.Text.Json;
using ExpressRecipe.AIService.Data;
using ExpressRecipe.AIService.Providers;

namespace ExpressRecipe.AIService.Services;

public sealed class CookingAssistantService : ICookingAssistantService
{
    private readonly IAIProviderFactory _aiFactory;
    private readonly IGroundingRepository _grounding;
    private readonly ILogger<CookingAssistantService> _logger;

    public CookingAssistantService(IAIProviderFactory aiFactory,
        IGroundingRepository grounding, ILogger<CookingAssistantService> logger)
    {
        _aiFactory = aiFactory;
        _grounding = grounding;
        _logger = logger;
    }

    public async Task<CookingAssistantResponse> TroubleshootProblemAsync(
        CookingAssistantRequest req, CancellationToken ct = default)
    {
        List<CookingTechniqueIssueDto> issues =
            await _grounding.FindMatchingIssuesAsync(req.UserMessage, maxResults: 4, ct);

        string groundingBlock = issues.Count > 0
            ? "Known cooking issues to reference:\n" + string.Join("\n",
                issues.Select(i => $"- {i.IssueName}: Cause: {i.Cause} | Fix: {i.Fix}"))
            : string.Empty;

        string recipeContext = req.RecipeName is not null ? $"Recipe: {req.RecipeName}" : string.Empty;
        string ingredientsContext = req.RecipeIngredients is not null
            ? $"Ingredients used: {req.RecipeIngredients}" : string.Empty;

        string prompt = $$"""
            You are an expert cooking troubleshooter. The user had a problem while cooking.
            
            {{recipeContext}}
            {{ingredientsContext}}
            Problem described: "{{req.UserMessage}}"
            
            {{groundingBlock}}
            
            If this problem was caused by a fixable technique mistake, include a "cookingNote" field
            with a concise tip the user should save for next time.
            
            Respond ONLY with valid JSON:
            {
              "suggestion": "What most likely happened and what to do about it",
              "explanation": "Why this happens (brief science/technique explanation)",
              "relatedTips": ["tip 1", "tip 2"],
              "cookingNote": {
                "noteType": "Warning",
                "noteText": "Short actionable reminder for next time",
                "saveNote": true
              }
            }
            If no cooking note is needed, omit the cookingNote field entirely.
            """;

        return await CallAndParseAsync("recipe-troubleshoot", prompt, ct);
    }

    public async Task<CookingAssistantResponse> GetPairingsAsync(
        CookingAssistantRequest req, CancellationToken ct = default)
    {
        List<IngredientPairingDto> seeds =
            await _grounding.FindMatchingPairingsAsync(
                req.RecipeName ?? req.UserMessage, maxResults: 10, ct);

        string pairingContext = seeds.Count > 0
            ? "Pairing database entries for this dish:\n" + string.Join("\n",
                seeds.Select(p => $"- [{p.PairingType}] {p.Suggestion}" +
                    (p.Notes is not null ? $" ({p.Notes})" : "")))
            : string.Empty;

        string allergenBlock = req.HouseholdAllergens.Count > 0
            ? $"MUST avoid these allergens in suggestions: {string.Join(", ", req.HouseholdAllergens)}"
            : string.Empty;

        string dishContext = req.RecipeName ?? req.UserMessage;
        string ingredientsContext = req.RecipeIngredients is not null
            ? $"Ingredients: {req.RecipeIngredients}" : string.Empty;

        string prompt = $$"""
            You are a professional food pairing expert. Suggest what would go well with this dish.
            Cover ALL categories: vegetables/sides, starches, breads, sauces, wines, beers,
            non-alcoholic drinks, and optionally a starter or dessert pairing.
            
            Dish: {{dishContext}}
            {{ingredientsContext}}
            {{allergenBlock}}
            {{pairingContext}}
            
            Respond ONLY with valid JSON:
            {
              "suggestion": "Brief overview of what pairs best and why",
              "explanation": "The flavor profile and why these pairings complement it",
              "relatedTips": [
                "Vegetables: ...",
                "Starches: ...",
                "Breads: ...",
                "Sauces: ...",
                "Wine: ...",
                "Beer: ...",
                "Non-alcoholic: ...",
                "Occasion: casual weeknight or special occasion notes"
              ]
            }
            """;

        return await CallAndParseAsync("recipe-pairings", prompt, ct);
    }

    public async Task<CookingAssistantResponse> GetVariationsAsync(
        CookingAssistantRequest req, CancellationToken ct = default)
    {
        string dietBlock = req.HouseholdDietaryRestrictions.Count > 0
            ? $"Household dietary restrictions (apply to variations): " +
              string.Join(", ", req.HouseholdDietaryRestrictions)
            : string.Empty;
        string allergenBlock = req.HouseholdAllergens.Count > 0
            ? $"MUST avoid allergens: {string.Join(", ", req.HouseholdAllergens)}"
            : string.Empty;

        string recipeContext = req.RecipeName ?? req.UserMessage;
        string ingredientsContext = req.RecipeIngredients is not null
            ? $"Base ingredients: {req.RecipeIngredients}" : string.Empty;

        string prompt = $$"""
            You are a creative chef. Provide 3-4 distinct variations of this recipe.
            Each variation should have a different angle: flavor profile, cuisine twist,
            dietary adaptation, or technique change.
            
            Recipe: {{recipeContext}}
            {{ingredientsContext}}
            {{dietBlock}}
            {{allergenBlock}}
            User request: "{{req.UserMessage}}"
            
            Respond ONLY with valid JSON:
            {
              "suggestion": "Overview of the variation ideas",
              "explanation": "How each variation changes the character of the dish",
              "relatedTips": [
                "Variation 1: [Name] - [what changes and why it works]",
                "Variation 2: [Name] - ...",
                "Variation 3: [Name] - ...",
                "Variation 4: [Name] - ..."
              ]
            }
            """;

        return await CallAndParseAsync("recipe-variations", prompt, ct);
    }

    public async Task<CookingAssistantResponse> AdaptRecipeAsync(
        CookingAssistantRequest req, string targetMethod, CancellationToken ct = default)
    {
        string recipeContext = req.RecipeName ?? req.UserMessage;
        string ingredientsContext = req.RecipeIngredients is not null
            ? $"Ingredients: {req.RecipeIngredients}" : string.Empty;

        string prompt = $$"""
            You are a recipe adaptation expert. Adapt this recipe for a {{targetMethod}}.
            
            Original recipe: {{recipeContext}}
            {{ingredientsContext}}
            
            For a {{targetMethod}} adaptation you MUST address:
            1. New cooking temperature/heat settings (if applicable)
            2. New timing - {{targetMethod}} usually requires very different times
            3. Order of operations changes (e.g., sear first separately for crockpot)
            4. Liquid adjustments (crockpot needs less liquid; pressure cooker different)
            5. Ingredient preparation changes
            6. Any steps to do BEFORE or AFTER using the {{targetMethod}}
            
            Respond ONLY with valid JSON:
            {
              "suggestion": "Summary of how to make this work in a {{targetMethod}}",
              "explanation": "Why these changes are needed for {{targetMethod}} cooking",
              "relatedTips": [
                "Step 1: ...",
                "Step 2: ...",
                "Timing: ...",
                "Key difference: ..."
              ]
            }
            """;

        return await CallAndParseAsync("recipe-adapt", prompt, ct);
    }

    public async Task<CookingAssistantResponse> AskSomethingSeemsBrokenAsync(
        CookingAssistantRequest req, CancellationToken ct = default)
        => await TroubleshootProblemAsync(req, ct);

    public async Task<CookingAssistantResponse> FixIssueAsync(
        CookingAssistantRequest req, CancellationToken ct = default)
        => await TroubleshootProblemAsync(req, ct);

    private async Task<CookingAssistantResponse> CallAndParseAsync(
        string useCase, string prompt, CancellationToken ct)
    {
        IAIProvider provider = await _aiFactory.GetProviderForUseCaseAsync(useCase, ct);
        AITextResult result = await provider.GenerateAsync(prompt,
            new AIRequestOptions { MaxTokens = 600, Temperature = 0.4m }, ct);

        if (!result.Success)
        {
            return new CookingAssistantResponse { Success = false, ErrorMessage = result.ErrorMessage };
        }

        return ParseAssistantResponse(result.Text);
    }

    private CookingAssistantResponse ParseAssistantResponse(string json)
    {
        try
        {
            string clean = json.Replace("```json", "").Replace("```", "").Trim();
            using JsonDocument doc = JsonDocument.Parse(clean);
            JsonElement root = doc.RootElement;

            List<string> tips = new();
            if (root.TryGetProperty("relatedTips", out JsonElement tipsEl))
            {
                foreach (JsonElement tip in tipsEl.EnumerateArray())
                {
                    string? tipText = tip.GetString();
                    if (!string.IsNullOrWhiteSpace(tipText)) { tips.Add(tipText); }
                }
            }

            CookingNoteToSave? noteToSave = null;
            if (root.TryGetProperty("cookingNote", out JsonElement noteEl))
            {
                noteToSave = new CookingNoteToSave
                {
                    NoteType = noteEl.TryGetProperty("noteType", out JsonElement nt)
                        ? nt.GetString() ?? "Tip" : "Tip",
                    NoteText = noteEl.TryGetProperty("noteText", out JsonElement nt2)
                        ? nt2.GetString() ?? string.Empty : string.Empty,
                    SaveNote = noteEl.TryGetProperty("saveNote", out JsonElement sv)
                        && sv.GetBoolean()
                };
            }

            return new CookingAssistantResponse
            {
                Success           = true,
                Suggestion        = root.TryGetProperty("suggestion", out JsonElement s)
                    ? s.GetString() ?? string.Empty : string.Empty,
                Explanation       = root.TryGetProperty("explanation", out JsonElement e)
                    ? e.GetString() ?? string.Empty : string.Empty,
                RelatedTips       = tips,
                CookingNoteToSave = noteToSave
            };
        }
        catch (JsonException ex)
        {
            // AI returned valid text but not valid JSON — surface the raw text
            _logger.LogWarning(ex, "AI response could not be parsed as JSON; surfacing raw text");
            return new CookingAssistantResponse
            {
                Success     = true,
                Suggestion  = json.Trim(),
                Explanation = string.Empty,
                RelatedTips = new List<string>()
            };
        }
    }
}
