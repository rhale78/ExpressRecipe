using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace ExpressRecipe.MealPlanningService.Tests.Services;

/// <summary>
/// Tests for <see cref="MealSuggestionService"/> scoring and suggestion logic.
/// External HTTP calls are intercepted with a stub handler.
/// </summary>
public class MealSuggestionServiceTests
{
    private readonly Mock<IMealPlanningRepository> _repoMock = new();
    private readonly IConfiguration _config = new ConfigurationBuilder().Build();

    // ── Scoring unit tests ────────────────────────────────────────────────────

    [Fact]
    public void ComputeInventoryScore_Slider0_FullMatch_Returns30()
    {
        decimal score = MealSuggestionService.ComputeInventoryScore(1.0m, 0);
        score.Should().Be(30m);
    }

    [Fact]
    public void ComputeInventoryScore_Slider0_NoMatch_Returns3()
    {
        decimal score = MealSuggestionService.ComputeInventoryScore(0m, 0);
        score.Should().Be(3m);
    }

    [Fact]
    public void ComputeInventoryScore_Slider100_AnyMatch_Returns0()
    {
        decimal fullMatch = MealSuggestionService.ComputeInventoryScore(1.0m, 100);
        decimal noMatch   = MealSuggestionService.ComputeInventoryScore(0m, 100);
        fullMatch.Should().Be(0m);
        noMatch.Should().Be(0m);
    }

    [Fact]
    public void ComputeModeBonus_TriedAndTrue_HighCookCount_ScoresHigherThanLow()
    {
        decimal highCount = MealSuggestionService.ComputeModeBonus(SuggestionModes.TriedAndTrue, 8, 5m);
        decimal lowCount  = MealSuggestionService.ComputeModeBonus(SuggestionModes.TriedAndTrue, 1, 5m);
        highCount.Should().BeGreaterThan(lowCount);
    }

    [Fact]
    public void ComputeModeBonus_SomethingNew_CookCount0_ScoresHigherThanCookCount3()
    {
        decimal neverCooked  = MealSuggestionService.ComputeModeBonus(SuggestionModes.SomethingNew, 0, 5m);
        decimal cookedBefore = MealSuggestionService.ComputeModeBonus(SuggestionModes.SomethingNew, 3, 5m);
        neverCooked.Should().BeGreaterThan(cookedBefore);
    }

    [Fact]
    public void ComputeModeBonus_Balanced_Returns0()
    {
        decimal bonus = MealSuggestionService.ComputeModeBonus(SuggestionModes.Balanced, 5, 5m);
        bonus.Should().Be(0m);
    }

    // ── SuggestAsync integration tests (mocked HTTP) ──────────────────────────

    [Fact]
    public async Task SuggestAsync_TriedAndTrue_RecipeCooked8TimesRanksAboveCooked1Time()
    {
        Guid userId     = Guid.NewGuid();
        Guid recipe8Id  = Guid.NewGuid();
        Guid recipe1Id  = Guid.NewGuid();

        List<CandidateDto> candidates = new()
        {
            new CandidateDto(recipe8Id, "Pasta Carbonara", 30, 4.0m, new(), new()),
            new CandidateDto(recipe1Id, "Chicken Stir Fry", 25, 4.0m, new(), new())
        };

        // Both rated 5 by user; recipe8 cooked 8 times, recipe1 cooked once
        _repoMock.Setup(r => r.GetUserRecipeRatingsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, decimal>
            {
                [recipe8Id] = 5m,
                [recipe1Id] = 5m
            });
        _repoMock.Setup(r => r.GetUserRecipeCookCountsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, int>
            {
                [recipe8Id] = 8,
                [recipe1Id] = 1
            });
        _repoMock.Setup(r => r.GetRecentlyCookedRecipeIdsAsync(userId, It.IsAny<int>(), default))
            .ReturnsAsync(new List<Guid>());

        MealSuggestionService service = CreateService(candidates);

        SuggestionRequest request = new()
        {
            UserId          = userId,
            SuggestionMode  = SuggestionModes.TriedAndTrue,
            Count           = 10,
            ExcludeRecentDays = false
        };

        List<MealSuggestion> results = await service.SuggestAsync(request);

        results.Should().NotBeEmpty();
        int recipe8Rank = results.FindIndex(r => r.RecipeId == recipe8Id);
        int recipe1Rank = results.FindIndex(r => r.RecipeId == recipe1Id);
        recipe8Rank.Should().BeLessThan(recipe1Rank, "recipe cooked 8 times should rank above cooked 1 time");
    }

