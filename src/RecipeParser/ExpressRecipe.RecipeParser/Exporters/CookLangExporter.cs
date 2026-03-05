using System.Text;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class CookLangExporter : IRecipeExporter
{
    public string FormatName => "CookLang";
    public string DefaultFileExtension => "cook";

    public string Export(ParsedRecipe recipe)
    {
        var sb = new StringBuilder();

        // Metadata
        sb.AppendLine($">> recipe: {recipe.Title}");
        if (!string.IsNullOrEmpty(recipe.Description)) sb.AppendLine($">> description: {recipe.Description}");
        if (!string.IsNullOrEmpty(recipe.Author)) sb.AppendLine($">> author: {recipe.Author}");
        if (!string.IsNullOrEmpty(recipe.PrepTime)) sb.AppendLine($">> prep_time: {recipe.PrepTime}");
        if (!string.IsNullOrEmpty(recipe.CookTime)) sb.AppendLine($">> cook_time: {recipe.CookTime}");
        if (!string.IsNullOrEmpty(recipe.Yield)) sb.AppendLine($">> servings: {recipe.Yield}");
        if (!string.IsNullOrEmpty(recipe.Cuisine)) sb.AppendLine($">> cuisine: {recipe.Cuisine}");
        if (!string.IsNullOrEmpty(recipe.Category)) sb.AppendLine($">> category: {recipe.Category}");
        if (recipe.Tags.Count > 0) sb.AppendLine($">> tags: {string.Join(", ", recipe.Tags)}");
        sb.AppendLine();

        // Write steps
        foreach (var inst in recipe.Instructions)
        {
            sb.AppendLine(inst.Text);
            sb.AppendLine();
        }

        // Mise en place for ingredients not referenced in steps
        if (recipe.Ingredients.Count > 0)
        {
            var stepTexts = string.Join(" ", recipe.Instructions.Select(i => i.Text));
            var unreferenced = recipe.Ingredients.Where(ing => !stepTexts.Contains(ing.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (unreferenced.Count > 0)
            {
                sb.AppendLine("== Mise en place ==");
                foreach (var ing in unreferenced)
                {
                    var qty = ing.Quantity ?? "";
                    var unit = ing.Unit ?? "";
                    var name = ing.Name.Replace(" ", "_");
                    sb.AppendLine($"@{name}{{{qty}{(string.IsNullOrEmpty(unit) ? "" : "%" + unit)}}}");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
        => string.Join("\n\n", recipes.Select(Export));
}
