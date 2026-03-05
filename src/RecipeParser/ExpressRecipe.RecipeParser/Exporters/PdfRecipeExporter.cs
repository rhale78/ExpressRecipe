using ExpressRecipe.RecipeParser.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExpressRecipe.RecipeParser.Exporters;

/// <summary>
/// Exports recipes to PDF using QuestPDF (MIT license).
/// Returns base64-encoded PDF bytes from the IRecipeExporter interface methods.
/// Use the typed overloads (ExportRecipePdf, ExportCookbookPdf, AddToPdf) for raw byte[] access.
/// </summary>
public sealed class PdfRecipeExporter : IRecipeExporter
{
    static PdfRecipeExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string FormatName => "PDF";
    public string DefaultFileExtension => "pdf";

    /// <inheritdoc/>
    public string Export(ParsedRecipe recipe)
    {
        var data = new RecipeExportData { Recipe = recipe };
        return Convert.ToBase64String(ExportRecipePdf(data));
    }

    /// <inheritdoc/>
    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
    {
        var dataList = recipes.Select(r => new RecipeExportData { Recipe = r });
        return Convert.ToBase64String(ExportCookbookPdf(dataList));
    }

    /// <summary>Export a single recipe to PDF bytes.</summary>
    public byte[] ExportRecipePdf(RecipeExportData data, PdfExportOptions? options = null)
    {
        options ??= new PdfExportOptions { Title = data.Recipe.Title };
        return Document.Create(container => ComposeRecipePage(container, data, options))
            .GeneratePdf();
    }

