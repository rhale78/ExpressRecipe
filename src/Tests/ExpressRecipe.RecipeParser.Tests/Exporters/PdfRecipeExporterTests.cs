using ExpressRecipe.RecipeParser.Exporters;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Tests.Exporters;

public class PdfRecipeExporterTests
{
    private static readonly byte[] PdfMagicBytes = "%PDF"u8.ToArray();

    private static RecipeExportData MakeSampleData(string title = "Test Recipe") => new()
    {
        Recipe = new ParsedRecipe
        {
            Title = title,
            Description = "A great dish.",
            Author = "Chef Test",
            Yield = "4 servings",
            PrepTime = "15 min",
            CookTime = "30 min",
            Category = "Main",
            Cuisine = "Italian",
            Tags = ["pasta", "quick"],
            Ingredients =
            [
                new ParsedIngredient { Quantity = "2", Unit = "cups", Name = "pasta" },
                new ParsedIngredient { Quantity = "1", Unit = "cup", Name = "tomato sauce", GroupHeading = "Sauce" }
            ],
            Instructions =
            [
                new ParsedInstruction { Step = 1, Text = "Boil the pasta until al dente." },
                new ParsedInstruction { Step = 2, Text = "Heat the sauce in a pan." },
                new ParsedInstruction { Step = 3, Text = "Combine and serve." }
            ],
            Nutrition = new ParsedNutrition { Calories = "400", Protein = "14g", Fat = "8g", Carbohydrates = "65g" }
        },
        Notes = "Add parmesan on top.",
        Source = "Italian Kitchen",
        SourceUrl = "https://example.com/pasta",
        AverageRating = 4.2,
        Allergens = ["Gluten"]
    };

    // ── ExportRecipePdf ───────────────────────────────────────────────────────

