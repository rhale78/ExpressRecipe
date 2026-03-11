using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ExpressRecipe.MealPlanningService.Controllers;
using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Tests.Controllers;

public class WorkQueueInternalControllerTests
{
    private readonly Mock<IWorkQueueRepository> _mockRepo;
    private readonly WorkQueueInternalController _controller;

    public WorkQueueInternalControllerTests()
    {
        _mockRepo   = new Mock<IWorkQueueRepository>();
        _controller = new WorkQueueInternalController(_mockRepo.Object);
    }

    [Fact]
    public async Task UpsertItem_CallsRepositoryAndReturnsNoContent()
    {
        Guid householdId    = Guid.NewGuid();
        Guid sourceEntityId = Guid.NewGuid();

        UpsertWorkQueueItemRequest req = new()
        {
            HouseholdId    = householdId,
            ItemType       = "RateRecipe",
            Priority       = WorkQueuePriority.RateRecipe,
            Title          = "Rate your meal",
            Body           = "Tap to rate",
            ActionPayload  = "{\"recipeId\":\"...\"}",
            RelatedEntityId = sourceEntityId,
            SourceService  = "Recipe",
            RelatedEntityType = "Recipe",
            ExpiresAt      = DateTime.UtcNow.AddDays(7)
        };

        _mockRepo
            .Setup(r => r.UpsertAsync(It.IsAny<UpsertWorkQueueItemRequest>(), default))
            .Returns(Task.FromResult(Guid.Empty));

        IActionResult result = await _controller.UpsertItem(req, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.UpsertAsync(It.IsAny<UpsertWorkQueueItemRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task UpsertItem_WithNullOptionalFields_StillCallsRepository()
    {
        Guid householdId = Guid.NewGuid();

        UpsertWorkQueueItemRequest req = new()
        {
            HouseholdId   = householdId,
            ItemType      = "PriceDrop",
            Priority      = WorkQueuePriority.PriceDrop,
            Title         = "Price drop on Milk",
            Body          = null,
            ActionPayload = null,
            RelatedEntityId = null,
            SourceService  = null,
            ExpiresAt      = null
        };

        _mockRepo
            .Setup(r => r.UpsertAsync(It.IsAny<UpsertWorkQueueItemRequest>(), default))
            .Returns(Task.FromResult(Guid.Empty));

        IActionResult result = await _controller.UpsertItem(req, default);

        result.Should().BeOfType<NoContentResult>();
    }
}
