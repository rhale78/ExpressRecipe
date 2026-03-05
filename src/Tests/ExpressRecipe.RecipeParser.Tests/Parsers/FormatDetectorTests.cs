using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class FormatDetectorTests
{
    [Fact]
    public void Detect_MmfExtension_ReturnsMealMaster()
    {
        FormatDetector.Detect("any text", "mmf").Should().Be("MealMaster");
    }

    [Fact]
    public void Detect_CookExtension_ReturnsCookLang()
    {
        FormatDetector.Detect("any text", "cook").Should().Be("CookLang");
    }

    [Fact]
    public void Detect_MxpExtension_ReturnsMasterCook()
    {
        FormatDetector.Detect("any text", "mxp").Should().Be("MasterCook");
    }

    [Fact]
    public void Detect_MmmmmContent_ReturnsMealMaster()
    {
        FormatDetector.Detect("MMMMM----- Recipe").Should().Be("MealMaster");
    }

    [Fact]
    public void Detect_JsonObject_ReturnsJson()
    {
        FormatDetector.Detect("{\"title\": \"test\"}").Should().Be("Json");
    }

    [Fact]
    public void Detect_JsonArray_ReturnsJson()
    {
        FormatDetector.Detect("[{\"title\": \"test\"}]").Should().Be("Json");
    }

    [Fact]
    public void Detect_RecipeMLXml_ReturnsRecipeML()
    {
        FormatDetector.Detect("<?xml version=\"1.0\"?><recipeml>").Should().Be("RecipeML");
    }

    [Fact]
    public void Detect_YamlWithIngredients_ReturnsYaml()
    {
        FormatDetector.Detect("title: Test\ningredients:\n  - flour").Should().Be("Yaml");
    }

    [Fact]
    public void Detect_EmptyText_ReturnsNull()
    {
        FormatDetector.Detect("").Should().BeNull();
    }

    [Fact]
    public void Detect_RxolXml_ReturnsRxol()
    {
        FormatDetector.Detect("<RXOL><Recipe>").Should().Be("Rxol");
    }
}
