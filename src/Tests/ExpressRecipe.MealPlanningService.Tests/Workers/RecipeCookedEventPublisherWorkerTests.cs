using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.Messaging.Core.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.MealPlanningService.Tests.Workers;

public class RecipeCookedEventPublisherWorkerTests
{
    [Fact]
    public async Task ProcessPendingDeductions_PendingRow_PublishesEventAndMarksAsSent()
    {
        // Arrange
        Guid historyId = Guid.NewGuid();
        CookingHistoryDto pendingRow = new()
        {
            Id        = historyId,
            UserId    = Guid.NewGuid(),
            RecipeId  = Guid.NewGuid(),
            RecipeName = "Test Recipe",
            CookedAt  = DateTime.UtcNow,
            Servings  = 2,
            MealType  = "Dinner",
            Source    = "PlannedMeal",
            InventoryDeductionSent = false
        };

        Mock<IMealPlanningRepository> repoMock = new();
        repoMock.Setup(r => r.GetPendingInventoryDeductionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CookingHistoryDto> { pendingRow });
        repoMock.Setup(r => r.MarkInventoryDeductionSentAsync(historyId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IMessageBus> busMock = new();
        busMock.Setup(b => b.PublishAsync(
                It.IsAny<ExpressRecipe.Shared.Messages.RecipeCookedEvent>(),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ServiceCollection services = new();
        services.AddSingleton(repoMock.Object);
        services.AddSingleton(busMock.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        ExpressRecipe.MealPlanningService.Workers.RecipeCookedEventPublisherWorker worker =
            new(provider, NullLogger<ExpressRecipe.MealPlanningService.Workers.RecipeCookedEventPublisherWorker>.Instance);

        // Act — call the processing method directly (deterministic, no timing dependency)
        await worker.ProcessPendingDeductionsAsync(CancellationToken.None);

        // Assert
        busMock.Verify(b => b.PublishAsync(
            It.Is<ExpressRecipe.Shared.Messages.RecipeCookedEvent>(e => e.CookingHistoryId == historyId),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        repoMock.Verify(r => r.MarkInventoryDeductionSentAsync(historyId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingDeductions_NoPendingRows_DoesNotPublish()
    {
        Mock<IMealPlanningRepository> repoMock = new();
        repoMock.Setup(r => r.GetPendingInventoryDeductionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CookingHistoryDto>());

        Mock<IMessageBus> busMock = new();

        ServiceCollection services = new();
        services.AddSingleton(repoMock.Object);
        services.AddSingleton(busMock.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        ExpressRecipe.MealPlanningService.Workers.RecipeCookedEventPublisherWorker worker =
            new(provider, NullLogger<ExpressRecipe.MealPlanningService.Workers.RecipeCookedEventPublisherWorker>.Instance);

        // Act — call the processing method directly
        await worker.ProcessPendingDeductionsAsync(CancellationToken.None);

        // Assert — no publish when there are no pending rows
        busMock.Verify(b => b.PublishAsync(
            It.IsAny<ExpressRecipe.Shared.Messages.RecipeCookedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingDeductions_SecondRun_SkipsAlreadySentRow()
    {
        // Arrange — first run marks as sent; simulate second run returning empty list
        Mock<IMealPlanningRepository> repoMock = new();

        // First call returns one pending row, second call returns empty (simulates the row is already marked)
        int callCount = 0;
        repoMock.Setup(r => r.GetPendingInventoryDeductionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new List<CookingHistoryDto>
                    {
                        new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RecipeId = Guid.NewGuid(),
                                RecipeName = "R", CookedAt = DateTime.UtcNow, Servings = 1,
                                MealType = "Dinner", Source = "PlannedMeal" }
                    }
                    : new List<CookingHistoryDto>();
            });
        repoMock.Setup(r => r.MarkInventoryDeductionSentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IMessageBus> busMock = new();
        busMock.Setup(b => b.PublishAsync(
                It.IsAny<ExpressRecipe.Shared.Messages.RecipeCookedEvent>(),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ServiceCollection services = new();
        services.AddSingleton(repoMock.Object);
        services.AddSingleton(busMock.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        ExpressRecipe.MealPlanningService.Workers.RecipeCookedEventPublisherWorker worker =
            new(provider, NullLogger<ExpressRecipe.MealPlanningService.Workers.RecipeCookedEventPublisherWorker>.Instance);

        // Act — run twice
        await worker.ProcessPendingDeductionsAsync(CancellationToken.None);  // publishes
        await worker.ProcessPendingDeductionsAsync(CancellationToken.None);  // skips (empty)

        // Assert — event published exactly once
        busMock.Verify(b => b.PublishAsync(
            It.IsAny<ExpressRecipe.Shared.Messages.RecipeCookedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
