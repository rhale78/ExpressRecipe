using FluentAssertions;
using ExpressRecipe.InventoryService.Services;

namespace ExpressRecipe.InventoryService.Tests.Services;

/// <summary>
/// Unit tests for ExpirationAlertWorker alert type determination.
/// </summary>
public class ExpirationAlertWorkerTests
{
    #region DetermineAlertType

    [Theory]
    [InlineData(4, "Critical")]    // 4 days → Critical (1-7 days)
    [InlineData(1, "Critical")]    // 1 day  → Critical
    [InlineData(7, "Critical")]    // 7 days → Critical (boundary)
    [InlineData(0, "Expired")]     // Today   → Expired
    [InlineData(-1, "Expired")]    // Yesterday → Expired
    [InlineData(8, "Warning")]     // 8 days  → Warning (>7 and <=14)
    [InlineData(10, "Warning")]    // 10 days → Warning
    [InlineData(14, "Warning")]    // 14 days → Warning (boundary)
    [InlineData(15, "Warning")]    // 15 days → Warning (> 14, still Warning as no upper bound on Warning)
    public void DetermineAlertType_ReturnsCorrectAlertType(int daysUntilExpiration, string expectedAlertType)
    {
        // Act
        string alertType = ExpirationAlertWorker.DetermineAlertTypeForTest(daysUntilExpiration);

        // Assert
        alertType.Should().Be(expectedAlertType);
    }

    [Fact]
    public void DetermineAlertType_4Days_IsCritical()
    {
        // Arrange & Act
        string alertType = ExpirationAlertWorker.DetermineAlertTypeForTest(4);

        // Assert
        alertType.Should().Be("Critical");
    }

    [Fact]
    public void DetermineAlertType_10Days_IsWarning()
    {
        // Arrange & Act
        string alertType = ExpirationAlertWorker.DetermineAlertTypeForTest(10);

        // Assert
        alertType.Should().Be("Warning");
    }

    [Fact]
    public void DetermineAlertType_Expired_IsExpired()
    {
        // Arrange & Act
        string alertType = ExpirationAlertWorker.DetermineAlertTypeForTest(0);

        // Assert
        alertType.Should().Be("Expired");
    }

    #endregion
}
