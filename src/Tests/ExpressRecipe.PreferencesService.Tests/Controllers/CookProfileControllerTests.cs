using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.PreferencesService.Controllers;
using ExpressRecipe.PreferencesService.Services;
using ExpressRecipe.PreferencesService.Contracts.Requests;
using ExpressRecipe.PreferencesService.Contracts.Responses;
using ExpressRecipe.PreferencesService.Tests.Helpers;

namespace ExpressRecipe.PreferencesService.Tests.Controllers;

public class CookProfileControllerTests
{
    private readonly Mock<ICookProfileService> _mockService;
    private readonly Mock<ILogger<CookProfileController>> _mockLogger;
    private readonly CookProfileController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testMemberId;

    public CookProfileControllerTests()
    {
        _mockService = new Mock<ICookProfileService>();
        _mockLogger = new Mock<ILogger<CookProfileController>>();
        _controller = new CookProfileController(_mockService.Object, _mockLogger.Object);
        _testUserId = Guid.NewGuid();
        _testMemberId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region GetCookProfile Tests

    [Fact]
    public async Task GetCookProfile_WithExistingMember_ReturnsCookProfile()
    {
        // Arrange
        var profile = new CookProfileDto
        {
            Id = Guid.NewGuid(),
            MemberId = _testMemberId,
            OverallSkillLevel = "HomeCook",
            CookingFrequency = "Regular"
        };

        _mockService
            .Setup(s => s.GetCookProfileAsync(_testMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _controller.GetCookProfile(_testMemberId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as CookProfileDto)!.MemberId.Should().Be(_testMemberId);
    }

    [Fact]
    public async Task GetCookProfile_WithNonExistingMember_ReturnsNotFound()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetCookProfileAsync(_testMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CookProfileDto?)null);

        // Act
        var result = await _controller.GetCookProfile(_testMemberId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region UpsertCookProfile Tests

    [Fact]
    public async Task UpsertCookProfile_WithValidRequest_ReturnsOkWithId()
    {
        // Arrange
        var request = new UpsertCookProfileRequest
        {
            CooksForHousehold = true,
            CookingFrequency = "Daily",
            OverallSkillLevel = "Intermediate"
        };
        var profileId = Guid.NewGuid();

        _mockService
            .Setup(s => s.UpsertCookProfileAsync(_testMemberId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileId);

        // Act
        var result = await _controller.UpsertCookProfile(_testMemberId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockService.Verify(s => s.UpsertCookProfileAsync(_testMemberId, request, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetTechniqueComfort Tests

    [Fact]
    public async Task GetTechniqueComfort_WithExistingTechnique_ReturnsComfort()
    {
        // Arrange
        var techniqueCode = "Saute";
        var comfort = new TechniqueComfortDto
        {
            Id = Guid.NewGuid(),
            MemberId = _testMemberId,
            TechniqueCode = techniqueCode,
            ComfortLevel = "Comfortable"
        };

        _mockService
            .Setup(s => s.GetTechniqueComfortAsync(_testMemberId, techniqueCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comfort);

        // Act
        var result = await _controller.GetTechniqueComfort(_testMemberId, techniqueCode, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as TechniqueComfortDto)!.TechniqueCode.Should().Be(techniqueCode);
    }

    [Fact]
    public async Task GetTechniqueComfort_WithNonExistingTechnique_ReturnsNotFound()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetTechniqueComfortAsync(_testMemberId, "Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TechniqueComfortDto?)null);

        // Act
        var result = await _controller.GetTechniqueComfort(_testMemberId, "Unknown", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetDismissedTips Tests

    [Fact]
    public async Task GetDismissedTips_ReturnsListOfDismissedTips()
    {
        // Arrange
        var tips = new List<DismissedTipDto>
        {
            new DismissedTipDto { Id = Guid.NewGuid(), MemberId = _testMemberId, TipId = Guid.NewGuid() },
            new DismissedTipDto { Id = Guid.NewGuid(), MemberId = _testMemberId, TipId = Guid.NewGuid() }
        };

        _mockService
            .Setup(s => s.GetDismissedTipsAsync(_testMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tips);

        // Act
        var result = await _controller.GetDismissedTips(_testMemberId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<DismissedTipDto>)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDismissedTips_WhenNone_ReturnsEmptyList()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetDismissedTipsAsync(_testMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DismissedTipDto>());

        // Act
        var result = await _controller.GetDismissedTips(_testMemberId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<DismissedTipDto>)!.Should().BeEmpty();
    }

    #endregion
}
