using ExpressRecipe.Shared.DTOs.Recipe;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using System.Web;

namespace ExpressRecipe.RecipeService.Services;

public class RecipePrintService : IRecipePrintService
{
    static RecipePrintService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(RecipeDto recipe)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Element(header => ComposeHeader(header, recipe));
                page.Content().Element(content => ComposeContent(content, recipe));
                page.Footer().Element(footer => ComposeFooter(footer, recipe));
            });
        }).GeneratePdf();
    }

    public string GenerateHtml(RecipeDto recipe)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>{HttpUtility.HtmlEncode(recipe.Name)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(GetPrintCss());
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"recipe-print-container\">");

        // Header
        sb.AppendLine($"<h1 class=\"recipe-title\">{HttpUtility.HtmlEncode(recipe.Name)}</h1>");
        sb.AppendLine("<div class=\"recipe-meta\">");
        if (recipe.PrepTimeMinutes.HasValue)
        {
            sb.AppendLine($"<span class=\"meta-item\">Prep: {recipe.PrepTimeMinutes} min</span>");
        }
        if (recipe.CookTimeMinutes.HasValue)
        {
            sb.AppendLine($"<span class=\"meta-item\">Cook: {recipe.CookTimeMinutes} min</span>");
        }
        if (recipe.Servings.HasValue)
        {
            sb.AppendLine($"<span class=\"meta-item\">Serves: {recipe.Servings}</span>");
        }
        if (!string.IsNullOrWhiteSpace(recipe.DifficultyLevel))
        {
            sb.AppendLine($"<span class=\"meta-item\">Difficulty: {HttpUtility.HtmlEncode(recipe.DifficultyLevel)}</span>");
        }
        if (!string.IsNullOrWhiteSpace(recipe.Cuisine))
        {
            sb.AppendLine($"<span class=\"meta-item\">Cuisine: {HttpUtility.HtmlEncode(recipe.Cuisine)}</span>");
        }
        sb.AppendLine("</div>");

        // Allergen warnings
        if (recipe.AllergenWarnings != null && recipe.AllergenWarnings.Count > 0)
        {
            sb.AppendLine("<div class=\"allergen-warning\">");
            sb.AppendLine("<strong>⚠ Allergen Warnings:</strong> ");
            sb.AppendLine(string.Join(", ", recipe.AllergenWarnings.Select(a => HttpUtility.HtmlEncode(a.AllergenName))));
            sb.AppendLine("</div>");
        }

        // Two-column layout
        sb.AppendLine("<div class=\"recipe-columns\">");

        // Ingredients column
        sb.AppendLine("<div class=\"ingredients-col\">");
        sb.AppendLine("<h2>Ingredients</h2>");
        sb.AppendLine("<ul class=\"ingredient-list\">");
        if (recipe.Ingredients != null)
        {
            foreach (RecipeIngredientDto ingredient in recipe.Ingredients)
            {
                string qty = ingredient.Quantity.HasValue ? $"{ingredient.Quantity} " : "";
                string unit = !string.IsNullOrWhiteSpace(ingredient.Unit) ? $"{ingredient.Unit} " : "";
                string name = HttpUtility.HtmlEncode(ingredient.IngredientName ?? "");
                sb.AppendLine($"<li>{qty}{unit}{name}</li>");
            }
        }
        sb.AppendLine("</ul>");
        sb.AppendLine("</div>");

        // Instructions column
        sb.AppendLine("<div class=\"instructions-col\">");
        sb.AppendLine("<h2>Instructions</h2>");
        sb.AppendLine("<ol class=\"instruction-list\">");
        if (!string.IsNullOrWhiteSpace(recipe.Instructions))
        {
            string[] steps = recipe.Instructions.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (string step in steps)
            {
                string trimmed = step.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    sb.AppendLine($"<li>{HttpUtility.HtmlEncode(trimmed)}</li>");
                }
            }
        }
        sb.AppendLine("</ol>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>"); // recipe-columns

        // Nutrition summary
        if (recipe.Nutrition != null)
        {
            sb.AppendLine("<div class=\"nutrition-summary\">");
            sb.AppendLine("<h3>Nutrition per Serving</h3>");
            sb.AppendLine("<div class=\"nutrition-row\">");
            if (recipe.Nutrition.Calories.HasValue)
            {
                sb.AppendLine($"<span>Calories: {recipe.Nutrition.Calories}</span>");
            }
            if (recipe.Nutrition.Protein.HasValue)
            {
                sb.AppendLine($"<span>Protein: {recipe.Nutrition.Protein}g</span>");
            }
            if (recipe.Nutrition.TotalCarbohydrates.HasValue)
            {
                sb.AppendLine($"<span>Carbs: {recipe.Nutrition.TotalCarbohydrates}g</span>");
            }
            if (recipe.Nutrition.TotalFat.HasValue)
            {
                sb.AppendLine($"<span>Fat: {recipe.Nutrition.TotalFat}g</span>");
            }
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>"); // recipe-print-container
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // PDF helpers
    // -----------------------------------------------------------------------

    private static void ComposeHeader(IContainer container, RecipeDto recipe)
    {
        container.Column(col =>
        {
            col.Item().Text(recipe.Name).SemiBold().FontSize(20);
            col.Item().Row(row =>
            {
                if (recipe.PrepTimeMinutes.HasValue)
                {
                    row.AutoItem().Text($"Prep: {recipe.PrepTimeMinutes} min").FontSize(9);
                    row.ConstantItem(12).Text("|").FontSize(9);
                }
                if (recipe.CookTimeMinutes.HasValue)
                {
                    row.AutoItem().Text($"Cook: {recipe.CookTimeMinutes} min").FontSize(9);
                    row.ConstantItem(12).Text("|").FontSize(9);
                }
                if (recipe.Servings.HasValue)
                {
                    row.AutoItem().Text($"Serves: {recipe.Servings}").FontSize(9);
                    row.ConstantItem(12).Text("|").FontSize(9);
                }
                if (!string.IsNullOrWhiteSpace(recipe.DifficultyLevel))
                {
                    row.AutoItem().Text($"Difficulty: {recipe.DifficultyLevel}").FontSize(9);
                    row.ConstantItem(12).Text("|").FontSize(9);
                }
                if (!string.IsNullOrWhiteSpace(recipe.Cuisine))
                {
                    row.AutoItem().Text($"Cuisine: {recipe.Cuisine}").FontSize(9);
                }
            });
            col.Item().LineHorizontal(1);
        });
    }

    private static void ComposeContent(IContainer container, RecipeDto recipe)
    {
        container.Column(col =>
        {
            // Allergen warning box
            if (recipe.AllergenWarnings != null && recipe.AllergenWarnings.Count > 0)
            {
                col.Item().Padding(8).Border(1).BorderColor(Colors.Red.Darken1).Background(Colors.Red.Lighten5)
                    .Column(warn =>
                    {
                        warn.Item().Text("⚠ Allergen Warnings").Bold().FontColor(Colors.Red.Darken2);
                        warn.Item().Text(string.Join(", ",
                            recipe.AllergenWarnings.Select(a => a.AllergenName)))
                            .FontColor(Colors.Red.Darken2);
                    });
                col.Item().PaddingBottom(8);
            }

            // Two-column layout: ingredients + instructions
            col.Item().Row(row =>
            {
                // Ingredients
                row.RelativeItem().Column(ingCol =>
                {
                    ingCol.Item().Text("Ingredients").Bold().FontSize(12);
                    ingCol.Item().PaddingBottom(4);
                    if (recipe.Ingredients != null)
                    {
                        foreach (RecipeIngredientDto ingredient in recipe.Ingredients)
                        {
                            string qty = ingredient.Quantity.HasValue ? $"{ingredient.Quantity} " : "";
                            string unit = !string.IsNullOrWhiteSpace(ingredient.Unit) ? $"{ingredient.Unit} " : "";
                            string name = ingredient.IngredientName ?? "";
                            ingCol.Item().Text($"• {qty}{unit}{name}").FontSize(9);
                        }
                    }
                });

                row.ConstantItem(20);

                // Instructions
                row.RelativeItem().Column(instCol =>
                {
                    instCol.Item().Text("Instructions").Bold().FontSize(12);
                    instCol.Item().PaddingBottom(4);
                    if (!string.IsNullOrWhiteSpace(recipe.Instructions))
                    {
                        string[] steps = recipe.Instructions.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        int stepNum = 1;
                        foreach (string step in steps)
                        {
                            string trimmed = step.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmed))
                            {
                                instCol.Item().Row(r =>
                                {
                                    r.ConstantItem(20).Text(stepNum.ToString()).Bold().FontSize(9);
                                    r.RelativeItem().Text(trimmed).FontSize(9);
                                });
                                instCol.Item().PaddingBottom(4);
                                stepNum++;
                            }
                        }
                    }
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container, RecipeDto recipe)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1);
            col.Item().Row(row =>
            {
                if (recipe.Nutrition != null)
                {
                    row.RelativeItem().Text(BuildNutritionSummary(recipe.Nutrition)).FontSize(8);
                }
                row.AutoItem().Text(text =>
                {
                    text.Span("Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" / ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });
    }

    private static string BuildNutritionSummary(RecipeNutritionDto nutrition)
    {
        List<string> parts = new List<string>();
        if (nutrition.Calories.HasValue) { parts.Add($"Calories: {nutrition.Calories}"); }
        if (nutrition.Protein.HasValue) { parts.Add($"Protein: {nutrition.Protein}g"); }
        if (nutrition.TotalCarbohydrates.HasValue) { parts.Add($"Carbs: {nutrition.TotalCarbohydrates}g"); }
        if (nutrition.TotalFat.HasValue) { parts.Add($"Fat: {nutrition.TotalFat}g"); }
        return string.Join("  |  ", parts);
    }

    private static string GetPrintCss() => @"
        body { font-family: Arial, sans-serif; margin: 0; padding: 0; color: #333; }
        .recipe-print-container { max-width: 800px; margin: 0 auto; padding: 20px; }
        .recipe-title { font-size: 24px; margin-bottom: 8px; }
        .recipe-meta { display: flex; gap: 16px; flex-wrap: wrap; margin-bottom: 12px; color: #555; font-size: 13px; }
        .meta-item { }
        .allergen-warning { border: 2px solid #c00; background: #fff5f5; color: #c00; padding: 8px 12px; border-radius: 4px; margin-bottom: 12px; }
        .recipe-columns { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 16px; }
        .ingredients-col h2, .instructions-col h2 { font-size: 16px; border-bottom: 1px solid #ccc; padding-bottom: 4px; margin-bottom: 8px; }
        .ingredient-list { padding-left: 20px; line-height: 1.7; }
        .instruction-list { padding-left: 20px; line-height: 1.7; }
        .nutrition-summary { border-top: 1px solid #ccc; padding-top: 8px; font-size: 12px; color: #555; }
        .nutrition-row { display: flex; gap: 20px; }
        @media print {
            nav, .sidebar, header, footer { display: none !important; }
            .recipe-print-container { max-width: 100%; padding: 0; }
            body { font-size: 11pt; }
            .recipe-columns { grid-template-columns: 1fr 1fr; }
        }";
}
