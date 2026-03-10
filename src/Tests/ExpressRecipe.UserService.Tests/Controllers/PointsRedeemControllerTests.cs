using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class PointsRedeemControllerTests
{
    private readonly Mock<IPointsRepository> _mockRepository;
    private readonly Mock<ILogger<PointsController>> _mockLogger;
    private readonly PointsController _controller;
    private readonly Guid _userId;

    public PointsRedeemControllerTests()
    {
        _mockRepository = new Mock<IPointsRepository>();
        _mockLogger = new Mock<ILogger<PointsController>>();
        _controller = new PointsController(_mockRepository.Object, _mockLogger.Object);
        _userId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _userId);
    }

    [Fact]
    public async Task RedeemReward_InsufficientPoints_Returns422UnprocessableEntity()
    {
        // Arrange
        var rewardItemId = Guid.NewGuid();
        var request = new RedeemRewardRequest { RewardItemId = rewardItemId };

        _mockRepository
            .Setup(r => r.RedeemRewardAsync(_userId, rewardItemId))
            .ThrowsAsync(new InsufficientPointsException(50, 200));

        // Act
        var result = await _controller.RedeemReward(request);

        // Assert
        result.Should().BeOfType<UnprocessableEntityObjectResult>();
        var unprocessable = (UnprocessableEntityObjectResult)result;
        unprocessable.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task RedeemReward_Success_ReturnsOk()
    {
        // Arrange
        var rewardItemId = Guid.NewGuid();
        var request = new RedeemRewardRequest { RewardItemId = rewardItemId };

        _mockRepository
            .Setup(r => r.RedeemRewardAsync(_userId, rewardItemId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RedeemReward(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RedeemReward_InvalidReward_ReturnsBadRequest()
    {
        // Arrange
        var rewardItemId = Guid.NewGuid();
        var request = new RedeemRewardRequest { RewardItemId = rewardItemId };

        _mockRepository
            .Setup(r => r.RedeemRewardAsync(_userId, rewardItemId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.RedeemReward(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RedeemReward_NotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var controller = new PointsController(_mockRepository.Object, _mockLogger.Object);
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(controller);
        var request = new RedeemRewardRequest { RewardItemId = Guid.NewGuid() };

        // Act
        var result = await controller.RedeemReward(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
