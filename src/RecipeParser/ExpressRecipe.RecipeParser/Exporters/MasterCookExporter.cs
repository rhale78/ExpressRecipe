using System.Text;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class MasterCookExporter : IRecipeExporter
{
    public string FormatName => "MasterCook";
    public string DefaultFileExtension => "mxp";

    public string Export(ParsedRecipe recipe)
    {
        var sb = new StringBuilder();
        sb.AppendLine("* Exported from MasterCook *");
        sb.AppendLine();
        sb.AppendLine(recipe.Title);
        sb.AppendLine();
        sb.AppendLine($"Recipe By     : {recipe.Author ?? ""}");
        sb.AppendLine($"Serving Size  : {recipe.Yield ?? "1"}");
        sb.AppendLine($"Preparation Time: {recipe.PrepTime ?? ""}");
        sb.AppendLine($"Categories    : {recipe.Category ?? ""}");
        sb.AppendLine();
        sb.AppendLine("  Amount  Measure       Ingredient -- Preparation Method");
        sb.AppendLine("--------  ------------  --------------------------------");

        foreach (var ing in recipe.Ingredients)
        {
            var qty = (ing.Quantity ?? "").PadLeft(8);
            var unit = (ing.Unit ?? "").PadRight(12);
            var prep = string.IsNullOrEmpty(ing.Preparation) ? "" : $" -- {ing.Preparation}";
            sb.AppendLine($"{qty}  {unit}  {ing.Name}{prep}");
        }

        sb.AppendLine();
        sb.AppendLine("  Method:");
        sb.AppendLine();
        foreach (var inst in recipe.Instructions)
            sb.AppendLine(inst.Text);

        sb.AppendLine();
        sb.AppendLine("- - - - - - - - - - - - - - - - - - ");
        return sb.ToString().TrimEnd();
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
        => string.Join("\n\n", recipes.Select(Export));
}
