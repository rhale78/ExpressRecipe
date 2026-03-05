using System.Text;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class YamlRecipeExporter : IRecipeExporter
{
    public string FormatName => "YAML";
    public string DefaultFileExtension => "yaml";

    public string Export(ParsedRecipe recipe)
    {
        var sb = new StringBuilder();
        WriteField(sb, "name", recipe.Title);
        if (!string.IsNullOrEmpty(recipe.Description)) WriteField(sb, "description", recipe.Description);
        if (!string.IsNullOrEmpty(recipe.Author)) WriteField(sb, "author", recipe.Author);
        if (!string.IsNullOrEmpty(recipe.PrepTime)) WriteField(sb, "prep_time", recipe.PrepTime);
        if (!string.IsNullOrEmpty(recipe.CookTime)) WriteField(sb, "cook_time", recipe.CookTime);
        if (!string.IsNullOrEmpty(recipe.TotalTime)) WriteField(sb, "total_time", recipe.TotalTime);
        if (!string.IsNullOrEmpty(recipe.Yield)) WriteField(sb, "servings", recipe.Yield);
        if (!string.IsNullOrEmpty(recipe.Category)) WriteField(sb, "categories", recipe.Category);
        if (!string.IsNullOrEmpty(recipe.Cuisine)) WriteField(sb, "cuisine", recipe.Cuisine);
        if (!string.IsNullOrEmpty(recipe.Url)) WriteField(sb, "source_url", recipe.Url);

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
                sb.AppendLine("  - name: " + EscapeYaml(ing.Name));
                if (!string.IsNullOrEmpty(ing.Quantity)) sb.AppendLine("    amount: " + EscapeYaml(ing.Quantity));
                if (!string.IsNullOrEmpty(ing.Unit)) sb.AppendLine("    unit: " + EscapeYaml(ing.Unit));
                if (!string.IsNullOrEmpty(ing.Preparation)) sb.AppendLine("    preparation: " + EscapeYaml(ing.Preparation));
                if (ing.IsOptional) sb.AppendLine("    optional: true");
                if (!string.IsNullOrEmpty(ing.GroupHeading)) sb.AppendLine("    group: " + EscapeYaml(ing.GroupHeading));
            }
        }

        if (recipe.Instructions.Count > 0)
        {
            sb.AppendLine("steps:");
            foreach (var inst in recipe.Instructions)
                sb.AppendLine("  - " + EscapeYaml(inst.Text));
        }

        return sb.ToString().TrimEnd();
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
        => string.Join("\n---\n", recipes.Select(Export));

    private static void WriteField(StringBuilder sb, string key, string value)
        => sb.AppendLine($"{key}: {EscapeYaml(value)}");

    private static string EscapeYaml(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        // Quote if contains special chars
        bool needsQuote = value.Contains(':') || value.Contains('#') || value.Contains('\'') ||
                          value.Contains('\n') || value.StartsWith(' ') || value.EndsWith(' ') ||
                          value.StartsWith('"');
        if (needsQuote)
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
        return value;
    }
}
