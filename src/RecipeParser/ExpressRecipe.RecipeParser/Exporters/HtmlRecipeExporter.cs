using System.Text;
using System.Text.RegularExpressions;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

/// <summary>
/// Exports recipes to self-contained HTML5 pages with embedded CSS.
/// No external dependencies - pages work fully offline.
/// </summary>
public sealed class HtmlRecipeExporter : IRecipeExporter
{
    public string FormatName => "HTML";
    public string DefaultFileExtension => "html";

    public string Export(ParsedRecipe recipe)
    {
        var data = new RecipeExportData { Recipe = recipe };
        return ExportRecipePage(data);
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
    {
        var entries = recipes.Select((r, i) =>
            new RecipeIndexEntry
            {
                Title = r.Title,
                FileName = $"recipe-{i + 1}.html",
                Category = r.Category,
                Yield = r.Yield,
                PrepTime = r.PrepTime,
                Description = r.Description
            }).ToList();
        return ExportIndexPage(entries);
    }

    /// <summary>Export a single recipe to a self-contained HTML page.</summary>
    public string ExportRecipePage(RecipeExportData data, HtmlExportOptions? options = null)
    {
        options ??= new HtmlExportOptions();
        var recipe = data.Recipe;
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>{HtmlEncode(recipe.Title)} - {HtmlEncode(options.SiteTitle)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetRecipePageCss(options));
        if (!string.IsNullOrEmpty(options.CustomCss))
            sb.AppendLine(options.CustomCss);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("  <header class=\"site-header\">");
        if (!string.IsNullOrEmpty(options.LogoUrl))
            sb.AppendLine($"    <img src=\"{HtmlEncode(options.LogoUrl)}\" alt=\"Logo\" class=\"logo\">");
        if (!string.IsNullOrEmpty(options.BaseUrl))
            sb.AppendLine($"    <nav><a href=\"{HtmlEncode(options.BaseUrl)}/index.html\" class=\"breadcrumb\">&larr; {HtmlEncode(options.SiteTitle)}</a></nav>");
        sb.AppendLine("  </header>");

        sb.AppendLine("  <main class=\"recipe-page\">");

        // Recipe title & meta
        sb.AppendLine("    <div class=\"recipe-header\">");
        sb.AppendLine($"      <h1 class=\"recipe-title\">{HtmlEncode(recipe.Title)}</h1>");

        // Author / source
        var authorParts = new List<string>();
        var author = data.CreatedBy ?? recipe.Author;
        if (!string.IsNullOrEmpty(author))
            authorParts.Add($"By <span class=\"author\">{HtmlEncode(author)}</span>");
        var source = data.Source ?? recipe.Source;
        var sourceUrl = data.SourceUrl ?? recipe.Url;
        if (!string.IsNullOrEmpty(source))
        {
            if (!string.IsNullOrEmpty(sourceUrl))
                authorParts.Add($"Source: <a href=\"{HtmlEncode(sourceUrl)}\" target=\"_blank\" rel=\"noopener\">{HtmlEncode(source)}</a>");
            else
                authorParts.Add($"Source: <span>{HtmlEncode(source)}</span>");
        }
        else if (!string.IsNullOrEmpty(sourceUrl))
        {
            authorParts.Add($"<a href=\"{HtmlEncode(sourceUrl)}\" target=\"_blank\" rel=\"noopener\">{HtmlEncode(sourceUrl)}</a>");
        }

        if (authorParts.Count > 0)
            sb.AppendLine($"      <p class=\"recipe-meta-author\">{string.Join(" &bull; ", authorParts)}</p>");

        // Rating stars
        if (options.IncludeRatings && data.AverageRating.HasValue)
        {
            var ratingHtml = RenderStars(data.AverageRating.Value);
            var countStr = data.RatingCount.HasValue ? $" <span class=\"rating-count\">({data.RatingCount.Value})</span>" : "";
            sb.AppendLine($"      <div class=\"rating\">{ratingHtml}{countStr}</div>");
        }

        sb.AppendLine("    </div>");

        // Meta bar
        var metaItems = new List<string>();
        if (!string.IsNullOrEmpty(recipe.PrepTime)) metaItems.Add(MetaItem("Prep", recipe.PrepTime));
        if (!string.IsNullOrEmpty(recipe.CookTime)) metaItems.Add(MetaItem("Cook", recipe.CookTime));
        if (!string.IsNullOrEmpty(recipe.TotalTime)) metaItems.Add(MetaItem("Total", recipe.TotalTime));
        if (!string.IsNullOrEmpty(recipe.Yield)) metaItems.Add(MetaItem("Yield", recipe.Yield));
        if (!string.IsNullOrEmpty(recipe.Category)) metaItems.Add(MetaItem("Category", recipe.Category));
        if (!string.IsNullOrEmpty(recipe.Cuisine)) metaItems.Add(MetaItem("Cuisine", recipe.Cuisine));

        if (metaItems.Count > 0)
        {
            sb.AppendLine("    <div class=\"recipe-meta-bar\">");
            foreach (var item in metaItems)
                sb.AppendLine($"      {item}");
            sb.AppendLine("    </div>");
        }

        // Main image
        if (options.IncludeImages && !string.IsNullOrEmpty(data.ThumbnailUrl))
        {
            sb.AppendLine($"    <img src=\"{HtmlEncode(data.ThumbnailUrl)}\" alt=\"{HtmlEncode(recipe.Title)}\" class=\"recipe-image\">");
        }

        // Description
        if (!string.IsNullOrEmpty(recipe.Description))
            sb.AppendLine($"    <p class=\"recipe-description\">{HtmlEncode(recipe.Description)}</p>");

        // Allergen warning
        if (data.Allergens.Count > 0)
        {
            sb.AppendLine("    <div class=\"allergen-warning\">");
            sb.AppendLine("      <strong>&#9888; Allergen Warning:</strong> Contains ");
            sb.AppendLine($"      <span class=\"allergen-list\">{HtmlEncode(string.Join(", ", data.Allergens))}</span>");
            sb.AppendLine("    </div>");
        }

        // Dietary badges
        if (data.DietaryTags.Count > 0 || recipe.Tags.Count > 0)
        {
            sb.AppendLine("    <div class=\"tags\">");
            foreach (var tag in data.DietaryTags)
                sb.AppendLine($"      <span class=\"badge badge-dietary\">{HtmlEncode(tag)}</span>");
            foreach (var tag in recipe.Tags)
                sb.AppendLine($"      <span class=\"badge badge-tag\">{HtmlEncode(tag)}</span>");
            sb.AppendLine("    </div>");
        }

        // Ingredients
        if (recipe.Ingredients.Count > 0)
        {
            sb.AppendLine("    <section class=\"ingredients-section\">");
            sb.AppendLine("      <h2>Ingredients</h2>");

            var groups = recipe.Ingredients
                .GroupBy(i => i.GroupHeading ?? "")
                .ToList();

            foreach (var group in groups)
            {
                if (!string.IsNullOrEmpty(group.Key))
                    sb.AppendLine($"      <h3 class=\"ingredient-group\">{HtmlEncode(group.Key)}</h3>");
                sb.AppendLine("      <ul class=\"ingredient-list\">");
                foreach (var ing in group)
                {
                    var ingText = FormatIngredient(ing);
                    var optional = ing.IsOptional ? " <em>(optional)</em>" : "";
                    sb.AppendLine($"        <li>{HtmlEncode(ingText)}{optional}</li>");
                }
                sb.AppendLine("      </ul>");
            }
            sb.AppendLine("    </section>");
        }

        // Instructions
        if (recipe.Instructions.Count > 0)
        {
            sb.AppendLine("    <section class=\"instructions-section\">");
            sb.AppendLine("      <h2>Instructions</h2>");
            sb.AppendLine("      <ol class=\"instruction-list\">");
            foreach (var inst in recipe.Instructions.OrderBy(i => i.Step))
            {
                sb.AppendLine($"        <li class=\"instruction-step\">{HtmlEncode(inst.Text)}</li>");
            }
            sb.AppendLine("      </ol>");
            sb.AppendLine("    </section>");
        }

        // Nutrition facts
        if (options.IncludeNutrition && recipe.Nutrition != null)
        {
            sb.AppendLine("    <section class=\"nutrition-section\">");
            sb.AppendLine("      <h2>Nutrition Facts</h2>");
            sb.AppendLine("      <table class=\"nutrition-table\">");
            AppendNutritionRow(sb, "Calories", recipe.Nutrition.Calories);
            AppendNutritionRow(sb, "Fat", recipe.Nutrition.Fat);
            AppendNutritionRow(sb, "Carbohydrates", recipe.Nutrition.Carbohydrates);
            AppendNutritionRow(sb, "Protein", recipe.Nutrition.Protein);
            AppendNutritionRow(sb, "Fiber", recipe.Nutrition.Fiber);
            AppendNutritionRow(sb, "Sugar", recipe.Nutrition.Sugar);
            AppendNutritionRow(sb, "Sodium", recipe.Nutrition.Sodium);
            AppendNutritionRow(sb, "Cholesterol", recipe.Nutrition.Cholesterol);
            foreach (var kv in recipe.Nutrition.Other)
                AppendNutritionRow(sb, kv.Key, kv.Value);
            sb.AppendLine("      </table>");
            sb.AppendLine("    </section>");
        }

        // Notes
        if (!string.IsNullOrEmpty(data.Notes))
        {
            sb.AppendLine("    <section class=\"notes-section\">");
            sb.AppendLine("      <h2>Notes</h2>");
            sb.AppendLine($"      <p>{HtmlEncode(data.Notes)}</p>");
            sb.AppendLine("    </section>");
        }

        // Print button
        if (options.IncludePrintButton)
        {
            sb.AppendLine("    <button class=\"print-btn\" onclick=\"window.print()\">&#128438; Print Recipe</button>");
        }

        sb.AppendLine("  </main>");

        // Footer
        sb.AppendLine("  <footer class=\"site-footer\">");
        var footerParts = new List<string>();
        if (!string.IsNullOrEmpty(sourceUrl))
            footerParts.Add($"<a href=\"{HtmlEncode(sourceUrl)}\" target=\"_blank\" rel=\"noopener\">Source</a>");
        if (data.CreatedAt.HasValue)
            footerParts.Add($"Created: {data.CreatedAt.Value:yyyy-MM-dd}");
        if (data.UpdatedAt.HasValue)
            footerParts.Add($"Updated: {data.UpdatedAt.Value:yyyy-MM-dd}");
        if (footerParts.Count > 0)
            sb.AppendLine($"    <p>{string.Join(" &bull; ", footerParts)}</p>");
        sb.AppendLine("  </footer>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Export a collection of recipes as an index page (recipe cards grid)
    /// where each card links to the recipe filename.
    /// </summary>
    public string ExportIndexPage(IEnumerable<RecipeIndexEntry> entries, HtmlExportOptions? options = null)
    {
        options ??= new HtmlExportOptions();
        var entryList = entries.ToList();
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>{HtmlEncode(options.SiteTitle)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetIndexPageCss(options));
        if (!string.IsNullOrEmpty(options.CustomCss))
            sb.AppendLine(options.CustomCss);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Site header
        sb.AppendLine("  <header class=\"site-header\">");
        if (!string.IsNullOrEmpty(options.LogoUrl))
            sb.AppendLine($"    <img src=\"{HtmlEncode(options.LogoUrl)}\" alt=\"Logo\" class=\"logo\">");
        sb.AppendLine($"    <h1>{HtmlEncode(options.SiteTitle)}</h1>");
        sb.AppendLine($"    <p class=\"recipe-count\" id=\"recipe-count\">{entryList.Count} recipe{(entryList.Count != 1 ? "s" : "")}</p>");
        sb.AppendLine("  </header>");

        sb.AppendLine("  <main>");
        sb.AppendLine("    <!-- RECIPE_GRID -->");
        sb.AppendLine("    <div class=\"recipe-grid\">");
        foreach (var entry in entryList)
            sb.Append(BuildCardHtml(entry, options));
        sb.AppendLine("    </div>");
        sb.AppendLine("    <!-- /RECIPE_GRID -->");
        sb.AppendLine("  </main>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Add a new recipe link/card to an existing index HTML page.
    /// Returns the updated HTML string.
    /// </summary>
    public string AddToIndexPage(string existingIndexHtml, RecipeIndexEntry entry, HtmlExportOptions? options = null)
    {
        options ??= new HtmlExportOptions();
        var cardHtml = BuildCardHtml(entry, options);

        const string closingMarker = "<!-- /RECIPE_GRID -->";
        int markerIndex = existingIndexHtml.IndexOf(closingMarker, StringComparison.Ordinal);

        string updated;
        if (markerIndex >= 0)
        {
            updated = existingIndexHtml.Insert(markerIndex, cardHtml + "    ");
        }
        else
        {
            // Fallback: append before </body>
            int bodyClose = existingIndexHtml.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyClose >= 0)
                updated = existingIndexHtml.Insert(bodyClose, cardHtml);
            else
                updated = existingIndexHtml + cardHtml;
        }

        // Update the count display - find current count and increment
        updated = Regex.Replace(
            updated,
            @"(\d+) recipe(s?)",
            m =>
            {
                if (int.TryParse(m.Groups[1].Value, out int n))
                    return $"{n + 1} recipe{(n + 1 != 1 ? "s" : "")}";
                return m.Value;
            });

        return updated;
    }

    private static string BuildCardHtml(RecipeIndexEntry entry, HtmlExportOptions options)
    {
        var sb = new StringBuilder();
        var href = !string.IsNullOrEmpty(options.BaseUrl)
            ? $"{options.BaseUrl.TrimEnd('/')}/{entry.FileName}"
            : entry.FileName;

        sb.AppendLine("      <article class=\"recipe-card\">");
        sb.AppendLine($"        <a href=\"{HtmlEncode(href)}\">");

        if (options.IncludeImages)
        {
            if (!string.IsNullOrEmpty(entry.ThumbnailUrl))
                sb.AppendLine($"          <img src=\"{HtmlEncode(entry.ThumbnailUrl)}\" alt=\"{HtmlEncode(entry.Title)}\" class=\"card-image\">");
            else
                sb.AppendLine("          <div class=\"card-image-placeholder\">&#127860;</div>");
        }

        sb.AppendLine("          <div class=\"card-body\">");
        sb.AppendLine($"            <h2 class=\"card-title\">{HtmlEncode(entry.Title)}</h2>");

        var metaParts = new List<string>();
        if (!string.IsNullOrEmpty(entry.Category)) metaParts.Add(HtmlEncode(entry.Category));
        if (!string.IsNullOrEmpty(entry.PrepTime)) metaParts.Add($"Prep: {HtmlEncode(entry.PrepTime)}");
        if (!string.IsNullOrEmpty(entry.Yield)) metaParts.Add($"Yield: {HtmlEncode(entry.Yield)}");
        if (metaParts.Count > 0)
            sb.AppendLine($"            <p class=\"card-meta\">{string.Join(" &bull; ", metaParts)}</p>");

        if (options.IncludeRatings && entry.AverageRating.HasValue)
            sb.AppendLine($"            <div class=\"card-rating\">{RenderStars(entry.AverageRating.Value)}</div>");

        if (!string.IsNullOrEmpty(entry.Description))
        {
            var snippet = entry.Description.Length > 100
                ? entry.Description[..100] + "…"
                : entry.Description;
            sb.AppendLine($"            <p class=\"card-description\">{HtmlEncode(snippet)}</p>");
        }

        sb.AppendLine("          </div>");
        sb.AppendLine("        </a>");
        sb.AppendLine("      </article>");

        return sb.ToString();
    }

    private static string RenderStars(double rating)
    {
        var sb = new StringBuilder("<span class=\"stars\" aria-label=\"");
        sb.Append(rating.ToString("0.0"));
        sb.Append(" out of 5 stars\">");
        for (int i = 1; i <= 5; i++)
        {
            if (rating >= i)
                sb.Append("<span class=\"star full\">&#9733;</span>"); // ★
            else if (rating >= i - 0.5)
                sb.Append("<span class=\"star half\">&#9734;</span>"); // ☆ half-approximation
            else
                sb.Append("<span class=\"star empty\">&#9734;</span>"); // ☆
        }
        sb.Append("</span>");
        return sb.ToString();
    }

    private static string MetaItem(string label, string value) =>
        $"<div class=\"meta-item\"><span class=\"meta-label\">{HtmlEncode(label)}</span><span class=\"meta-value\">{HtmlEncode(value)}</span></div>";

    private static void AppendNutritionRow(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.AppendLine($"        <tr><td class=\"nut-label\">{HtmlEncode(label)}</td><td class=\"nut-value\">{HtmlEncode(value)}</td></tr>");
    }

    private static string FormatIngredient(ParsedIngredient ing)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(ing.Quantity)) sb.Append(ing.Quantity).Append(' ');
        if (!string.IsNullOrEmpty(ing.Unit)) sb.Append(ing.Unit).Append(' ');
        sb.Append(ing.Name);
        if (!string.IsNullOrEmpty(ing.Preparation)) sb.Append(", ").Append(ing.Preparation);
        return sb.ToString().Trim();
    }

    private static string HtmlEncode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    private static string GetRecipePageCss(HtmlExportOptions options) => @"
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: Georgia, serif; font-size: 16px; line-height: 1.6; color: #333; background: #fafafa; }
    a { color: #E67E22; text-decoration: none; }
    a:hover { text-decoration: underline; }

    .site-header { background: #fff; border-bottom: 3px solid #E67E22; padding: 12px 24px; display: flex; align-items: center; gap: 16px; }
    .site-header .logo { height: 40px; }
    .breadcrumb { font-size: 14px; color: #E67E22; }

    .recipe-page { max-width: 860px; margin: 32px auto; padding: 0 16px; }

    .recipe-header { margin-bottom: 24px; }
    .recipe-title { font-size: 2.2rem; color: #2c2c2c; margin-bottom: 8px; }
    .recipe-meta-author { color: #666; font-size: 14px; margin-bottom: 8px; }

    .rating { font-size: 20px; margin-bottom: 8px; }
    .star.full { color: #E67E22; }
    .star.half, .star.empty { color: #ccc; }
    .rating-count { font-size: 14px; color: #888; }

    .recipe-meta-bar { display: flex; flex-wrap: wrap; gap: 16px; background: #fff; border: 1px solid #eee; border-radius: 8px; padding: 16px; margin-bottom: 24px; }
    .meta-item { display: flex; flex-direction: column; min-width: 80px; }
    .meta-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; color: #999; }
    .meta-value { font-size: 15px; font-weight: bold; color: #333; }

    .recipe-image { width: 100%; max-height: 420px; object-fit: cover; border-radius: 10px; margin-bottom: 24px; }
    .recipe-description { font-size: 16px; color: #555; margin-bottom: 24px; font-style: italic; }

    .allergen-warning { background: #fff3cd; border: 1px solid #ffc107; border-radius: 6px; padding: 12px 16px; margin-bottom: 16px; color: #856404; }

    .tags { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 24px; }
    .badge { padding: 4px 10px; border-radius: 14px; font-size: 12px; font-weight: 600; }
    .badge-dietary { background: #d4edda; color: #155724; }
    .badge-tag { background: #e2e3e5; color: #383d41; }

    .ingredients-section, .instructions-section, .nutrition-section, .notes-section { margin-bottom: 32px; }
    h2 { font-size: 1.5rem; color: #E67E22; border-bottom: 2px solid #E67E22; padding-bottom: 6px; margin-bottom: 16px; }
    h3.ingredient-group { font-size: 1rem; text-transform: uppercase; color: #888; margin: 16px 0 8px; letter-spacing: 0.5px; }

    .ingredient-list { list-style: none; padding: 0; }
    .ingredient-list li { padding: 6px 0; border-bottom: 1px solid #f0f0f0; }
    .ingredient-list li:last-child { border-bottom: none; }

    .instruction-list { padding-left: 0; counter-reset: steps; list-style: none; }
    .instruction-step { counter-increment: steps; position: relative; padding: 12px 0 12px 56px; border-bottom: 1px solid #f0f0f0; }
    .instruction-step::before { content: counter(steps); position: absolute; left: 0; top: 8px; width: 36px; height: 36px; background: #E67E22; color: #fff; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: bold; font-size: 15px; line-height: 36px; text-align: center; }

    .nutrition-table { width: 100%; max-width: 320px; border-collapse: collapse; }
    .nutrition-table tr { border-bottom: 1px solid #eee; }
    .nut-label { padding: 6px 0; color: #555; width: 60%; }
    .nut-value { padding: 6px 0; font-weight: bold; text-align: right; }

    .notes-section p { background: #fffbf0; border-left: 4px solid #E67E22; padding: 12px 16px; border-radius: 0 6px 6px 0; }

    .print-btn { display: inline-flex; align-items: center; gap: 8px; margin-top: 16px; padding: 10px 20px; background: #E67E22; color: #fff; border: none; border-radius: 6px; cursor: pointer; font-size: 15px; }
    .print-btn:hover { background: #d35400; }

    .site-footer { background: #f0f0f0; border-top: 1px solid #ddd; padding: 16px 24px; text-align: center; color: #888; font-size: 13px; margin-top: 48px; }

    @media print {
      .site-header, .print-btn { display: none; }
      body { background: #fff; font-size: 11pt; }
      .recipe-page { max-width: 100%; margin: 0; padding: 0; }
      .recipe-title { font-size: 18pt; }
      h2 { font-size: 13pt; }
      .recipe-image { max-height: 250px; }
      .allergen-warning { border: 1px solid #000; background: #fff; }
      a { color: #000; text-decoration: none; }
      .recipe-meta-bar { border: 1px solid #000; }
    }

    @media (max-width: 600px) {
      .recipe-title { font-size: 1.5rem; }
      .recipe-meta-bar { gap: 12px; }
    }
";

    private static string GetIndexPageCss(HtmlExportOptions options) => @"
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: Arial, sans-serif; font-size: 16px; line-height: 1.5; color: #333; background: #f5f5f5; }
    a { color: inherit; text-decoration: none; }

    .site-header { background: #E67E22; color: #fff; padding: 24px; text-align: center; }
    .site-header h1 { font-size: 2rem; }
    .site-header .logo { height: 48px; margin-bottom: 8px; }
    .recipe-count { margin-top: 8px; font-size: 14px; opacity: 0.85; }

    main { max-width: 1200px; margin: 32px auto; padding: 0 16px; }

    .recipe-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 24px; }
    @media (max-width: 900px) { .recipe-grid { grid-template-columns: repeat(2, 1fr); } }
    @media (max-width: 580px) { .recipe-grid { grid-template-columns: 1fr; } }

    .recipe-card { background: #fff; border-radius: 10px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.08); transition: transform 0.15s, box-shadow 0.15s; }
    .recipe-card:hover { transform: translateY(-3px); box-shadow: 0 6px 20px rgba(0,0,0,0.14); }
    .recipe-card a { display: block; }

    .card-image { width: 100%; height: 180px; object-fit: cover; }
    .card-image-placeholder { width: 100%; height: 180px; background: #f0e6d3; display: flex; align-items: center; justify-content: center; font-size: 3rem; color: #E67E22; }

    .card-body { padding: 16px; }
    .card-title { font-size: 1.1rem; font-weight: 700; color: #2c2c2c; margin-bottom: 6px; }
    .card-meta { font-size: 12px; color: #888; margin-bottom: 6px; }
    .card-rating { font-size: 16px; margin-bottom: 6px; }
    .star.full { color: #E67E22; }
    .star.half, .star.empty { color: #ccc; }
    .card-description { font-size: 13px; color: #666; margin-top: 8px; line-height: 1.4; }
";
}
