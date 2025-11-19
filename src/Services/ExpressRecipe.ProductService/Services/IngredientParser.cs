using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using System.Text.RegularExpressions;

namespace ExpressRecipe.ProductService.Services;

public interface IIngredientParser
{
    Task<ParsedIngredientResult> ParseIngredientStringAsync(string ingredientString);
    Task<List<Guid>> MatchAndLinkBaseIngredientsAsync(Guid ingredientId, string ingredientString, Guid createdBy);
}

public class IngredientParser : IIngredientParser
{
    private readonly IBaseIngredientRepository _baseIngredientRepository;
    private readonly ILogger<IngredientParser> _logger;

    public IngredientParser(
        IBaseIngredientRepository baseIngredientRepository,
        ILogger<IngredientParser> logger)
    {
        _baseIngredientRepository = baseIngredientRepository;
        _logger = logger;
    }

    /// <summary>
    /// Parse ingredient string into structured components
    /// Example: "Enriched Wheat Flour (Wheat Flour, Niacin, Iron), Sugar, Salt"
    /// </summary>
    public async Task<ParsedIngredientResult> ParseIngredientStringAsync(string ingredientString)
    {
        if (string.IsNullOrWhiteSpace(ingredientString))
        {
            return new ParsedIngredientResult { OriginalString = ingredientString ?? string.Empty };
        }

        var result = new ParsedIngredientResult
        {
            OriginalString = ingredientString
        };

        // Split by commas at the top level (not inside parentheses)
        var topLevelComponents = SplitRespectingParentheses(ingredientString, ',');

        for (int i = 0; i < topLevelComponents.Count; i++)
        {
            var component = ParseComponent(topLevelComponents[i].Trim(), i);
            if (component != null)
            {
                result.Components.Add(component);
            }
        }

        // Try to match components to base ingredients
        await MatchComponentsToBaseIngredientsAsync(result.Components);

        return result;
    }

    /// <summary>
    /// Parse a component and match it to base ingredients, then create links
    /// Returns list of base ingredient IDs that were linked
    /// </summary>
    public async Task<List<Guid>> MatchAndLinkBaseIngredientsAsync(Guid ingredientId, string ingredientString, Guid createdBy)
    {
        var parsedResult = await ParseIngredientStringAsync(ingredientString);
        var linkedIds = new List<Guid>();

        foreach (var component in parsedResult.Components)
        {
            if (component.BaseIngredientId.HasValue)
            {
                try
                {
                    await _baseIngredientRepository.AddIngredientBaseComponentAsync(
                        ingredientId,
                        new AddIngredientBaseComponentRequest
                        {
                            BaseIngredientId = component.BaseIngredientId.Value,
                            OrderIndex = component.OrderIndex,
                            IsMainComponent = component.OrderIndex == 0, // First ingredient is usually main component
                            Notes = component.SubComponents?.Any() == true
                                ? $"Contains: {string.Join(", ", component.SubComponents.Select(s => s.Name))}"
                                : null
                        },
                        createdBy);

                    linkedIds.Add(component.BaseIngredientId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to link base ingredient {BaseIngredientId} to ingredient {IngredientId}",
                        component.BaseIngredientId.Value, ingredientId);
                }
            }

            // Also link sub-components
            if (component.SubComponents != null)
            {
                foreach (var subComponent in component.SubComponents)
                {
                    if (subComponent.BaseIngredientId.HasValue && !linkedIds.Contains(subComponent.BaseIngredientId.Value))
                    {
                        try
                        {
                            await _baseIngredientRepository.AddIngredientBaseComponentAsync(
                                ingredientId,
                                new AddIngredientBaseComponentRequest
                                {
                                    BaseIngredientId = subComponent.BaseIngredientId.Value,
                                    OrderIndex = subComponent.OrderIndex + 1000, // Offset sub-components
                                    IsMainComponent = false,
                                    Notes = $"Sub-component of {component.Name}"
                                },
                                createdBy);

                            linkedIds.Add(subComponent.BaseIngredientId.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to link sub-component base ingredient {BaseIngredientId}",
                                subComponent.BaseIngredientId.Value);
                        }
                    }
                }
            }
        }

        return linkedIds;
    }

    private ParsedIngredientComponent? ParseComponent(string componentString, int orderIndex)
    {
        if (string.IsNullOrWhiteSpace(componentString))
        {
            return null;
        }

        var component = new ParsedIngredientComponent
        {
            OrderIndex = orderIndex
        };

        // Check if component has parenthetical sub-components
        // Example: "Enriched Wheat Flour (Wheat Flour, Niacin, Iron)"
        var match = Regex.Match(componentString, @"^(.+?)\s*\((.+)\)$");

        if (match.Success)
        {
            component.Name = CleanIngredientName(match.Groups[1].Value);
            component.IsParenthetical = false;

            // Parse sub-components
            var subComponentsString = match.Groups[2].Value;
            var subComponentParts = SplitRespectingParentheses(subComponentsString, ',');

            component.SubComponents = new List<ParsedIngredientComponent>();
            for (int i = 0; i < subComponentParts.Count; i++)
            {
                var subComponent = new ParsedIngredientComponent
                {
                    Name = CleanIngredientName(subComponentParts[i].Trim()),
                    OrderIndex = i,
                    IsParenthetical = true
                };
                component.SubComponents.Add(subComponent);
            }
        }
        else
        {
            component.Name = CleanIngredientName(componentString);
        }

        return component;
    }

    private async Task MatchComponentsToBaseIngredientsAsync(List<ParsedIngredientComponent> components)
    {
        foreach (var component in components)
        {
            // Try exact match first
            var baseIngredient = await _baseIngredientRepository.FindByNameAsync(component.Name);

            if (baseIngredient != null)
            {
                component.BaseIngredientId = baseIngredient.Id;
                component.MatchedName = baseIngredient.Name;
            }
            else
            {
                // Try fuzzy match by searching common names
                var searchResults = await _baseIngredientRepository.SearchAsync(new BaseIngredientSearchRequest
                {
                    SearchTerm = component.Name,
                    PageSize = 1,
                    OnlyApproved = true
                });

                if (searchResults.Any())
                {
                    var match = searchResults.First();
                    component.BaseIngredientId = match.Id;
                    component.MatchedName = match.Name;
                }
            }

            // Match sub-components
            if (component.SubComponents != null)
            {
                await MatchComponentsToBaseIngredientsAsync(component.SubComponents);
            }
        }
    }

    private string CleanIngredientName(string name)
    {
        // Remove common prefixes/suffixes and clean up
        name = name.Trim();

        // Remove trailing qualifiers like "and/or", "less than 2% of"
        name = Regex.Replace(name, @"\s+(and/or|or)\s*$", "", RegexOptions.IgnoreCase);

        // Remove percentage indicators
        name = Regex.Replace(name, @"\s*\([<>]?\s*\d+%\s*\)", "");

        // Remove asterisks and other markers
        name = name.Replace("*", "").Replace("†", "").Replace("‡", "");

        return name.Trim();
    }

    private List<string> SplitRespectingParentheses(string input, char delimiter)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        int parenthesesDepth = 0;

        foreach (char c in input)
        {
            if (c == '(')
            {
                parenthesesDepth++;
                current.Append(c);
            }
            else if (c == ')')
            {
                parenthesesDepth--;
                current.Append(c);
            }
            else if (c == delimiter && parenthesesDepth == 0)
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
}
