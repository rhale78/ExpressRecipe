using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.UserService.Tests.Services;

public class AllergyDifferentialAnalyzerTests
{
    private readonly Mock<IAllergyIncidentRepository> _repoMock;
    private readonly Mock<IIngredientFetchService> _ingredientsMock;
    private readonly AllergyDifferentialAnalyzer _analyzer;

    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _memberId    = Guid.NewGuid();
    private const string MemberName    = "Alice";

    public AllergyDifferentialAnalyzerTests()
    {
        _repoMock        = new Mock<IAllergyIncidentRepository>();
        _ingredientsMock = new Mock<IIngredientFetchService>();

        // Default: empty safe set
        _ingredientsMock
            .Setup(s => s.GetSafeIngredientSetAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        _analyzer = new AllergyDifferentialAnalyzer(
            _repoMock.Object,
            _ingredientsMock.Object,
            NullLogger<AllergyDifferentialAnalyzer>.Instance);
    }

    // ── Frequency / severity math ────────────────────────────────────────────

    [Fact]
    public async Task RunForMemberAsync_BothProductsHaveWheat_WheatHasFullFrequency()
    {
        // Arrange — one incident, two reaction products both with wheat
        Guid incidentId = Guid.NewGuid();
        Guid prodA      = Guid.NewGuid();
        Guid prodB      = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetReactionProductsForMemberAsync(_householdId, _memberId, default))
            .ReturnsAsync(new List<AllergyIncidentProductRow>
            {
                new() { IncidentId = incidentId, ProductId = prodA, HadReaction = true, SeverityLevel = "Rash" },
                new() { IncidentId = incidentId, ProductId = prodB, HadReaction = true, SeverityLevel = "Rash" }
            });

        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(prodA, default))
            .ReturnsAsync(new List<string> { "wheat", "milk" });
        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(prodB, default))
            .ReturnsAsync(new List<string> { "wheat", "eggs" });

        // Act
        await _analyzer.RunForMemberAsync(_householdId, _memberId, MemberName);

        // Assert — wheat appears in all 1 incident → frequency 1.0
        // severity Rash = 2.0 → normWeight = 2/3 ≈ 0.6667
        // confidence ≈ 0.6667 (above SuspectThreshold 0.15)
        _repoMock.Verify(r => r.UpsertSuspectedAllergenAsync(
            _householdId, _memberId, MemberName,
            "wheat", null,
            It.Is<decimal>(c => c > 0.15m),
            1,
            default), Times.Once);
    }

    [Fact]
    public async Task RunForMemberAsync_ControlProductHasWheat_WheatCleared()
    {
        // Arrange — one reaction incident with wheat; one control product also with wheat
        Guid incidentId  = Guid.NewGuid();
        Guid reactionProd = Guid.NewGuid();
        Guid controlProd  = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetReactionProductsForMemberAsync(_householdId, _memberId, default))
            .ReturnsAsync(new List<AllergyIncidentProductRow>
            {
                new() { IncidentId = incidentId, ProductId = reactionProd, HadReaction = true,  SeverityLevel = "Intolerance" },
                new() { IncidentId = incidentId, ProductId = controlProd,  HadReaction = false, SeverityLevel = "Intolerance" }
            });

        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(reactionProd, default))
            .ReturnsAsync(new List<string> { "wheat", "sugar" });
        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(controlProd, default))
            .ReturnsAsync(new List<string> { "wheat", "flour" });

        // Act
        await _analyzer.RunForMemberAsync(_householdId, _memberId, MemberName);

        // Assert — wheat is in the control product → should be cleared, NOT upserted as suspect
        _repoMock.Verify(r => r.InsertClearedIngredientAsync(
            _householdId, _memberId, MemberName, "wheat", null, default), Times.Once);

        _repoMock.Verify(r => r.UpsertSuspectedAllergenAsync(
            _householdId, _memberId, MemberName, "wheat", null,
            It.IsAny<decimal>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task RunForMemberAsync_ERVisitSeverity_ConfidenceHigherThanIntolerance()
    {
        // Arrange
        Guid incidentIdER    = Guid.NewGuid();
        Guid incidentIdIntol = Guid.NewGuid();
        Guid prod1           = Guid.NewGuid();
        Guid prod2           = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetReactionProductsForMemberAsync(_householdId, _memberId, default))
            .ReturnsAsync(new List<AllergyIncidentProductRow>
            {
                new() { IncidentId = incidentIdER,    ProductId = prod1, HadReaction = true, SeverityLevel = "ERVisit" },
                new() { IncidentId = incidentIdIntol, ProductId = prod2, HadReaction = true, SeverityLevel = "Intolerance" }
            });

        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(prod1, default))
            .ReturnsAsync(new List<string> { "peanut" });
        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(prod2, default))
            .ReturnsAsync(new List<string> { "peanut" });

        // Act
        await _analyzer.RunForMemberAsync(_householdId, _memberId, MemberName);

        // Assert — peanut in 2/2 incidents, maxWeight = ERVisit = 3.0; confidence = 1.0 * (3.0/3.0) = 1.0
        _repoMock.Verify(r => r.UpsertSuspectedAllergenAsync(
            _householdId, _memberId, MemberName,
            "peanut", null,
            It.Is<decimal>(c => c > 0.90m),
            2,
            default), Times.Once);
    }

    [Fact]
    public async Task RunForMemberAsync_SameIngredientTwoIncidents_MaxWeightWins()
    {
        // "First ERVisit, second Intolerance" — MaxSeverityWeight must be 3.0
        Guid incidentId1 = Guid.NewGuid();
        Guid incidentId2 = Guid.NewGuid();
        Guid prod1       = Guid.NewGuid();
        Guid prod2       = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetReactionProductsForMemberAsync(_householdId, _memberId, default))
            .ReturnsAsync(new List<AllergyIncidentProductRow>
            {
                new() { IncidentId = incidentId1, ProductId = prod1, HadReaction = true, SeverityLevel = "ERVisit" },
                new() { IncidentId = incidentId2, ProductId = prod2, HadReaction = true, SeverityLevel = "Intolerance" }
            });

        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(prod1, default))
            .ReturnsAsync(new List<string> { "soy" });
        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(prod2, default))
            .ReturnsAsync(new List<string> { "soy" });

        await _analyzer.RunForMemberAsync(_householdId, _memberId, MemberName);

        // frequency=1.0 (both incidents), maxWeight=3.0 → confidence = 1.0 * 1.0 = 1.0
        _repoMock.Verify(r => r.UpsertSuspectedAllergenAsync(
            _householdId, _memberId, MemberName,
            "soy", null,
            It.Is<decimal>(c => c >= 0.99m),
            2,
            default), Times.Once);
    }

    [Fact]
    public async Task RunForMemberAsync_ConfidenceBelowThreshold_NotInserted()
    {
        // frequency very low: ingredient in 1 of 10 incidents; severity Intolerance (1.0)
        // confidence = (1/10) * (1/3) ≈ 0.033 — below 0.15 threshold
        List<AllergyIncidentProductRow> rows = new();
        List<Guid> products = new();

        for (int i = 0; i < 10; i++)
        {
            Guid incId = Guid.NewGuid();
            Guid prod  = Guid.NewGuid();
            products.Add(prod);

            rows.Add(new AllergyIncidentProductRow
            {
                IncidentId    = incId,
                ProductId     = prod,
                HadReaction   = true,
                SeverityLevel = "Intolerance"
            });
        }

        _repoMock
            .Setup(r => r.GetReactionProductsForMemberAsync(_householdId, _memberId, default))
            .ReturnsAsync(rows);

        // Only prod[0] has "rare_spice"; rest have unrelated ingredients
        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(products[0], default))
            .ReturnsAsync(new List<string> { "rare_spice", "wheat" });
        for (int i = 1; i < 10; i++)
        {
            _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(products[i], default))
                .ReturnsAsync(new List<string> { "wheat" });
        }

        await _analyzer.RunForMemberAsync(_householdId, _memberId, MemberName);

        // rare_spice only in 1/10 incidents with Intolerance (1.0) → conf ≈ 0.033 → not inserted
        _repoMock.Verify(r => r.UpsertSuspectedAllergenAsync(
            _householdId, _memberId, MemberName,
            "rare_spice", null,
            It.IsAny<decimal>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task RunForMemberAsync_SafeIngredientFromInventory_Cleared()
    {
        // Ingredient also in safe-set returned by InventoryService
        Guid incidentId = Guid.NewGuid();
        Guid prod       = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetReactionProductsForMemberAsync(_householdId, _memberId, default))
            .ReturnsAsync(new List<AllergyIncidentProductRow>
            {
                new() { IncidentId = incidentId, ProductId = prod, HadReaction = true, SeverityLevel = "Rash" }
            });

        _ingredientsMock.Setup(s => s.GetNormalizedIngredientsAsync(prod, default))
            .ReturnsAsync(new List<string> { "wheat" });

        // Safe set contains wheat
        _ingredientsMock
            .Setup(s => s.GetSafeIngredientSetAsync(_householdId, _memberId, It.IsAny<int>(), default))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "wheat" });

        await _analyzer.RunForMemberAsync(_householdId, _memberId, MemberName);

        _repoMock.Verify(r => r.InsertClearedIngredientAsync(
            _householdId, _memberId, MemberName, "wheat", null, default), Times.Once);
        _repoMock.Verify(r => r.UpsertSuspectedAllergenAsync(
            _householdId, _memberId, MemberName, "wheat", null,
            It.IsAny<decimal>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task RunForMemberAsync_NoReactionRows_ReturnsImmediately()
    {
        _repoMock
            .Setup(r => r.GetReactionProductsForMemberAsync(_householdId, _memberId, default))
            .ReturnsAsync(new List<AllergyIncidentProductRow>());

        await _analyzer.RunForMemberAsync(_householdId, _memberId, MemberName);

        _repoMock.Verify(r => r.UpsertSuspectedAllergenAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<decimal>(), It.IsAny<int>(), default), Times.Never);
        _repoMock.Verify(r => r.InsertClearedIngredientAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid?>(), default), Times.Never);
    }
}
