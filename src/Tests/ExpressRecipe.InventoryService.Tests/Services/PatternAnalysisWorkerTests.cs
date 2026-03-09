using FluentAssertions;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;

namespace ExpressRecipe.InventoryService.Tests.Services;

/// <summary>
/// Unit tests for PatternAnalysisWorker.ComputePattern
/// </summary>
public class PatternAnalysisWorkerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    #region ComputePattern — Five purchases at 7-day intervals

    [Fact]
    public void ComputePattern_FivePurchasesAt7DayIntervals_AvgIs7()
    {
        // Arrange: 5 purchases at exactly 7-day intervals
        DateTime baseDate = DateTime.UtcNow.AddDays(-28); // oldest
        List<PurchaseEventDto> purchases = Enumerable.Range(0, 5)
            .Select(i => new PurchaseEventDto
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                ProductId = ProductId,
                PurchasedAt = baseDate.AddDays(i * 7),
                Quantity = 1,
                Source = "ManualAdd"
            })
            .ToList();

        // Act
        ProductConsumptionPatternRecord result = PatternAnalysisWorker.ComputePattern(UserId, purchases);

        // Assert
        result.AvgDaysBetweenPurchases.Should().BeApproximately(7m, 0.01m);
        result.PurchaseCount.Should().Be(5);
        result.FirstPurchasedAt.Should().Be(purchases.First().PurchasedAt);
        result.LastPurchasedAt.Should().Be(purchases.Last().PurchasedAt);
    }

    [Fact]
    public void ComputePattern_FivePurchasesAt7DayIntervals_EstimatedNextIsLastPlusAvg()
    {
        // Arrange
        DateTime baseDate = DateTime.UtcNow.AddDays(-28);
        List<PurchaseEventDto> purchases = Enumerable.Range(0, 5)
            .Select(i => new PurchaseEventDto
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                ProductId = ProductId,
                PurchasedAt = baseDate.AddDays(i * 7),
                Quantity = 1,
                Source = "ManualAdd"
            })
            .ToList();

        // Act
        ProductConsumptionPatternRecord result = PatternAnalysisWorker.ComputePattern(UserId, purchases);

        // Assert
        DateTime expectedNext = purchases.Last().PurchasedAt.AddDays(7);
        result.EstimatedNextPurchaseDate.Should().BeCloseTo(expectedNext, TimeSpan.FromSeconds(1));
        result.IsAbandoned.Should().BeFalse();
    }

    #endregion

    #region ComputePattern — Single purchase 95 days ago → IsAbandoned

    [Fact]
    public void ComputePattern_SinglePurchase95DaysAgo_IsAbandoned()
    {
        // Arrange: one purchase 95 days ago
        List<PurchaseEventDto> purchases = new List<PurchaseEventDto>
        {
            new PurchaseEventDto
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                ProductId = ProductId,
                PurchasedAt = DateTime.UtcNow.AddDays(-95),
                Quantity = 1,
                Source = "ManualAdd"
            }
        };

        // Act
        ProductConsumptionPatternRecord result = PatternAnalysisWorker.ComputePattern(UserId, purchases);

        // Assert
        result.IsAbandoned.Should().BeTrue();
        result.AbandonedAfterCount.Should().Be(1);
        result.PurchaseCount.Should().Be(1);
    }

    [Fact]
    public void ComputePattern_SinglePurchaseRecently_IsNotAbandoned()
    {
        // Arrange: one purchase yesterday
        List<PurchaseEventDto> purchases = new List<PurchaseEventDto>
        {
            new PurchaseEventDto
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                ProductId = ProductId,
                PurchasedAt = DateTime.UtcNow.AddDays(-1),
                Quantity = 1,
                Source = "ManualAdd"
            }
        };

        // Act
        ProductConsumptionPatternRecord result = PatternAnalysisWorker.ComputePattern(UserId, purchases);

        // Assert
        result.IsAbandoned.Should().BeFalse();
    }

    #endregion

    #region ComputePattern — Abandoned based on avg*3 threshold

    [Fact]
    public void ComputePattern_MultiplePurchasesWithLongSilence_IsAbandoned()
    {
        // Arrange: 3 purchases at 10-day intervals, last purchase was 95 days ago
        // max(10*3=30, 90) = 90; 95 > 90 → abandoned
        DateTime lastPurchase = DateTime.UtcNow.AddDays(-95);
        List<PurchaseEventDto> purchases = new List<PurchaseEventDto>
        {
            new PurchaseEventDto { Id = Guid.NewGuid(), UserId = UserId, ProductId = ProductId, PurchasedAt = lastPurchase.AddDays(-20), Quantity = 1, Source = "ManualAdd" },
            new PurchaseEventDto { Id = Guid.NewGuid(), UserId = UserId, ProductId = ProductId, PurchasedAt = lastPurchase.AddDays(-10), Quantity = 1, Source = "ManualAdd" },
            new PurchaseEventDto { Id = Guid.NewGuid(), UserId = UserId, ProductId = ProductId, PurchasedAt = lastPurchase, Quantity = 1, Source = "ManualAdd" }
        };

        // Act
        ProductConsumptionPatternRecord result = PatternAnalysisWorker.ComputePattern(UserId, purchases);

        // Assert
        result.AvgDaysBetweenPurchases.Should().BeApproximately(10m, 0.1m);
        // daysSinceLast=95 > max(avg*3=30, 90)=90 → abandoned
        result.IsAbandoned.Should().BeTrue();
        result.AbandonedAfterCount.Should().Be(3);
    }

    [Fact]
    public void ComputePattern_MultiplePurchasesWithShortSilence_IsNotAbandoned()
    {
        // Arrange: 3 purchases at 10-day intervals, last purchase 35 days ago
        // max(10*3=30, 90) = 90; 35 < 90 → NOT abandoned
        DateTime lastPurchase = DateTime.UtcNow.AddDays(-35);
        List<PurchaseEventDto> purchases = new List<PurchaseEventDto>
        {
            new PurchaseEventDto { Id = Guid.NewGuid(), UserId = UserId, ProductId = ProductId, PurchasedAt = lastPurchase.AddDays(-20), Quantity = 1, Source = "ManualAdd" },
            new PurchaseEventDto { Id = Guid.NewGuid(), UserId = UserId, ProductId = ProductId, PurchasedAt = lastPurchase.AddDays(-10), Quantity = 1, Source = "ManualAdd" },
            new PurchaseEventDto { Id = Guid.NewGuid(), UserId = UserId, ProductId = ProductId, PurchasedAt = lastPurchase, Quantity = 1, Source = "ManualAdd" }
        };

        // Act
        ProductConsumptionPatternRecord result = PatternAnalysisWorker.ComputePattern(UserId, purchases);

        // Assert
        result.IsAbandoned.Should().BeFalse();
    }

    #endregion

    #region ComputePattern — UserId and ProductId propagated

    [Fact]
    public void ComputePattern_SetsUserIdAndProductId()
    {
        // Arrange
        Guid specificUserId = Guid.NewGuid();
        Guid specificProductId = Guid.NewGuid();
        List<PurchaseEventDto> purchases = new List<PurchaseEventDto>
        {
            new PurchaseEventDto
            {
                Id = Guid.NewGuid(),
                UserId = specificUserId,
                ProductId = specificProductId,
                PurchasedAt = DateTime.UtcNow.AddDays(-1),
                Quantity = 1,
                Source = "ManualAdd"
            }
        };

        // Act
        ProductConsumptionPatternRecord result = PatternAnalysisWorker.ComputePattern(specificUserId, purchases);

        // Assert
        result.UserId.Should().Be(specificUserId);
        result.ProductId.Should().Be(specificProductId);
    }

    #endregion

    #region ComputePattern — No gaps (single purchase), AvgDays null

    [Fact]
    public void ComputePattern_SinglePurchase_AvgDaysIsNull()
    {
        // Arrange
        List<PurchaseEventDto> purchases = new List<PurchaseEventDto>
        {
            new PurchaseEventDto
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                ProductId = ProductId,
                PurchasedAt = DateTime.UtcNow.AddDays(-5),
                Quantity = 1,
                Source = "ManualAdd"
            }
        };

        // Act
        ProductConsumptionPatternRecord result = PatternAnalysisWorker.ComputePattern(UserId, purchases);

        // Assert
        result.AvgDaysBetweenPurchases.Should().BeNull();
        result.EstimatedNextPurchaseDate.Should().BeNull();
    }

    #endregion
}
