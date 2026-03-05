using ExpressRecipe.RecipeParser.Exporters;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Tests.Exporters;

public class HtmlRecipeExporterTests
{
    private static RecipeExportData MakeSampleData() => new()
    {
        Recipe = new ParsedRecipe
        {
            Title = "Chocolate Cake",
            Description = "A rich, moist chocolate cake.",
            Author = "Chef Alice",
            Yield = "8 servings",
            PrepTime = "20 min",
            CookTime = "40 min",
            Category = "Dessert",
            Cuisine = "American",
            Tags = ["chocolate", "cake"],
            Ingredients =
            [
                new ParsedIngredient { Quantity = "2", Unit = "cups", Name = "flour" },
                new ParsedIngredient { Quantity = "1", Unit = "cup", Name = "sugar" },
                new ParsedIngredient { Name = "cocoa powder", Quantity = "3", Unit = "tbsp", GroupHeading = "Dry Ingredients" }
            ],
            Instructions =
            [
                new ParsedInstruction { Step = 1, Text = "Mix dry ingredients." },
                new ParsedInstruction { Step = 2, Text = "Add wet ingredients and stir." },
                new ParsedInstruction { Step = 3, Text = "Bake at 350°F for 40 minutes." }
            ],
            Nutrition = new ParsedNutrition { Calories = "350", Fat = "12g", Carbohydrates = "55g", Protein = "5g" }
        },
        AverageRating = 4.5,
        RatingCount = 12,
        ThumbnailUrl = "https://example.com/cake.jpg",
        Notes = "Best served warm.",
        Source = "Family Cookbook",
        SourceUrl = "https://example.com/recipes",
        Allergens = ["Gluten", "Eggs"],
        DietaryTags = ["vegetarian"],
        CreatedAt = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
        CreatedBy = "Alice"
    };

    private static RecipeIndexEntry MakeSampleEntry(int n = 1) => new()
    {
        Title = $"Recipe {n}",
        FileName = $"recipe-{n}.html",
        ThumbnailUrl = $"https://example.com/img{n}.jpg",
        AverageRating = 3.5,
        Category = "Main",
        PrepTime = "15 min",
        Yield = "4 servings",
        Description = "A delicious recipe that everyone will love."
    };

    // ── ExportRecipePage ──────────────────────────────────────────────────────