    [Fact]
    public void ExportRecipePdf_ReturnsByteArray()
    {
        var exporter = new PdfRecipeExporter();
        var bytes = exporter.ExportRecipePdf(MakeSampleData());
        bytes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ExportRecipePdf_StartsWithPdfMagicBytes()
    {
        var exporter = new PdfRecipeExporter();
        var bytes = exporter.ExportRecipePdf(MakeSampleData());
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void ExportRecipePdf_MinimalData_Succeeds()
    {
        var exporter = new PdfRecipeExporter();
        var minimal = new RecipeExportData { Recipe = new ParsedRecipe { Title = "Minimal" } };
        var bytes = exporter.ExportRecipePdf(minimal);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void ExportRecipePdf_WithCustomOptions_Succeeds()
    {
        var exporter = new PdfRecipeExporter();
        var options = new PdfExportOptions
        {
            Title = "Custom Title",
            PrimaryColor = "#336699",
            AddPageNumbers = false,
            IncludeNutrition = false
        };
        var bytes = exporter.ExportRecipePdf(MakeSampleData(), options);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void ExportRecipePdf_NoNutrition_WhenDataAbsent()
    {
        var exporter = new PdfRecipeExporter();
        var data = MakeSampleData();
        data.Recipe.Nutrition = null;
        var bytes = exporter.ExportRecipePdf(data);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void ExportRecipePdf_WithNotes_Succeeds()
    {
        var exporter = new PdfRecipeExporter();
        var data = MakeSampleData();
        data.Notes = "A special note about this recipe.";
        var bytes = exporter.ExportRecipePdf(data);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    // ── ExportCookbookPdf ─────────────────────────────────────────────────────

    [Fact]
    public void ExportCookbookPdf_MultipleRecipes_ReturnsValidPdf()
    {
        var exporter = new PdfRecipeExporter();
        var recipes = new[]
        {
            MakeSampleData("Recipe One"),
            MakeSampleData("Recipe Two"),
            MakeSampleData("Recipe Three")
        };
        var bytes = exporter.ExportCookbookPdf(recipes);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void ExportCookbookPdf_SingleRecipe_ReturnsValidPdf()
    {
        var exporter = new PdfRecipeExporter();
        var bytes = exporter.ExportCookbookPdf([MakeSampleData()]);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void ExportCookbookPdf_WithTableOfContents_Succeeds()
    {
        var exporter = new PdfRecipeExporter();
        var options = new PdfExportOptions { IncludeTableOfContents = true };
        var recipes = new[] { MakeSampleData("Alpha"), MakeSampleData("Beta") };
        var bytes = exporter.ExportCookbookPdf(recipes, options);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void ExportCookbookPdf_EmptyList_ReturnsValidPdf()
    {
        var exporter = new PdfRecipeExporter();
        var bytes = exporter.ExportCookbookPdf([]);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    // ── AddToPdf ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddToPdf_CombinesRecipesIntoSinglePdf()
    {
        var exporter = new PdfRecipeExporter();
        var existing = new[] { MakeSampleData("Existing Recipe") };
        var newRecipe = MakeSampleData("New Recipe");
        var bytes = exporter.AddToPdf(existing, newRecipe);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void AddToPdf_EmptyExisting_StillProducesValidPdf()
    {
        var exporter = new PdfRecipeExporter();
        var bytes = exporter.AddToPdf([], MakeSampleData("Standalone"));
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    // ── IRecipeExporter interface ─────────────────────────────────────────────

    [Fact]
    public void Export_ViaInterface_ReturnsBase64()
    {
        IRecipeExporter exporter = new PdfRecipeExporter();
        var result = exporter.Export(new ParsedRecipe { Title = "Interface Test" });
        result.Should().NotBeNullOrEmpty();

        var bytes = Convert.FromBase64String(result);
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void ExportAll_ViaInterface_ReturnsBase64()
    {
        IRecipeExporter exporter = new PdfRecipeExporter();
        var result = exporter.ExportAll([
            new ParsedRecipe { Title = "Alpha" },
            new ParsedRecipe { Title = "Beta" }
        ]);
        result.Should().NotBeNullOrEmpty();

        var bytes = Convert.FromBase64String(result);
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void FormatName_IsPdf()
    {
        var exporter = new PdfRecipeExporter();
        exporter.FormatName.Should().Be("PDF");
    }

    [Fact]
    public void DefaultFileExtension_IsPdf()
    {
        var exporter = new PdfRecipeExporter();
        exporter.DefaultFileExtension.Should().Be("pdf");
    }

    // ── RecipeExportEngine integration ────────────────────────────────────────

    [Fact]
    public void RecipeExportEngine_SupportedFormats_ContainsPdf()
    {
        RecipeExportEngine.SupportedFormats.Should().Contain("PDF");
    }

    [Fact]
    public void RecipeExportEngine_ExportRecipePdf_ReturnsBytes()
    {
        var data = new RecipeExportData { Recipe = new ParsedRecipe { Title = "Engine PDF Test" } };
        var bytes = RecipeExportEngine.ExportRecipePdf(data);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void RecipeExportEngine_ExportCookbookPdf_ReturnsBytes()
    {
        var recipes = new[]
        {
            new RecipeExportData { Recipe = new ParsedRecipe { Title = "Soup" } },
            new RecipeExportData { Recipe = new ParsedRecipe { Title = "Salad" } }
        };
        var bytes = RecipeExportEngine.ExportCookbookPdf(recipes);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }

    [Fact]
    public void RecipeExportEngine_AddToPdf_CombinesRecipes()
    {
        var existing = new[] { new RecipeExportData { Recipe = new ParsedRecipe { Title = "Old" } } };
        var newRecipe = new RecipeExportData { Recipe = new ParsedRecipe { Title = "New" } };
        var bytes = RecipeExportEngine.AddToPdf(existing, newRecipe);
        bytes.Should().NotBeNullOrEmpty();
        bytes.Should().StartWith(PdfMagicBytes);
    }
}
