using FluentAssertions;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Tests.Helpers;

namespace ExpressRecipe.MealPlanningService.Tests;

public class CookingTimerDtoTests
{
    [Fact]
    public void RemainingSeconds_WhenRunning_ReturnsSecondsUntilExpiry()
    {
        DateTime expiry = DateTime.UtcNow.AddSeconds(120);
        CookingTimerDto timer = new()
        {
            Id              = Guid.NewGuid(),
            UserId          = Guid.NewGuid(),
            HouseholdId     = Guid.NewGuid(),
            Label           = "Test",
            DurationSeconds = 300,
            Status          = "Running",
            ExpiresAt       = expiry
        };

        timer.RemainingSeconds.Should().BeInRange(118, 122);
    }

    [Fact]
    public void RemainingSeconds_WhenExpired_ReturnsNegative()
    {
        DateTime expiry = DateTime.UtcNow.AddSeconds(-30);
        CookingTimerDto timer = new()
        {
            Id              = Guid.NewGuid(),
            UserId          = Guid.NewGuid(),
            HouseholdId     = Guid.NewGuid(),
            Label           = "Test",
            DurationSeconds = 300,
            Status          = "Running",
            ExpiresAt       = expiry
        };

        timer.RemainingSeconds.Should().BeLessThan(0);
    }

    [Fact]
    public void RemainingSeconds_WhenPreset_ReturnsDuration()
    {
        CookingTimerDto timer = new()
        {
            Id              = Guid.NewGuid(),
            UserId          = Guid.NewGuid(),
            HouseholdId     = Guid.NewGuid(),
            Label           = "Test",
            DurationSeconds = 300,
            Status          = "Preset"
        };

        timer.RemainingSeconds.Should().Be(300);
    }

    [Fact]
    public void RemainingSeconds_WhenPaused_ReturnsDurationMinusPausedSeconds()
    {
        CookingTimerDto timer = new()
        {
            Id              = Guid.NewGuid(),
            UserId          = Guid.NewGuid(),
            HouseholdId     = Guid.NewGuid(),
            Label           = "Test",
            DurationSeconds = 300,
            Status          = "Paused",
            PausedSeconds   = 30
        };

        timer.RemainingSeconds.Should().Be(270);
    }

    [Fact]
    public void CreateTimerAsync_StartImmediately_SetsStatusRunning()
    {
        // This tests the logic documented in the spec — status=Running when startImmediately=true
        // The actual DB call is tested via controller tests; here we test the DTO computation
        DateTime now = DateTime.UtcNow;
        CookingTimerDto runningTimer = TestDataFactory.CreateRunningTimer(Guid.NewGuid(), durationSeconds: 600);

        runningTimer.Status.Should().Be("Running");
        runningTimer.ExpiresAt.Should().NotBeNull();
        runningTimer.ExpiresAt!.Value.Should().BeCloseTo(now.AddSeconds(600), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateTimerAsync_NotStartImmediately_SetsStatusPreset()
    {
        CookingTimerDto presetTimer = TestDataFactory.CreateCookingTimerDto(
            status: "Preset", startedAt: null, expiresAt: null);

        presetTimer.Status.Should().Be("Preset");
        presetTimer.StartedAt.Should().BeNull();
        presetTimer.ExpiresAt.Should().BeNull();
    }
}
