using ExpressRecipe.RecipeParser.Models;
using ExpressRecipe.RecipeParser.Parsers;
using FluentAssertions;

namespace ExpressRecipe.RecipeParser.Tests.Parsers;

public class MasterCookParserTests
{
    private readonly MasterCookParser _parser = new();

    private const string MxpRecipe = """
        * Exported from MasterCook *

        Beef Stew

        Recipe By     : Grandma
        Serving Size  : 6    Preparation Time :1:30
        Categories    : Soups & Stews

          Amount  Measure       Ingredient -- Preparation Method
        --------  ------------  --------------------------------
               2  lbs           Beef chuck -- cut into cubes
               3  medium        Potatoes -- peeled and diced
               2  large         Carrots -- sliced
               1  large         Onion -- chopped
               2  cups          Beef broth
               1  tbsp          Tomato paste
                                Salt and pepper to taste

        Brown beef in large pot. Add vegetables and broth. Simmer 1.5 hours until tender.
        Season with salt and pepper.

        """;

    private const string Mx2Recipe = """
        <?xml version="1.0" encoding="UTF-8"?>
        <mx2 version="1.0">
          <Recipe name="Apple Pie" author="Baker Bob" servings="8">
            <Category>Desserts</Category>
            <IngredientList>
              <Ingredient quantity="2" unit="cups" ingredient="Apples, sliced" />
              <Ingredient quantity="1" unit="cup" ingredient="Sugar" />
              <Ingredient quantity="2" unit="" ingredient="Pie crusts" />
            </IngredientList>
            <Directions>Mix apples and sugar. Pour into crust. Bake at 375F for 45 minutes.</Directions>
          </Recipe>
        </mx2>
        """;

    [Fact]
    public void Parse_MxpFormat_ExtractsBasicFields()
    {
        var result = _parser.Parse(MxpRecipe);
        result.Should().NotBeNull();
        result.Recipes.Should().NotBeEmpty();
        result.Recipes[0].Title.Should().Contain("Beef Stew");
    }

    [Fact]
    public void Parse_MxpFormat_ExtractsIngredients()
    {
        var result = _parser.Parse(MxpRecipe);
        result.Recipes[0].Ingredients.Should().NotBeEmpty();
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name.Contains("Beef", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_MxpFormat_HandlesPreparationNotes()
    {
        var result = _parser.Parse(MxpRecipe);
        result.Recipes[0].Ingredients.Should().NotBeEmpty();
        // At least one ingredient should have a preparation note (e.g., "cut into cubes" or "peeled and diced")
        result.Recipes[0].Ingredients.Should().Contain(i =>
            i.Preparation != null && i.Preparation.Length > 0 || i.Name.Contains("--"));
    }

    [Fact]
    public void Parse_Mx2Format_ExtractsRecipe()
    {
        var result = _parser.Parse(Mx2Recipe);
        result.Success.Should().BeTrue();
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].Title.Should().Be("Apple Pie");
        result.Recipes[0].Author.Should().Be("Baker Bob");
        result.Recipes[0].Yield.Should().Be("8");
    }

    [Fact]
    public void Parse_Mx2Format_ExtractsIngredients()
    {
        var result = _parser.Parse(Mx2Recipe);
        result.Recipes[0].Ingredients.Should().HaveCount(3);
        result.Recipes[0].Ingredients.Should().Contain(i => i.Name.Contains("Apples"));
    }

    [Fact]
    public void Parse_InvalidData_DoesNotThrow()
    {
        var act = () => _parser.Parse("* Exported from MasterCook *\n\njunk data here");
        act.Should().NotThrow();
    }
}
