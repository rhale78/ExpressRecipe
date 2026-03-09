using Xunit;
using FluentAssertions;
using ExpressRecipe.MealPlanningService.Services;

namespace ExpressRecipe.InventoryService.Tests.Services;

public class SeasonalProduceServiceTests
{
    private readonly SeasonalProduceService _service = new();

    #region GetInSeasonProduce Tests

    [Fact]
    public void GetInSeasonProduce_Northeast_September_ContainsTomatoCornPepper()
    {
        // Act
        List<string> produce = _service.GetInSeasonProduce("northeast", new DateOnly(2025, 9, 1));

        // Assert
        produce.Should().Contain("Tomato");
        produce.Should().Contain("Corn");
        produce.Should().Contain("Pepper");
    }

    [Fact]
    public void GetInSeasonProduce_California_April_ContainsArtichokeAvocadoStrawberry()
    {
        // Act
        List<string> produce = _service.GetInSeasonProduce("california", new DateOnly(2025, 4, 1));

        // Assert
        produce.Should().Contain("Artichoke");
        produce.Should().Contain("Avocado");
        produce.Should().Contain("Strawberry");
    }

    [Fact]
    public void GetInSeasonProduce_CaseInsensitiveRegion_ReturnsResults()
    {
        // Act
        List<string> lower  = _service.GetInSeasonProduce("northeast", new DateOnly(2025, 7, 1));
        List<string> upper  = _service.GetInSeasonProduce("NORTHEAST", new DateOnly(2025, 7, 1));
        List<string> mixed  = _service.GetInSeasonProduce("NorthEast", new DateOnly(2025, 7, 1));

        // Assert
        lower.Should().BeEquivalentTo(upper);
        lower.Should().BeEquivalentTo(mixed);
    }

    [Fact]
    public void GetInSeasonProduce_NullRegion_ReturnsEmptyList()
    {
        // Act
        List<string> produce = _service.GetInSeasonProduce(null!, new DateOnly(2025, 7, 1));

        // Assert
        produce.Should().BeEmpty();
    }

    [Fact]
    public void GetInSeasonProduce_WhitespaceRegion_ReturnsEmptyList()
    {
        // Act
        List<string> produce = _service.GetInSeasonProduce("   ", new DateOnly(2025, 7, 1));

        // Assert
        produce.Should().BeEmpty();
    }

    [Fact]
    public void GetInSeasonProduce_UnknownRegion_ReturnsEmptyList()
    {
        // Act
        List<string> produce = _service.GetInSeasonProduce("unknown_region", new DateOnly(2025, 6, 1));

        // Assert
        produce.Should().BeEmpty();
    }

    [Fact]
    public void GetInSeasonProduce_MidwestMissingMonth_ReturnsEmptyList()
    {
        // Midwest does not have entries for February
        List<string> produce = _service.GetInSeasonProduce("midwest", new DateOnly(2025, 2, 1));

        produce.Should().BeEmpty();
    }

    #endregion

    #region IsInSeason Tests

    [Fact]
    public void IsInSeason_Tomato_Northeast_January_ReturnsFalse()
    {
        // Act
        bool result = _service.IsInSeason("Tomato", "northeast", new DateOnly(2025, 1, 1));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInSeason_Tomato_Northeast_August_ReturnsTrue()
    {
        // Act
        bool result = _service.IsInSeason("Tomato", "northeast", new DateOnly(2025, 8, 1));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_CaseInsensitiveProduceName()
    {
        // Act
        bool lower = _service.IsInSeason("tomato", "northeast", new DateOnly(2025, 8, 1));
        bool upper = _service.IsInSeason("TOMATO", "northeast", new DateOnly(2025, 8, 1));

        // Assert
        lower.Should().BeTrue();
        upper.Should().BeTrue();
    }

    #endregion

    #region GetSeasonalScoreBoost Tests

    [Fact]
    public void GetSeasonalScoreBoost_TwoMatches_Northeast_August_Returns0Point10()
    {
        // Arrange — Tomato and Corn are in season in northeast August; Butter is not
        List<string> ingredients = new() { "Tomato", "Corn", "Butter" };

        // Act
        decimal boost = _service.GetSeasonalScoreBoost(ingredients, "northeast", new DateOnly(2025, 8, 1));

        // Assert
        boost.Should().Be(0.10m);
    }

    [Fact]
    public void GetSeasonalScoreBoost_SixOrMoreMatches_CappedAt0Point25()
    {
        // northeast August has: Tomato, Corn, Pepper, Eggplant, Peach, Melon, Zucchini, Basil
        List<string> ingredients = new() { "Tomato", "Corn", "Pepper", "Eggplant", "Peach", "Melon", "Zucchini", "Basil" };

        // Act
        decimal boost = _service.GetSeasonalScoreBoost(ingredients, "northeast", new DateOnly(2025, 8, 1));

        // Assert
        boost.Should().Be(0.25m);
    }

    [Fact]
    public void GetSeasonalScoreBoost_NoMatches_ReturnsZero()
    {
        // Act
        decimal boost = _service.GetSeasonalScoreBoost(new[] { "Salt", "Pepper Flakes", "Vinegar" }, "northeast", new DateOnly(2025, 8, 1));

        // Assert — "Pepper Flakes" != "Pepper" (exact case-insensitive match required)
        boost.Should().Be(0.00m);
    }

    [Fact]
    public void GetSeasonalScoreBoost_EmptyIngredients_ReturnsZero()
    {
        // Act
        decimal boost = _service.GetSeasonalScoreBoost(Array.Empty<string>(), "northeast", new DateOnly(2025, 8, 1));

        // Assert
        boost.Should().Be(0.00m);
    }

    #endregion

    #region GetFreshnessDays Tests

    [Fact]
    public void GetFreshnessDays_KnownPlant_Tomato_Returns7()
    {
        // Act
        int days = SeasonalProduceService.GetFreshnessDays("Tomato");

        // Assert
        days.Should().Be(7);
    }

    [Fact]
    public void GetFreshnessDays_KnownPlant_CaseInsensitive()
    {
        // Act
        int lower = SeasonalProduceService.GetFreshnessDays("tomato");
        int upper = SeasonalProduceService.GetFreshnessDays("TOMATO");

        // Assert
        lower.Should().Be(7);
        upper.Should().Be(7);
    }

    [Fact]
    public void GetFreshnessDays_UnknownPlant_Returns7Default()
    {
        // Act
        int days = SeasonalProduceService.GetFreshnessDays("UnknownPlant");

        // Assert
        days.Should().Be(7);
    }

    [Fact]
    public void GetFreshnessDays_Apple_Returns21()
    {
        // Act
        int days = SeasonalProduceService.GetFreshnessDays("Apple");

        // Assert
        days.Should().Be(21);
    }

    [Fact]
    public void GetFreshnessDays_Corn_Returns3()
    {
        // Act
        int days = SeasonalProduceService.GetFreshnessDays("Corn");

        // Assert
        days.Should().Be(3);
    }

    #endregion
}
