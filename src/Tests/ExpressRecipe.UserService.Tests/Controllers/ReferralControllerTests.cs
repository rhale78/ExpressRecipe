using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class ReferralControllerTests
{
    private readonly Mock<IReferralService> _mockService;
    private readonly Mock<IReferralRepository> _mockRepository;
    private readonly Mock<ILogger<ReferralController>> _mockLogger;
    private readonly ReferralController _controller;
    private readonly Guid _userId;

    public ReferralControllerTests()
    {
        _mockService = new Mock<IReferralService>();
        _mockRepository = new Mock<IReferralRepository>();
        _mockLogger = new Mock<ILogger<ReferralController>>();

        _controller = new ReferralController(_mockService.Object, _mockRepository.Object, _mockLogger.Object);
        _userId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _userId);
    }

    [Fact]
    public async Task GetOrCreateCode_MaxActiveCodesReached_Returns422()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetOrCreateReferralCodeAsync(_userId, default))
            .ThrowsAsync(new ReferralException("MaxActiveCodesReached", "Too many codes."));

        // Act
        var result = await _controller.GetOrCreateCode(default);

        // Assert
        result.Should().BeOfType<UnprocessableEntityObjectResult>()
            .Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task GetOrCreateCode_Success_ReturnsCode()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetOrCreateReferralCodeAsync(_userId, default))
            .ReturnsAsync("TESTCODE");

        // Act
        var result = await _controller.GetOrCreateCode(default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateShareLink_DailyLimitReached_Returns422()
    {
        // Arrange
        var request = new CreateShareLinkRequest { EntityType = "Recipe", EntityId = Guid.NewGuid() };
        _mockService
            .Setup(s => s.CreateShareLinkAsync(_userId, "Recipe", request.EntityId, default))
            .ThrowsAsync(new ReferralException("DailyShareLimitReached", "Too many links today."));

        // Act
        var result = await _controller.CreateShareLink(request, default);

        // Assert
        result.Should().BeOfType<UnprocessableEntityObjectResult>()
            .Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task GetShareLink_ExpiredToken_Returns404()
    {
        // Arrange
        const string token = "EXPIREDTOKEN";
        _mockRepository
            .Setup(r => r.GetShareLinkByTokenAsync(token, default))
            .ReturnsAsync(new ShareLinkDto
            {
                Id = Guid.NewGuid(),
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(-1),  // expired
                CreatedBy = _userId,
                EntityType = "Recipe",
                EntityId = Guid.NewGuid(),
                ViewCount = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-31)
            });

        // Act
        var result = await _controller.GetShareLink(token, default);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetShareLink_ValidToken_ReturnsLink()
    {
        // Arrange
        const string token = "VALIDTOKEN";
        var linkId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetShareLinkByTokenAsync(token, default))
            .ReturnsAsync(new ShareLinkDto
            {
                Id = linkId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(20),  // valid
                CreatedBy = _userId,
                EntityType = "Recipe",
                EntityId = Guid.NewGuid(),
                ViewCount = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            });

        _mockRepository
            .Setup(r => r.IncrementShareLinkViewCountAsync(linkId, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.GetShareLink(token, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.IncrementShareLinkViewCountAsync(linkId, default), Times.Once);
    }

    [Fact]
    public async Task GetShareLink_NotFound_Returns404()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetShareLinkByTokenAsync("NOTFOUND", default))
            .ReturnsAsync((ShareLinkDto?)null);

        // Act
        var result = await _controller.GetShareLink("NOTFOUND", default);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
