using ExpressRecipe.RecipeParser.Parsers;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class GoogleStructuredDataParserTests
{
    private readonly GoogleStructuredDataParser _parser = new();

    private const string BareJson = """
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Chocolate Chip Cookies",
          "description": "Classic cookies",
          "author": {"@type": "Person", "name": "Jane Baker"},
          "recipeYield": "24 cookies",
          "prepTime": "PT15M",
          "cookTime": "PT12M",
          "totalTime": "PT27M",
          "recipeCategory": "Dessert",
          "recipeCuisine": "American",
          "keywords": "cookies, chocolate, baking",
          "recipeIngredient": [
            "2 cups flour",
            "1 cup butter",
            "2 eggs"
          ],
          "recipeInstructions": [
            {"@type": "HowToStep", "text": "Preheat oven to 375F."},
            {"@type": "HowToStep", "text": "Mix butter and sugar."},
            {"@type": "HowToStep", "text": "Bake for 12 minutes."}
          ],
          "nutrition": {
            "@type": "NutritionInformation",
            "calories": "150",
            "fatContent": "8g",
            "carbohydrateContent": "20g",
            "proteinContent": "2g"
          }
        }
        """;

    private const string HtmlWithJsonLd = """
        <!DOCTYPE html>
        <html>
        <head>
          <script type="application/ld+json">
          {
            "@context": "https://schema.org",
            "@type": "Recipe",
            "name": "Pasta Carbonara",
            "recipeIngredient": ["200g pasta", "100g bacon", "2 eggs"],
            "recipeInstructions": [
              {"@type": "HowToStep", "text": "Cook pasta."},
              {"@type": "HowToStep", "text": "Fry bacon."}
            ]
          }
          </script>
        </head>
        <body><h1>Pasta Carbonara</h1></body>
        </html>
        """;

    private const string GraphJson = """
        {
          "@context": "https://schema.org",
          "@graph": [
            {"@type": "Recipe", "name": "Recipe One", "recipeIngredient": ["1 cup water"]},
            {"@type": "Recipe", "name": "Recipe Two", "recipeIngredient": ["2 cups milk"]}
          ]
        }
        """;

    private const string HowToSectionJson = """
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Layered Cake",
          "recipeInstructions": [
            {
              "@type": "HowToSection",
              "name": "Make the batter",
              "itemListElement": [
                {"@type": "HowToStep", "text": "Mix dry ingredients."},
                {"@type": "HowToStep", "text": "Add wet ingredients."}
              ]
            },
            {
              "@type": "HowToSection",
              "name": "Bake",
              "itemListElement": [
                {"@type": "HowToStep", "text": "Pour into pan."},
                {"@type": "HowToStep", "text": "Bake at 350F for 30 min."}
              ]
            }
          ]
        }
        """;

    [Fact]
    public void CanParse_BareJson_ReturnsTrue()
        => _parser.CanParse(BareJson).Should().BeTrue();

    [Fact]
    public void CanParse_HtmlWithJsonLd_ReturnsTrue()
        => _parser.CanParse(HtmlWithJsonLd).Should().BeTrue();

    [Fact]
    public void CanParse_RandomText_ReturnsFalse()
        => _parser.CanParse("Hello world this is not a recipe").Should().BeFalse();

    [Fact]
    public void Parse_BareJson_ParsesCorrectly()
    {
        var result = _parser.Parse(BareJson);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);

        var recipe = result.Recipes[0];
        recipe.Title.Should().Be("Chocolate Chip Cookies");
        recipe.Description.Should().Be("Classic cookies");
        recipe.Author.Should().Be("Jane Baker");
        recipe.Yield.Should().Be("24 cookies");
        recipe.Category.Should().Be("Dessert");
        recipe.Cuisine.Should().Be("American");
        recipe.Tags.Should().Contain("cookies");
        recipe.Tags.Should().Contain("chocolate");
        recipe.Ingredients.Should().HaveCount(3);
        recipe.Instructions.Should().HaveCount(3);
        recipe.Nutrition.Should().NotBeNull();
        recipe.Nutrition!.Calories.Should().Be("150");
    }

    [Fact]
    public void Parse_BareJson_ParsesIsoDurations()
    {
        var result = _parser.Parse(BareJson);
        var recipe = result.Recipes[0];
        recipe.PrepTime.Should().NotBeNull();
        recipe.PrepTime.Should().Contain("15");
        recipe.CookTime.Should().NotBeNull();
    }

    [Fact]
    public void Parse_HtmlWithJsonLd_ExtractsRecipe()
    {
        var result = _parser.Parse(HtmlWithJsonLd);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Pasta Carbonara");
        result.Recipes[0].Ingredients.Should().HaveCount(3);
        result.Recipes[0].Instructions.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_GraphArray_ParsesMultipleRecipes()
    {
        var result = _parser.Parse(GraphJson);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(2);
        result.Recipes[0].Title.Should().Be("Recipe One");
        result.Recipes[1].Title.Should().Be("Recipe Two");
    }

    [Fact]
    public void Parse_HowToSections_FlattensSteps()
    {
        var result = _parser.Parse(HowToSectionJson);
        result.Success.Should().BeTrue();
        var recipe = result.Recipes[0];
        recipe.Instructions.Should().HaveCount(4);
        recipe.Instructions[0].Text.Should().Be("Mix dry ingredients.");
        recipe.Instructions[2].Text.Should().Be("Pour into pan.");
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsNoRecipes()
    {
        var result = _parser.Parse("{ this is not valid JSON }");
        result.Recipes.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsFailed()
    {
        var result = _parser.Parse("");
        result.Success.Should().BeFalse();
    }
}