    /// <summary>
    /// Export multiple recipes as a single PDF with an optional table of contents page.
    /// </summary>
    public byte[] ExportCookbookPdf(IEnumerable<RecipeExportData> recipes, PdfExportOptions? options = null)
    {
        options ??= new PdfExportOptions();
        var recipeList = recipes.ToList();

        return Document.Create(container =>
        {
            if (recipeList.Count == 0)
            {
                // QuestPDF requires at least one page; render a placeholder
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.Content().AlignCenter().AlignMiddle()
                        .Text("No recipes").FontSize(18).FontColor(Colors.Grey.Medium);
                });
                return;
            }

            if (options.IncludeTableOfContents && recipeList.Count > 1)
                ComposeTocPage(container, recipeList, options);

            foreach (var data in recipeList)
                ComposeRecipePage(container, data, options);
        }).GeneratePdf();
    }

    /// <summary>
    /// Combine existing recipes with a new recipe into a single PDF.
    /// Because PDF merging requires re-rendering, pass all existing recipes alongside the new one.
    /// For a true single-source-of-truth cookbook, use <see cref="ExportCookbookPdf"/> directly.
    /// </summary>
    public byte[] AddToPdf(IEnumerable<RecipeExportData> existingRecipes, RecipeExportData newRecipe, PdfExportOptions? options = null)
    {
        var combined = existingRecipes.Append(newRecipe);
        return ExportCookbookPdf(combined, options);
    }

    // ── QuestPDF composition helpers ──────────────────────────────────────────

    private static void ComposeTocPage(IDocumentContainer container, List<RecipeExportData> recipes, PdfExportOptions options)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);

            page.Header().Element(h =>
            {
                h.Text(options.Title).FontSize(28).Bold().FontColor(ParseColor(options.PrimaryColor));
            });

            page.Content().Element(c =>
            {
                c.PaddingTop(20).Column(col =>
                {
                    col.Item().Text("Table of Contents").FontSize(18).Bold().FontColor(ParseColor(options.PrimaryColor));
                    col.Item().PaddingTop(12).Column(toc =>
                    {
                        for (int i = 0; i < recipes.Count; i++)
                        {
                            var title = recipes[i].Recipe.Title;
                            toc.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{i + 1}. {title}").FontSize(13);
                            });
                        }
                    });
                });
            });

            if (options.AddPageNumbers)
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ").FontSize(10).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(10).FontColor(Colors.Grey.Medium);
                    x.Span(" of ").FontSize(10).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(10).FontColor(Colors.Grey.Medium);
                });
        });
    }

    private static void ComposeRecipePage(IDocumentContainer container, RecipeExportData data, PdfExportOptions options)
    {
        var recipe = data.Recipe;
        var primaryColor = ParseColor(options.PrimaryColor);

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);

            // ── Header ──
            page.Header().Element(h =>
            {
                h.Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(recipe.Title).FontSize(22).Bold().FontColor(primaryColor);
                        if (!string.IsNullOrEmpty(recipe.Yield))
                            col.Item().Text($"Yield: {recipe.Yield}").FontSize(11).FontColor(Colors.Grey.Darken2);
                    });
                });
            });

            // ── Content ──
            page.Content().Element(c =>
            {
                c.Column(col =>
                {
                    // Meta row
                    var metaParts = BuildMetaParts(recipe);
                    if (metaParts.Count > 0)
                    {
                        col.Item().PaddingTop(8).Row(row =>
                        {
                            foreach (var (label, value) in metaParts)
                            {
                                row.RelativeItem().Column(mc =>
                                {
                                    mc.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Medium);
                                    mc.Item().Text(value).FontSize(11).Bold();
                                });
                            }
                        });
                    }

                    // Horizontal rule
                    col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(primaryColor);

                    // Two-column body: ingredients (35%) | instructions (65%)
                    col.Item().Row(row =>
                    {
                        // Ingredients (left 35%)
                        row.RelativeItem(35).PaddingRight(16).Column(ingCol =>
                        {
                            ingCol.Item().Text("Ingredients").FontSize(14).Bold().FontColor(primaryColor);
                            ingCol.Item().PaddingTop(6).Column(ingList =>
                            {
                                var groups = recipe.Ingredients
                                    .GroupBy(i => i.GroupHeading ?? "")
                                    .ToList();

                                foreach (var group in groups)
                                {
                                    if (!string.IsNullOrEmpty(group.Key))
                                        ingList.Item().PaddingTop(8).Text(group.Key).FontSize(10).Bold().FontColor(Colors.Grey.Darken2);

                                    foreach (var ing in group)
                                    {
                                        var text = FormatIngredient(ing);
                                        if (ing.IsOptional) text += " (opt.)";
                                        ingList.Item().Row(r =>
                                        {
                                            r.ConstantItem(12).Text("•").FontSize(10).FontColor(primaryColor);
                                            r.RelativeItem().Text(text).FontSize(10);
                                        });
                                    }
                                }
                            });
                        });

                        // Instructions (right 65%)
                        row.RelativeItem(65).PaddingLeft(16).Column(instrCol =>
                        {
                            instrCol.Item().Text("Instructions").FontSize(14).Bold().FontColor(primaryColor);
                            instrCol.Item().PaddingTop(6).Column(instrList =>
                            {
                                foreach (var inst in recipe.Instructions.OrderBy(i => i.Step))
                                {
                                    instrList.Item().PaddingBottom(8).Row(r =>
                                    {
                                        r.ConstantItem(24).Background(primaryColor)
                                            .AlignCenter().AlignMiddle()
                                            .Text(inst.Step.ToString()).FontSize(10).Bold().FontColor(Colors.White);
                                        r.RelativeItem().PaddingLeft(8).Text(inst.Text).FontSize(11);
                                    });
                                }
                            });
                        });
                    });

                    // Nutrition facts
                    if (options.IncludeNutrition && recipe.Nutrition != null)
                    {
                        col.Item().PaddingTop(16).Column(nutCol =>
                        {
                            nutCol.Item().Text("Nutrition Facts").FontSize(14).Bold().FontColor(primaryColor);
                            nutCol.Item().PaddingTop(6).Table(table =>
                            {
                                table.ColumnsDefinition(cd =>
                                {
                                    cd.RelativeColumn(1);
                                    cd.RelativeColumn(1);
                                });

                                AddNutritionRow(table, "Calories", recipe.Nutrition.Calories);
                                AddNutritionRow(table, "Fat", recipe.Nutrition.Fat);
                                AddNutritionRow(table, "Carbohydrates", recipe.Nutrition.Carbohydrates);
                                AddNutritionRow(table, "Protein", recipe.Nutrition.Protein);
                                AddNutritionRow(table, "Fiber", recipe.Nutrition.Fiber);
                                AddNutritionRow(table, "Sugar", recipe.Nutrition.Sugar);
                                AddNutritionRow(table, "Sodium", recipe.Nutrition.Sodium);
                                AddNutritionRow(table, "Cholesterol", recipe.Nutrition.Cholesterol);
                                foreach (var kv in recipe.Nutrition.Other)
                                    AddNutritionRow(table, kv.Key, kv.Value);
                            });
                        });
                    }

                    // Notes
                    if (!string.IsNullOrEmpty(data.Notes))
                    {
                        col.Item().PaddingTop(16).Column(notesCol =>
                        {
                            notesCol.Item().Text("Notes").FontSize(14).Bold().FontColor(primaryColor);
                            notesCol.Item().PaddingTop(6)
                                .Background(Colors.Orange.Lighten5)
                                .Padding(10)
                                .Text(data.Notes).FontSize(11);
                        });
                    }
                });
            });

            // ── Footer ──
            page.Footer().Element(f =>
            {
                f.Row(row =>
                {
                    var sourceText = data.SourceUrl ?? recipe.Url ?? data.Source ?? recipe.Source ?? "";
                    row.RelativeItem().Text(sourceText).FontSize(9).FontColor(Colors.Grey.Medium);
                    if (options.AddPageNumbers)
                        row.ConstantItem(80).AlignRight().Text(x =>
                        {
                            x.Span("Page ").FontSize(9).FontColor(Colors.Grey.Medium);
                            x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                            x.Span(" of ").FontSize(9).FontColor(Colors.Grey.Medium);
                            x.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                });
            });
        });
    }

    private static List<(string Label, string Value)> BuildMetaParts(ParsedRecipe recipe)
    {
        var parts = new List<(string, string)>();
        if (!string.IsNullOrEmpty(recipe.PrepTime)) parts.Add(("Prep Time", recipe.PrepTime));
        if (!string.IsNullOrEmpty(recipe.CookTime)) parts.Add(("Cook Time", recipe.CookTime));
        if (!string.IsNullOrEmpty(recipe.TotalTime)) parts.Add(("Total Time", recipe.TotalTime));
        if (!string.IsNullOrEmpty(recipe.Category)) parts.Add(("Category", recipe.Category));
        if (!string.IsNullOrEmpty(recipe.Cuisine)) parts.Add(("Cuisine", recipe.Cuisine));
        return parts;
    }

    private static void AddNutritionRow(TableDescriptor table, string label, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        table.Cell().Padding(4).Text(label).FontSize(10);
        table.Cell().Padding(4).AlignRight().Text(value).FontSize(10).Bold();
    }

    private static string FormatIngredient(ParsedIngredient ing)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(ing.Quantity)) parts.Add(ing.Quantity);
        if (!string.IsNullOrEmpty(ing.Unit)) parts.Add(ing.Unit);
        parts.Add(ing.Name);
        if (!string.IsNullOrEmpty(ing.Preparation)) parts.Add($"({ing.Preparation})");
        return string.Join(" ", parts);
    }

    private static string ParseColor(string hex)
    {
        // QuestPDF accepts "#RRGGBB" strings directly
        if (!string.IsNullOrEmpty(hex)
            && hex.Length == 7
            && hex[0] == '#'
            && hex[1..].All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
        {
            return hex;
        }
        return "#E67E22";
    }
}
