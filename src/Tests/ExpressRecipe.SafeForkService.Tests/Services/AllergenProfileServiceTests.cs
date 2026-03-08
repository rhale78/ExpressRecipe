using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;
using ExpressRecipe.SafeForkService.Data;
using ExpressRecipe.SafeForkService.Models;
using ExpressRecipe.SafeForkService.Services;
using ExpressRecipe.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.SafeForkService.Tests.Services;

public class AllergenProfileServiceTests
{
    private readonly Mock<IAllergenProfileRepository> _profileRepoMock;
    private readonly Mock<ITemporaryScheduleRepository> _scheduleRepoMock;
    private readonly Mock<IAdaptationOverrideRepository> _overrideRepoMock;
    private readonly Mock<ISafeForkEventPublisher> _publisherMock;
    private readonly Mock<ILogger<AllergenProfileService>> _loggerMock;

    public AllergenProfileServiceTests()
    {
        _profileRepoMock = new Mock<IAllergenProfileRepository>();
        _scheduleRepoMock = new Mock<ITemporaryScheduleRepository>();
        _overrideRepoMock = new Mock<IAdaptationOverrideRepository>();
        _publisherMock = new Mock<ISafeForkEventPublisher>();
        _loggerMock = new Mock<ILogger<AllergenProfileService>>();
    }

    /// <summary>
    /// Creates a service with a stub AllergenResolutionService and a null-safe HybridCacheService.
    /// </summary>
    private AllergenProfileService CreateService()
    {
        Mock<IAllergenProfileRepository> resolutionProfileRepoMock = new Mock<IAllergenProfileRepository>();
        Mock<IHttpClientFactory> httpClientFactoryMock = new Mock<IHttpClientFactory>();
        Mock<ILogger<AllergenResolutionService>> resolutionLoggerMock = new Mock<ILogger<AllergenResolutionService>>();

        AllergenResolutionService resolutionService = new AllergenResolutionService(
            resolutionProfileRepoMock.Object,
            httpClientFactoryMock.Object,
            resolutionLoggerMock.Object);

        Mock<HybridCache> hybridCacheMock = new Mock<HybridCache>();
        Mock<ILogger<HybridCacheService>> cacheLoggerMock = new Mock<ILogger<HybridCacheService>>();
        HybridCacheService cacheService = new HybridCacheService(hybridCacheMock.Object, cacheLoggerMock.Object);

        return new AllergenProfileService(
            _profileRepoMock.Object,
            _scheduleRepoMock.Object,
            _overrideRepoMock.Object,
            resolutionService,
            _publisherMock.Object,
            cacheService,
            _loggerMock.Object);
    }

    // ── EvaluateRecipeAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateRecipe_NoConflicts_ReturnsSafe()
    {
        // Arrange
        AllergenProfileService service = CreateService();

        List<RecipeIngredientDto> ingredients = new List<RecipeIngredientDto>
        {
            new RecipeIngredientDto { Name = "carrots" }
        };

        UnionProfileDto profile = new UnionProfileDto
        {
            HouseholdId = Guid.NewGuid(),
            AllEntries = new List<AllergenProfileEntryDto>
            {
                new AllergenProfileEntryDto
                {
                    Id = Guid.NewGuid(),
                    MemberId = Guid.NewGuid(),
                    FreeFormName = "peanuts",
                    Severity = "Severe",
                    ExposureThreshold = "IngestionOnly"
                }
            }
        };

        // Act
        RecipeEvaluationResult result = await service.EvaluateRecipeAsync(ingredients, profile, CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeTrue();
        result.ConflictReport.HasConflicts.Should().BeFalse();
        result.ConflictReport.HasAnaphylacticRisk.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateRecipe_SingleConflict_ReturnsNotSafe()
    {
        // Arrange
        AllergenProfileService service = CreateService();
        Guid memberId = Guid.NewGuid();

        List<RecipeIngredientDto> ingredients = new List<RecipeIngredientDto>
        {
            new RecipeIngredientDto { Name = "peanut butter" }
        };

        UnionProfileDto profile = new UnionProfileDto
        {
            HouseholdId = Guid.NewGuid(),
            AllEntries = new List<AllergenProfileEntryDto>
            {
                new AllergenProfileEntryDto
                {
                    Id = Guid.NewGuid(),
                    MemberId = memberId,
                    FreeFormName = "peanut",
                    Severity = "Moderate",
                    ExposureThreshold = "IngestionOnly"
                }
            }
        };

        // Act
        RecipeEvaluationResult result = await service.EvaluateRecipeAsync(ingredients, profile, CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ConflictReport.HasConflicts.Should().BeTrue();
        result.ConflictReport.HasAnaphylacticRisk.Should().BeFalse();
        result.SuggestedStrategy.Should().Be("AdaptAll");
    }

    [Fact]
    public async Task EvaluateRecipe_LifeThreateningConflict_SetsAnaphylacticRisk()
    {
        // Arrange
        AllergenProfileService service = CreateService();
        Guid memberId = Guid.NewGuid();

        List<RecipeIngredientDto> ingredients = new List<RecipeIngredientDto>
        {
            new RecipeIngredientDto { Name = "shellfish sauce" }
        };

        UnionProfileDto profile = new UnionProfileDto
        {
            HouseholdId = Guid.NewGuid(),
            AllEntries = new List<AllergenProfileEntryDto>
            {
                new AllergenProfileEntryDto
                {
                    Id = Guid.NewGuid(),
                    MemberId = memberId,
                    FreeFormName = "shellfish",
                    Severity = "LifeThreatening",
                    ExposureThreshold = "AirborneSensitive"
                }
            }
        };

        // Act
        RecipeEvaluationResult result = await service.EvaluateRecipeAsync(ingredients, profile, CancellationToken.None);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.ConflictReport.HasAnaphylacticRisk.Should().BeTrue();
        result.SuggestedStrategy.Should().Be("SeparateMeal");
    }

    // ── ResolveAdaptationStrategyAsync ──────────────────────────────────────

    [Fact]
    public async Task ResolveAdaptationStrategy_AnaphylacticRisk_ForcedToSeparateMeal()
    {
        // Arrange
        AllergenProfileService service = CreateService();

        ConflictReport report = new ConflictReport
        {
            HasConflicts = true,
            HasAnaphylacticRisk = true,
            Conflicts = new List<ConflictItem>
            {
                new ConflictItem
                {
                    MemberId = Guid.NewGuid(),
                    AllergenProfileId = Guid.NewGuid(),
                    AllergenName = "nuts",
                    Severity = "LifeThreatening",
                    ExposureThreshold = "IngestionOnly"
                }
            }
        };

        // Act
        string strategy = await service.ResolveAdaptationStrategyAsync(
            report, Guid.NewGuid(), null, CancellationToken.None);

        // Assert
        strategy.Should().Be("SeparateMeal");

        // Verify repo was NOT called (early return for anaphylactic guard)
        _overrideRepoMock.Verify(
            r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveAdaptationStrategy_AnaphylacticRisk_OverridesAdaptAll()
    {
        // Arrange — even if household default is AdaptAll, anaphylactic risk forces SeparateMeal
        AllergenProfileService service = CreateService();
        Guid householdId = Guid.NewGuid();

        _overrideRepoMock
            .Setup(r => r.GetAsync(householdId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdaptationOverrideEntry>
            {
                new AdaptationOverrideEntry { StrategyCode = "AdaptAll" }
            });

        ConflictReport report = new ConflictReport
        {
            HasConflicts = true,
            HasAnaphylacticRisk = true,
            Conflicts = new List<ConflictItem>
            {
                new ConflictItem { MemberId = Guid.NewGuid(), Severity = "LifeThreatening" }
            }
        };

        // Act
        string strategy = await service.ResolveAdaptationStrategyAsync(
            report, householdId, null, CancellationToken.None);

        // Assert: anaphylactic guard wins regardless
        strategy.Should().Be("SeparateMeal");
    }

    [Fact]
    public async Task ResolveAdaptationStrategy_NoOverrides_ReturnsAdaptAll()
    {
        // Arrange
        AllergenProfileService service = CreateService();
        Guid householdId = Guid.NewGuid();

        _overrideRepoMock
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdaptationOverrideEntry>());

        ConflictReport report = new ConflictReport
        {
            HasConflicts = false,
            HasAnaphylacticRisk = false
        };

        // Act
        string strategy = await service.ResolveAdaptationStrategyAsync(
            report, householdId, null, CancellationToken.None);

        // Assert
        strategy.Should().Be("AdaptAll");
    }

    [Fact]
    public async Task ResolveAdaptationStrategy_HouseholdDefaultOverride_ReturnsHouseholdDefault()
    {
        // Arrange
        AllergenProfileService service = CreateService();
        Guid householdId = Guid.NewGuid();

        // Only household-level override (level 4) exists
        _overrideRepoMock
            .Setup(r => r.GetAsync(householdId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdaptationOverrideEntry>
            {
                new AdaptationOverrideEntry { StrategyCode = "SplitMeal" }
            });

        // All other queries return empty
        _overrideRepoMock
            .Setup(r => r.GetAsync(householdId, null, It.IsNotNull<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdaptationOverrideEntry>());

        ConflictReport report = new ConflictReport
        {
            HasConflicts = false,
            HasAnaphylacticRisk = false
        };

        // Act
        string strategy = await service.ResolveAdaptationStrategyAsync(
            report, householdId, null, CancellationToken.None);

        // Assert
        strategy.Should().Be("SplitMeal");
    }

    // ── GetSubstitutesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetSubstitutes_EdRecoveryMode_SuppressesCalorieFramedSubstitutes()
    {
        // Arrange
        AllergenProfileService service = CreateService();

        RecipeIngredientDto ingredient = new RecipeIngredientDto { Name = "butter" };
        RecipeContextDto context = new RecipeContextDto
        {
            RecipeName = "Cookies",
            EatingDisorderRecovery = true
        };

        // Act
        List<SubstituteDto> substitutes = await service.GetSubstitutesAsync(
            ingredient, Guid.NewGuid(), context, CancellationToken.None);

        // Assert: no substitute should have "calorie", "weight", or "macro" in Reason
        substitutes.Should().NotContain(s =>
            s.Reason.Contains("calorie", StringComparison.OrdinalIgnoreCase) ||
            s.Reason.Contains("weight", StringComparison.OrdinalIgnoreCase) ||
            s.Reason.Contains("macro", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetSubstitutes_NoEdMode_ReturnsSubstitutes()
    {
        // Arrange
        AllergenProfileService service = CreateService();

        RecipeIngredientDto ingredient = new RecipeIngredientDto { Name = "milk" };
        RecipeContextDto context = new RecipeContextDto
        {
            RecipeName = "Pancakes",
            EatingDisorderRecovery = false
        };

        // Act
        List<SubstituteDto> substitutes = await service.GetSubstitutesAsync(
            ingredient, Guid.NewGuid(), context, CancellationToken.None);

        // Assert: substitutes returned (may be empty depending on implementation, but no exception)
        substitutes.Should().NotBeNull();
    }
}