    [Fact]
    public void ExportRecipePage_ContainsTitle()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().Contain("Chocolate Cake");
    }

    [Fact]
    public void ExportRecipePage_ContainsIngredients()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().Contain("flour");
        html.Should().Contain("sugar");
    }

    [Fact]
    public void ExportRecipePage_ContainsInstructions()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().Contain("Mix dry ingredients");
        html.Should().Contain("Bake at");
    }

    [Fact]
    public void ExportRecipePage_ContainsRatingStars()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        // Unicode stars ★ or HTML entity &#9733;
        html.Should().MatchRegex(@"(&#9733;|★)");
    }

    [Fact]
    public void ExportRecipePage_ContainsNutritionSection()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().Contain("Nutrition Facts");
        html.Should().Contain("350");
    }

    [Fact]
    public void ExportRecipePage_ContainsAllergenWarning()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().Contain("Allergen");
        html.Should().Contain("Gluten");
    }

    [Fact]
    public void ExportRecipePage_ContainsNotes()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().Contain("Best served warm");
    }

    [Fact]
    public void ExportRecipePage_ContainsPrintButton_WhenEnabled()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData(), new HtmlExportOptions { IncludePrintButton = true });
        html.Should().Contain("print");
    }

    [Fact]
    public void ExportRecipePage_NoPrintButton_WhenDisabled()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData(), new HtmlExportOptions { IncludePrintButton = false });
        html.Should().NotContain("window.print()");
    }

    [Fact]
    public void ExportRecipePage_ContainsBreadcrumb_WhenBaseUrlSet()
    {
        var exporter = new HtmlRecipeExporter();
        var options = new HtmlExportOptions { BaseUrl = "https://example.com" };
        var html = exporter.ExportRecipePage(MakeSampleData(), options);
        html.Should().Contain("https://example.com");
    }

    [Fact]
    public void ExportRecipePage_ContainsIngredientGroupHeading()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().Contain("Dry Ingredients");
    }

    [Fact]
    public void ExportRecipePage_IsValidHtml5()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("<html");
        html.Should().Contain("</html>");
        html.Should().Contain("<head>");
        html.Should().Contain("</head>");
        html.Should().Contain("<body>");
        html.Should().Contain("</body>");
    }

    [Fact]
    public void ExportRecipePage_HandlesMinimalData_Gracefully()
    {
        var exporter = new HtmlRecipeExporter();
        var minimal = new RecipeExportData { Recipe = new ParsedRecipe { Title = "Simple Recipe" } };
        var act = () => exporter.ExportRecipePage(minimal);
        act.Should().NotThrow();
        act().Should().Contain("Simple Recipe");
    }

    [Fact]
    public void ExportRecipePage_NoNutrition_WhenOptionDisabled()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData(), new HtmlExportOptions { IncludeNutrition = false });
        html.Should().NotContain("Nutrition Facts");
    }

    [Fact]
    public void ExportRecipePage_NoImage_WhenOptionDisabled()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData(), new HtmlExportOptions { IncludeImages = false });
        html.Should().NotContain("cake.jpg");
    }

    [Fact]
    public void ExportRecipePage_CustomCss_Injected()
    {
        var exporter = new HtmlRecipeExporter();
        var options = new HtmlExportOptions { CustomCss = ".custom { color: red; }" };
        var html = exporter.ExportRecipePage(MakeSampleData(), options);
        html.Should().Contain(".custom { color: red; }");
    }

    [Fact]
    public void ExportRecipePage_DietaryBadges_Present()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportRecipePage(MakeSampleData());
        html.Should().Contain("vegetarian");
    }

    // ── ExportIndexPage ───────────────────────────────────────────────────────

    [Fact]
    public void ExportIndexPage_ContainsSiteTitle()
    {
        var exporter = new HtmlRecipeExporter();
        var options = new HtmlExportOptions { SiteTitle = "My Cookbook" };
        var html = exporter.ExportIndexPage([MakeSampleEntry(1), MakeSampleEntry(2)], options);
        html.Should().Contain("My Cookbook");
    }

    [Fact]
    public void ExportIndexPage_ContainsRecipeCards()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportIndexPage([MakeSampleEntry(1), MakeSampleEntry(2)]);
        html.Should().Contain("Recipe 1");
        html.Should().Contain("Recipe 2");
    }

    [Fact]
    public void ExportIndexPage_ContainsRecipeLinks()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportIndexPage([MakeSampleEntry(1)]);
        html.Should().Contain("recipe-1.html");
    }

    [Fact]
    public void ExportIndexPage_ContainsRecipeCount()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportIndexPage([MakeSampleEntry(1), MakeSampleEntry(2), MakeSampleEntry(3)]);
        html.Should().Contain("3 recipes");
    }

    [Fact]
    public void ExportIndexPage_ContainsRatingStars()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportIndexPage([MakeSampleEntry(1)]);
        html.Should().MatchRegex(@"(&#9733;|★)");
    }

    [Fact]
    public void ExportIndexPage_ContainsGridMarkers()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportIndexPage([MakeSampleEntry(1)]);
        html.Should().Contain("<!-- RECIPE_GRID -->");
        html.Should().Contain("<!-- /RECIPE_GRID -->");
    }

    [Fact]
    public void ExportIndexPage_ContainsDescriptionSnippet()
    {
        var exporter = new HtmlRecipeExporter();
        var html = exporter.ExportIndexPage([MakeSampleEntry(1)]);
        html.Should().Contain("A delicious recipe");
    }

    [Fact]
    public void ExportIndexPage_EmptyList_ProducesValidHtml()
    {
        var exporter = new HtmlRecipeExporter();
        var act = () => exporter.ExportIndexPage([]);
        act.Should().NotThrow();
        act().Should().Contain("0 recipes");
    }

    [Fact]
    public void ExportIndexPage_PlaceholderShown_WhenNoThumbnail()
    {
        var exporter = new HtmlRecipeExporter();
        var entry = new RecipeIndexEntry { Title = "No Image", FileName = "no-image.html" };
        var html = exporter.ExportIndexPage([entry]);
        html.Should().Contain("card-image-placeholder");
    }

    // ── AddToIndexPage ────────────────────────────────────────────────────────

    [Fact]
    public void AddToIndexPage_AppendsNewCard()
    {
        var exporter = new HtmlRecipeExporter();
        var initial = exporter.ExportIndexPage([MakeSampleEntry(1)]);
        var updated = exporter.AddToIndexPage(initial, MakeSampleEntry(2));
        updated.Should().Contain("Recipe 1");
        updated.Should().Contain("Recipe 2");
    }

    [Fact]
    public void AddToIndexPage_UpdatesCount()
    {
        var exporter = new HtmlRecipeExporter();
        var initial = exporter.ExportIndexPage([MakeSampleEntry(1)]);
        var updated = exporter.AddToIndexPage(initial, MakeSampleEntry(2));
        updated.Should().Contain("2 recipes");
    }

    [Fact]
    public void AddToIndexPage_FallsBackToBodyTag_WhenNoMarker()
    {
        var exporter = new HtmlRecipeExporter();
        const string html = "<html><body><p>existing</p></body></html>";
        var updated = exporter.AddToIndexPage(html, MakeSampleEntry(1));
        updated.Should().Contain("Recipe 1");
    }

    // ── IRecipeExporter interface ─────────────────────────────────────────────

    [Fact]
    public void Export_ViaInterface_ReturnsHtml()
    {
        IRecipeExporter exporter = new HtmlRecipeExporter();
        var result = exporter.Export(new ParsedRecipe { Title = "Interface Test" });
        result.Should().Contain("<!DOCTYPE html>");
        result.Should().Contain("Interface Test");
    }

    [Fact]
    public void ExportAll_ViaInterface_ReturnsIndexHtml()
    {
        IRecipeExporter exporter = new HtmlRecipeExporter();
        var result = exporter.ExportAll([
            new ParsedRecipe { Title = "Alpha" },
            new ParsedRecipe { Title = "Beta" }
        ]);
        result.Should().Contain("<!DOCTYPE html>");
        result.Should().Contain("Alpha");
        result.Should().Contain("Beta");
    }

    [Fact]
    public void FormatName_IsHtml()
    {
        var exporter = new HtmlRecipeExporter();
        exporter.FormatName.Should().Be("HTML");
    }

    [Fact]
    public void DefaultFileExtension_IsHtml()
    {
        var exporter = new HtmlRecipeExporter();
        exporter.DefaultFileExtension.Should().Be("html");
    }

    // ── RecipeExportEngine integration ────────────────────────────────────────

    [Fact]
    public void RecipeExportEngine_SupportedFormats_ContainsHtml()
    {
        RecipeExportEngine.SupportedFormats.Should().Contain("HTML");
    }

    [Fact]
    public void RecipeExportEngine_ExportRecipeHtml_ReturnsHtml()
    {
        var data = new RecipeExportData { Recipe = new ParsedRecipe { Title = "Engine HTML Test" } };
        var html = RecipeExportEngine.ExportRecipeHtml(data);
        html.Should().Contain("Engine HTML Test");
    }

    [Fact]
    public void RecipeExportEngine_ExportIndexHtml_ReturnsIndexPage()
    {
        var entries = new[] { MakeSampleEntry(1) };
        var html = RecipeExportEngine.ExportIndexHtml(entries);
        html.Should().Contain("Recipe 1");
    }

    [Fact]
    public void RecipeExportEngine_AddToIndexHtml_AppendsCard()
    {
        var initial = RecipeExportEngine.ExportIndexHtml([MakeSampleEntry(1)]);
        var updated = RecipeExportEngine.AddToIndexHtml(initial, MakeSampleEntry(2));
        updated.Should().Contain("Recipe 2");
    }
}
