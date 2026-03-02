using ExpressRecipe.AIService.Services;
using ExpressRecipe.Client.Shared.Models.AI;
using FluentAssertions;

namespace ExpressRecipe.AIService.Tests;

/// <summary>
/// Unit tests for <see cref="RecipeTextParser"/> — the regex-based fallback parser
/// used when Ollama is unavailable.
/// </summary>
public class RecipeTextParserTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // ExtractRecipeLocally — basic extraction
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractRecipeLocally_EmptyText_ReturnsEmptyDto()
    {
        var result = RecipeTextParser.ExtractRecipeLocally(string.Empty);

        result.Title.Should().BeEmpty();
        result.Ingredients.Should().BeEmpty();
        result.Instructions.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRecipeLocally_TitleIsFirstNonEmptyLine()
    {
        const string text = "\n\nChocolate Chip Cookies\nIngredients:\n2 cups flour";

        var result = RecipeTextParser.ExtractRecipeLocally(text);

        result.Title.Should().Be("Chocolate Chip Cookies");
    }

    [Theory]
    [InlineData("Serves 4", 4)]
    [InlineData("Makes 12 cookies", 12)]
    [InlineData("Yields 6 servings", 6)]
    [InlineData("Servings: 8", 8)]
    public void ExtractRecipeLocally_ServingsPatterns_ParseCorrectly(string text, int expectedServings)
    {
        // Prepend a title so the parser has a first line
        var result = RecipeTextParser.ExtractRecipeLocally("My Recipe\n" + text);

        result.Servings.Should().Be(expectedServings);
    }

    [Fact]
    public void ExtractRecipeLocally_NoServingsKeyword_DefaultsToFour()
    {
        var result = RecipeTextParser.ExtractRecipeLocally("Simple Cake\n1 cup flour");

        result.Servings.Should().Be(4);
    }

    [Theory]
    [InlineData("Prep time: 15 min", 15)]
    [InlineData("Prep: 20 minutes", 20)]
    [InlineData("30 min prep", 30)]
    public void ExtractRecipeLocally_PrepTimePatterns_ParseCorrectly(string text, int expectedMinutes)
    {
        var result = RecipeTextParser.ExtractRecipeLocally("My Recipe\n" + text);

        result.PrepTimeMinutes.Should().Be(expectedMinutes);
    }

    [Theory]
    [InlineData("Cook time: 45 min", 45)]
    [InlineData("Bake time: 30 minutes", 30)]
    [InlineData("60 min cook", 60)]
    public void ExtractRecipeLocally_CookTimePatterns_ParseCorrectly(string text, int expectedMinutes)
    {
        var result = RecipeTextParser.ExtractRecipeLocally("My Recipe\n" + text);

        result.CookTimeMinutes.Should().Be(expectedMinutes);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Difficulty
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("This is an easy recipe for beginners.", "Easy")]
    [InlineData("A challenging and complex dish.", "Hard")]
    [InlineData("A tasty dish.", "Medium")]
    public void ExtractRecipeLocally_DifficultyKeywords_Detected(string bodyText, string expectedDifficulty)
    {
        var result = RecipeTextParser.ExtractRecipeLocally("Recipe Title\n" + bodyText);

        result.Difficulty.Should().Be(expectedDifficulty);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cuisine & Category detection
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Italian pasta dish", "Italian")]
    [InlineData("A classic Mexican taco", "Mexican")]
    [InlineData("Traditional Japanese ramen", "Japanese")]
    public void ExtractRecipeLocally_CuisineKeyword_Detected(string bodyText, string expectedCuisine)
    {
        var result = RecipeTextParser.ExtractRecipeLocally("Recipe\n" + bodyText);

        result.Cuisine.Should().Be(expectedCuisine);
    }

    [Theory]
    [InlineData("This chocolate cake is a great dessert", "Dessert")]
    [InlineData("Tomato soup for a cold day", "Soup")]
    [InlineData("Fresh garden salad", "Salad")]
    [InlineData("Homemade bread recipe", "Bread")]
    [InlineData("Pancake breakfast for the family", "Breakfast")]
    public void ExtractRecipeLocally_CategoryKeyword_Detected(string bodyText, string expectedCategory)
    {
        var result = RecipeTextParser.ExtractRecipeLocally("Recipe\n" + bodyText);

        result.Category.Should().Be(expectedCategory);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dietary labels
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("This is a vegetarian dish", "Vegetarian")]
    [InlineData("Fully vegan meal", "Vegan")]
    [InlineData("Gluten-free option available", "Gluten-Free")]
    [InlineData("Dairy-free alternative", "Dairy-Free")]
    [InlineData("Keto friendly recipe", "Keto")]
    [InlineData("Paleo diet approved", "Paleo")]
    public void ExtractRecipeLocally_DietaryKeyword_PopulatesDietaryInfo(string bodyText, string expectedDiet)
    {
        var result = RecipeTextParser.ExtractRecipeLocally("Recipe\n" + bodyText);

        result.DietaryInfo.Should().Contain(expectedDiet);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Ingredient & instruction section parsing
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractRecipeLocally_IngredientsSection_ParsesAllIngredients()
    {
        const string text = """
            Chocolate Chip Cookies
            Ingredients:
            2 cups flour
            1 tsp vanilla
            3 oz chocolate chips
            Instructions:
            1. Mix ingredients.
            2. Bake at 350°F.
            """;

        var result = RecipeTextParser.ExtractRecipeLocally(text);

        result.Ingredients.Should().HaveCount(3);
        result.Ingredients.Should().Contain(i => i.Name == "flour" && i.Quantity == "2" && i.Unit == "cups");
    }

    [Fact]
    public void ExtractRecipeLocally_InstructionsSection_ParsesAllSteps()
    {
        const string text = """
            Cookie Recipe
            Ingredients:
            1 cup sugar
            Instructions:
            1. Preheat oven to 350F.
            2. Mix all ingredients.
            3. Bake for 12 minutes.
            """;

        var result = RecipeTextParser.ExtractRecipeLocally(text);

        result.Instructions.Should().HaveCount(3);
        result.Instructions[0].Should().Be("Preheat oven to 350F.");
    }

    [Fact]
    public void ExtractRecipeLocally_FullRecipe_ConfidenceIsHigh()
    {
        const string text = """
            Chocolate Chip Cookies
            Prep time: 15 min
            Cook time: 12 min
            Serves 24
            Ingredients:
            2 cups flour
            1 tsp vanilla
            Instructions:
            1. Mix flour and vanilla.
            2. Bake at 350F for 12 minutes.
            """;

        var result = RecipeTextParser.ExtractRecipeLocally(text);

        result.ConfidenceScore.Should().BeGreaterThan(0.5);
        result.ConfidenceScore.Should().BeLessThanOrEqualTo(0.85, "regex fallback is capped at 0.85");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TryParseIngredientLine
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2 cups flour", "2", "cups", "flour")]
    [InlineData("1 tsp salt", "1", "tsp", "salt")]
    [InlineData("3 oz dark chocolate", "3", "oz", "dark chocolate")]
    [InlineData("0.5 lb ground beef", "0.5", "lb", "ground beef")]
    public void TryParseIngredientLine_ValidLine_ParsesCorrectly(
        string line, string qty, string unit, string name)
    {
        var result = RecipeTextParser.TryParseIngredientLine(line);

        result.Should().NotBeNull();
        result!.Quantity.Should().Be(qty);
        result.Unit.Should().Be(unit);
        result.Name.Should().Be(name);
    }

    [Fact]
    public void TryParseIngredientLine_FractionQuantity_Parses()
    {
        var result = RecipeTextParser.TryParseIngredientLine("1 1/2 cups butter");

        result.Should().NotBeNull();
        result!.Quantity.Should().Be("1 1/2");
        result.Unit.Should().Be("cups");
        result.Name.Should().Be("butter");
    }

    [Fact]
    public void TryParseIngredientLine_WithNotes_SeparatesNameAndNotes()
    {
        var result = RecipeTextParser.TryParseIngredientLine("2 cups flour, sifted");

        result.Should().NotBeNull();
        result!.Name.Should().Be("flour");
        result.Notes.Should().Be("sifted");
    }

    [Theory]
    [InlineData("just flour")]                 // no quantity
    [InlineData("2 fistfuls flour")]           // unknown unit
    [InlineData("Mix all the dry ingredients")] // instruction line
    public void TryParseIngredientLine_InvalidLine_ReturnsNull(string line)
    {
        var result = RecipeTextParser.TryParseIngredientLine(line);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ExtractFirstJsonObject
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractFirstJsonObject_PureJson_ReturnsSameString()
    {
        const string json = """{"title":"Test","servings":4}""";

        var result = RecipeTextParser.ExtractFirstJsonObject(json);

        result.Should().Be(json);
    }

    [Fact]
    public void ExtractFirstJsonObject_JsonWithSurroundingText_ExtractsOnlyJson()
    {
        const string response = """
            Here is the extracted recipe:
            {"title":"Pasta","servings":4}
            Let me know if you need more.
            """;

        var result = RecipeTextParser.ExtractFirstJsonObject(response);

        result.Should().Be("""{"title":"Pasta","servings":4}""");
    }

    [Fact]
    public void ExtractFirstJsonObject_NestedObjects_ExtractsOutermostObject()
    {
        const string json = """{"title":"Test","meta":{"author":"Alice"}}""";

        var result = RecipeTextParser.ExtractFirstJsonObject(json);

        result.Should().Be(json);
    }

    [Fact]
    public void ExtractFirstJsonObject_NoJson_ReturnsNull()
    {
        var result = RecipeTextParser.ExtractFirstJsonObject("No JSON here at all");

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractFirstJsonObject_UnterminatedJson_ReturnsNull()
    {
        var result = RecipeTextParser.ExtractFirstJsonObject("""{"title":"oops""");

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractFirstJsonObject_JsonWithEscapedQuotes_ParsesCorrectly()
    {
        const string json = """{"title":"Mac \"n\" Cheese","servings":4}""";

        var result = RecipeTextParser.ExtractFirstJsonObject(json);

        result.Should().Be(json);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TryParseAiExtractionResponse
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryParseAiExtractionResponse_ValidJson_PopulatesDto()
    {
        const string aiResponse = """
            {"title":"Chocolate Cake","servings":8,"prepTimeMinutes":20,"cookTimeMinutes":40,
             "difficulty":"Medium","ingredients":[],"instructions":[],"confidenceScore":0.95}
            """;

        var result = RecipeTextParser.TryParseAiExtractionResponse(aiResponse);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Chocolate Cake");
        result.Servings.Should().Be(8);
        result.ConfidenceScore.Should().BeApproximately(0.95, 0.001);
    }

    [Fact]
    public void TryParseAiExtractionResponse_InvalidJson_ReturnsNull()
    {
        var result = RecipeTextParser.TryParseAiExtractionResponse("not json at all");

        result.Should().BeNull();
    }

    [Fact]
    public void TryParseAiExtractionResponse_JsonWithProse_StillExtractsDto()
    {
        const string aiResponse = """
            Here's the extracted recipe:
            {"title":"Tacos","servings":4,"ingredients":[],"instructions":[],"confidenceScore":0.88}
            That should work!
            """;

        var result = RecipeTextParser.TryParseAiExtractionResponse(aiResponse);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Tacos");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DetectCuisine / DetectCategory (standalone)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DetectCuisine_NoCuisineKeyword_ReturnsNull()
    {
        var result = RecipeTextParser.DetectCuisine("Just a plain recipe with no origin");

        result.Should().BeNull();
    }

    [Fact]
    public void DetectCategory_NoCategoryKeyword_ReturnsNull()
    {
        var result = RecipeTextParser.DetectCategory("Plain grilled chicken");

        result.Should().BeNull();
    }
}
