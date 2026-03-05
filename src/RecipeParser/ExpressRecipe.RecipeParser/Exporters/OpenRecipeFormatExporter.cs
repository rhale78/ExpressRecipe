using System.Text;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class OpenRecipeFormatExporter : IRecipeExporter
{
    public string FormatName => "OpenRecipeFormat";
    public string DefaultFileExtension => "yaml";

    public string Export(ParsedRecipe recipe)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"name: {EscapeYaml(recipe.Title)}");
        if (!string.IsNullOrEmpty(recipe.Description)) sb.AppendLine($"description: {EscapeYaml(recipe.Description)}");
        if (!string.IsNullOrEmpty(recipe.Author)) sb.AppendLine($"author: {EscapeYaml(recipe.Author)}");
        if (!string.IsNullOrEmpty(recipe.Source)) sb.AppendLine($"source_url: {EscapeYaml(recipe.Source)}");
        if (!string.IsNullOrEmpty(recipe.Yield)) sb.AppendLine($"servings: {EscapeYaml(recipe.Yield)}");
        if (!string.IsNullOrEmpty(recipe.PrepTime)) sb.AppendLine($"prep_time: {EscapeYaml(recipe.PrepTime)}");
        if (!string.IsNullOrEmpty(recipe.CookTime)) sb.AppendLine($"cook_time: {EscapeYaml(recipe.CookTime)}");
        if (!string.IsNullOrEmpty(recipe.TotalTime)) sb.AppendLine($"total_time: {EscapeYaml(recipe.TotalTime)}");
        if (!string.IsNullOrEmpty(recipe.Category)) sb.AppendLine($"categories: {EscapeYaml(recipe.Category)}");
        if (!string.IsNullOrEmpty(recipe.Cuisine)) sb.AppendLine($"cuisine: {EscapeYaml(recipe.Cuisine)}");

        if (recipe.Tags.Count > 0)
        {
            sb.AppendLine("tags:");
            foreach (var tag in recipe.Tags) sb.AppendLine($"  - {EscapeYaml(tag)}");
        }

        if (recipe.Ingredients.Count > 0)
        {
            sb.AppendLine("ingredients:");
            foreach (var ing in recipe.Ingredients)
            {
                sb.AppendLine($"  - name: {EscapeYaml(ing.Name)}");
                if (!string.IsNullOrEmpty(ing.Quantity)) sb.AppendLine($"    amount: {EscapeYaml(ing.Quantity)}");
                if (!string.IsNullOrEmpty(ing.Unit)) sb.AppendLine($"    unit: {EscapeYaml(ing.Unit)}");
                if (!string.IsNullOrEmpty(ing.Preparation)) sb.AppendLine($"    notes: {EscapeYaml(ing.Preparation)}");
            }
        }

        if (recipe.Instructions.Count > 0)
        {
            sb.AppendLine("steps:");
            foreach (var inst in recipe.Instructions)
                sb.AppendLine($"  - {EscapeYaml(inst.Text)}");
        }

        return sb.ToString().TrimEnd();
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
        => string.Join("\n---\n", recipes.Select(Export));

    private static string EscapeYaml(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        bool needsQuote = value.Contains(':') || value.Contains('#') || value.Contains('\'') ||
                          value.Contains('\n') || value.StartsWith(' ') || value.EndsWith(' ') ||
                          value.StartsWith('"');
        if (needsQuote)
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
        return value;
    }
}