    [Fact]
    public async Task SuggestAsync_SomethingNew_CookCount0_RanksAboveCookCount3()
    {
        Guid userId       = Guid.NewGuid();
        Guid newRecipeId  = Guid.NewGuid();
        Guid oldRecipeId  = Guid.NewGuid();

        List<CandidateDto> candidates = new()
        {
            new CandidateDto(newRecipeId, "New Recipe", 20, 4.5m, new(), new()),
            new CandidateDto(oldRecipeId, "Old Favourite", 20, 5.0m, new(), new())
        };

        _repoMock.Setup(r => r.GetUserRecipeRatingsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, decimal>
            {
                [oldRecipeId] = 5m  // higher user rating for old favourite
            });
        _repoMock.Setup(r => r.GetUserRecipeCookCountsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, int>
            {
                [oldRecipeId] = 3
                // newRecipeId not in dict → CookCount = 0
            });
        _repoMock.Setup(r => r.GetRecentlyCookedRecipeIdsAsync(userId, It.IsAny<int>(), default))
            .ReturnsAsync(new List<Guid>());

        MealSuggestionService service = CreateService(candidates);

        SuggestionRequest request = new()
        {
            UserId          = userId,
            SuggestionMode  = SuggestionModes.SomethingNew,
            Count           = 10,
            ExcludeRecentDays = false
        };

        List<MealSuggestion> results = await service.SuggestAsync(request);

        results.Should().HaveCount(2);
        results[0].RecipeId.Should().Be(newRecipeId,
            "CookCount=0 recipe should rank above CookCount=3 even if ratings differ");
    }

    [Fact]
    public async Task SuggestAsync_AllergenUnsafe_ExcludedFromResults()
    {
        Guid userId        = Guid.NewGuid();
        Guid safeRecipeId  = Guid.NewGuid();
        Guid unsafeRecipeId = Guid.NewGuid();

        List<CandidateDto> candidates = new()
        {
            new CandidateDto(safeRecipeId,   "Safe Recipe",   20, 4.0m, new(), new()),
            new CandidateDto(unsafeRecipeId, "Unsafe Recipe", 20, 5.0m, new(), new())
        };

        SetupEmptyRepoResponses(userId);

        // SafeFork returns unsafe=false for unsafeRecipeId
        Dictionary<string, bool> safeForkResponse = new()
        {
            [safeRecipeId.ToString()]   = true,
            [unsafeRecipeId.ToString()] = false
        };

        MealSuggestionService service = CreateService(candidates, safeForkOverride: safeForkResponse);

        SuggestionRequest request = new()
        {
            UserId          = userId,
            Count           = 10,
            ExcludeRecentDays = false
        };

        List<MealSuggestion> results = await service.SuggestAsync(request);

        results.Should().OnlyContain(r => r.RecipeId == safeRecipeId);
        results.Should().NotContain(r => r.RecipeId == unsafeRecipeId);
    }

