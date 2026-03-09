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
    public async Task Worker_PendingRow_PublishesEventAndMarksAsSent()
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

        // Act — run one cycle
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await Task.Delay(200); // let worker process
        await worker.StopAsync(cts.Token);

        // Assert
        busMock.Verify(b => b.PublishAsync(
            It.Is<ExpressRecipe.Shared.Messages.RecipeCookedEvent>(e => e.CookingHistoryId == historyId),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        repoMock.Verify(r => r.MarkInventoryDeductionSentAsync(historyId, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Worker_NoPendingRows_DoesNotPublish()
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

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await Task.Delay(200);
        await worker.StopAsync(cts.Token);

        busMock.Verify(b => b.PublishAsync(
            It.IsAny<ExpressRecipe.Shared.Messages.RecipeCookedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
