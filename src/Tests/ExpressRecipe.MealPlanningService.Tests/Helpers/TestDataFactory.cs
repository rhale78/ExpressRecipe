using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Tests.Helpers;

public static class TestDataFactory
{
    public static WorkQueueItemDto CreateWorkQueueItemDto(
        Guid? id          = null,
        Guid? householdId = null,
        string itemType   = "RateRecipe",
        int priority      = WorkQueuePriority.RateRecipe,
        string title      = "Rate your meal",
        string status     = "Pending",
        DateTime? expiresAt = null)
    {
        return new WorkQueueItemDto
        {
            Id          = id ?? Guid.NewGuid(),
            HouseholdId = householdId ?? Guid.NewGuid(),
            ItemType    = itemType,
            Priority    = priority,
            Title       = title,
            Status      = status,
            ExpiresAt   = expiresAt,
            CreatedAt   = DateTime.UtcNow
        };
    }

    public static CookingTimerDto CreateCookingTimerDto(
        Guid? id = null,
        Guid? userId = null,
        Guid? householdId = null,
        string label = "Test Timer",
        int durationSeconds = 300,
        string status = "Preset",
        DateTime? startedAt = null,
        DateTime? expiresAt = null,
        int pausedSeconds = 0,
        bool notificationSent = false)
    {
        return new CookingTimerDto
        {
            Id               = id ?? Guid.NewGuid(),
            UserId           = userId ?? Guid.NewGuid(),
            HouseholdId      = householdId ?? Guid.NewGuid(),
            Label            = label,
            DurationSeconds  = durationSeconds,
            Status           = status,
            StartedAt        = startedAt,
            ExpiresAt        = expiresAt,
            PausedSeconds    = pausedSeconds,
            NotificationSent = notificationSent
        };
    }

    public static CookingTimerDto CreateRunningTimer(Guid userId, string label = "Simmer sauce",
        int durationSeconds = 600)
    {
        DateTime now = DateTime.UtcNow;
        return new CookingTimerDto
        {
            Id              = Guid.NewGuid(),
            UserId          = userId,
            HouseholdId     = Guid.NewGuid(),
            Label           = label,
            DurationSeconds = durationSeconds,
            Status          = "Running",
            StartedAt       = now,
            ExpiresAt       = now.AddSeconds(durationSeconds)
        };
    }

    public static CookingTimerDto CreateExpiredUnnotifiedTimer(Guid userId, string label = "Boil pasta", Guid? plannedMealId = null)
    {
        DateTime now = DateTime.UtcNow;
        return new CookingTimerDto
        {
            Id               = Guid.NewGuid(),
            UserId           = userId,
            HouseholdId      = Guid.NewGuid(),
            Label            = label,
            DurationSeconds  = 300,
            Status           = "Running",
            StartedAt        = now.AddSeconds(-400),
            ExpiresAt        = now.AddSeconds(-100),
            NotificationSent = false,
            PlannedMealId    = plannedMealId
        };
    }
}
