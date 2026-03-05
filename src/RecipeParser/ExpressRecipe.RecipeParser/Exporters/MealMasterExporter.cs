using System.Text;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class MealMasterExporter : IRecipeExporter
{
    public string FormatName => "MealMaster";
    public string DefaultFileExtension => "mmf";
    private const int ColWidth = 36;

    public string Export(ParsedRecipe recipe)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MMMMM----- Recipe via Meal-Master (tm) v8.05");
        sb.AppendLine($"     Title: {recipe.Title}");
        sb.AppendLine($"Categories: {recipe.Category ?? ""}");
        sb.AppendLine($"     Yield: {recipe.Yield ?? "1"} Servings");
        sb.AppendLine();

        // Two-column ingredient layout
        var ings = recipe.Ingredients.ToList();
        for (int i = 0; i < ings.Count; i += 2)
        {
            var col1 = FormatIngredient(ings[i]);
            if (i + 1 < ings.Count)
            {
                var col2 = FormatIngredient(ings[i + 1]);
                sb.AppendLine(col1.PadRight(ColWidth) + col2);
            }
            else
            {
                sb.AppendLine(col1);
            }
        }

        sb.AppendLine();
        sb.AppendLine("MMMMM----- Instructions");
        sb.AppendLine();
        foreach (var inst in recipe.Instructions)
            sb.AppendLine(inst.Text);

        sb.AppendLine();
        sb.AppendLine("MMMMM");
        return sb.ToString().TrimEnd();
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
        => string.Join("\n\n", recipes.Select(Export));

    private static string FormatIngredient(ParsedIngredient ing)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(ing.Quantity)) sb.Append(ing.Quantity.PadLeft(7));
        else sb.Append("       ");
        sb.Append(' ');
        if (!string.IsNullOrEmpty(ing.Unit)) sb.Append(ing.Unit.PadRight(2));
        else sb.Append("  ");
        sb.Append(' ');
        sb.Append(ing.Name);
        if (!string.IsNullOrEmpty(ing.Preparation)) sb.Append(", ").Append(ing.Preparation);
        return sb.ToString();
    }
}