    [Fact]
    public async Task SuggestAsync_ExcludeRecentDays_RecipeCookedWithinCutoff_NotReturned()
    {
        Guid userId        = Guid.NewGuid();
        Guid recentId      = Guid.NewGuid();
        Guid eligibleId    = Guid.NewGuid();

        List<CandidateDto> candidates = new()
        {
            new CandidateDto(recentId,   "Recent Recipe",   20, 4.0m, new(), new()),
            new CandidateDto(eligibleId, "Eligible Recipe", 20, 4.0m, new(), new())
        };

        _repoMock.Setup(r => r.GetUserRecipeRatingsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, decimal>());
        _repoMock.Setup(r => r.GetUserRecipeCookCountsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, int>());
        // recentId was cooked 10 days ago (within 14-day cutoff)
        _repoMock.Setup(r => r.GetRecentlyCookedRecipeIdsAsync(userId, 14, default))
            .ReturnsAsync(new List<Guid> { recentId });

        MealSuggestionService service = CreateService(candidates);

        SuggestionRequest request = new()
        {
            UserId            = userId,
            ExcludeRecentDays = true,
            RecentDaysCutoff  = 14,
            Count             = 10
        };

        List<MealSuggestion> results = await service.SuggestAsync(request);

        results.Should().OnlyContain(r => r.RecipeId == eligibleId);
    }

    [Fact]
    public async Task SuggestAsync_ExcludeRecentDays_RecipeCookedOutsideCutoff_IsReturned()
    {
        Guid userId     = Guid.NewGuid();
        Guid recipeId   = Guid.NewGuid();

        List<CandidateDto> candidates = new()
        {
            new CandidateDto(recipeId, "Older Recipe", 20, 4.0m, new(), new())
        };

        _repoMock.Setup(r => r.GetUserRecipeRatingsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, decimal>());
        _repoMock.Setup(r => r.GetUserRecipeCookCountsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, int>());
        // Recipe cooked 15 days ago — outside the 14-day cutoff → NOT in recent list
        _repoMock.Setup(r => r.GetRecentlyCookedRecipeIdsAsync(userId, 14, default))
            .ReturnsAsync(new List<Guid>());   // empty: 15-day-old recipe not in window

        MealSuggestionService service = CreateService(candidates);

        SuggestionRequest request = new()
        {
            UserId            = userId,
            ExcludeRecentDays = true,
            RecentDaysCutoff  = 14,
            Count             = 10
        };

        List<MealSuggestion> results = await service.SuggestAsync(request);

        results.Should().ContainSingle(r => r.RecipeId == recipeId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupEmptyRepoResponses(Guid userId)
    {
        _repoMock.Setup(r => r.GetUserRecipeRatingsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, decimal>());
        _repoMock.Setup(r => r.GetUserRecipeCookCountsAsync(userId, default))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _repoMock.Setup(r => r.GetRecentlyCookedRecipeIdsAsync(userId, It.IsAny<int>(), default))
            .ReturnsAsync(new List<Guid>());
    }

    /// <summary>
    /// Builds a <see cref="MealSuggestionService"/> whose HTTP calls are intercepted.
    /// </summary>
    private MealSuggestionService CreateService(
        List<CandidateDto> candidates,
        Dictionary<string, bool>? safeForkOverride = null)
    {
        // Stub handlers for each named client
        Mock<HttpMessageHandler> recipeHandler = new();
        recipeHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(candidates)
            });

        Mock<HttpMessageHandler> inventoryHandler = new();
        inventoryHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<object>())
            });

        Dictionary<string, bool> safeForkResult = safeForkOverride
            ?? candidates.ToDictionary(c => c.Id.ToString(), _ => true);

        Mock<HttpMessageHandler> safeForkHandler = new();
        safeForkHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(safeForkResult)
            });

        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("RecipeService"))
            .Returns(new HttpClient(recipeHandler.Object));
        factory.Setup(f => f.CreateClient("InventoryService"))
            .Returns(new HttpClient(inventoryHandler.Object));
        factory.Setup(f => f.CreateClient("SafeForkService"))
            .Returns(new HttpClient(safeForkHandler.Object));

        return new MealSuggestionService(
            factory.Object,
            _repoMock.Object,
            _config,
            NullLogger<MealSuggestionService>.Instance,
            hybridCache: null);
    }

    /// <summary>
    /// Minimal DTO mirroring what the RecipeService returns for candidates.
    /// Matches the private <c>RecipeCandidate</c> record shape in the service.
    /// </summary>
    private record CandidateDto(
        Guid Id,
        string Name,
        int? CookTimeMinutes,
        decimal? GlobalAverageRating,
        List<string> Tags,
        List<object> Ingredients);
}
