using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.ProfileService.Controllers;
using ExpressRecipe.ProfileService.Services;
using ExpressRecipe.ProfileService.Contracts.Requests;
using ExpressRecipe.ProfileService.Contracts.Responses;
using ExpressRecipe.ProfileService.Tests.Helpers;

namespace ExpressRecipe.ProfileService.Tests.Controllers;

public class HouseholdControllerTests
{
    private readonly Mock<IHouseholdMemberService> _mockService;
    private readonly Mock<ILogger<HouseholdController>> _mockLogger;
    private readonly HouseholdController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testHouseholdId;

    public HouseholdControllerTests()
    {
        _mockService = new Mock<IHouseholdMemberService>();
        _mockLogger = new Mock<ILogger<HouseholdController>>();
        _controller = new HouseholdController(_mockService.Object, _mockLogger.Object);
        _testUserId = Guid.NewGuid();
        _testHouseholdId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region GetMembers Tests

    [Fact]
    public async Task GetMembers_ReturnsAllHouseholdMembers()
    {
        // Arrange
        var members = new List<HouseholdMemberDto>
        {
            new HouseholdMemberDto { Id = Guid.NewGuid(), HouseholdId = _testHouseholdId, DisplayName = "Alice", MemberType = "Adult" },
            new HouseholdMemberDto { Id = Guid.NewGuid(), HouseholdId = _testHouseholdId, DisplayName = "Bob", MemberType = "Adult" }
        };

        _mockService
            .Setup(s => s.GetMembersAsync(_testHouseholdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Act
        var result = await _controller.GetMembers(_testHouseholdId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<HouseholdMemberDto>)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMembers_WhenNoMembers_ReturnsEmptyList()
    {
        // Arrange
        _mockService
            .Setup(s => s.GetMembersAsync(_testHouseholdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HouseholdMemberDto>());

        // Act
        var result = await _controller.GetMembers(_testHouseholdId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<HouseholdMemberDto>)!.Should().BeEmpty();
    }

    #endregion

    #region AddMember Tests

    [Fact]
    public async Task AddMember_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new AddMemberRequest { DisplayName = "Charlie", MemberType = "Child" };
        var memberId = Guid.NewGuid();

        _mockService
            .Setup(s => s.AddMemberAsync(_testHouseholdId, request, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberId);

        // Act
        var result = await _controller.AddMember(_testHouseholdId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        _mockService.Verify(s => s.AddMemberAsync(_testHouseholdId, request, _testUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMember_PassesRequestingUserIdFromClaims()
    {
        // Arrange
        var request = new AddMemberRequest { DisplayName = "Dave", MemberType = "Adult" };
        var memberId = Guid.NewGuid();

        _mockService
            .Setup(s => s.AddMemberAsync(_testHouseholdId, request, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberId);

        // Act
        await _controller.AddMember(_testHouseholdId, request, CancellationToken.None);

        // Assert
        _mockService.Verify(s => s.AddMemberAsync(_testHouseholdId, request, _testUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateMember Tests

    [Fact]
    public async Task UpdateMember_WithExistingMember_ReturnsNoContent()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var request = new UpdateMemberRequest { DisplayName = "Alice Updated" };

        _mockService
            .Setup(s => s.UpdateMemberAsync(_testHouseholdId, memberId, request, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateMember(_testHouseholdId, memberId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateMember_WithNonExistingMember_ReturnsNotFound()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var request = new UpdateMemberRequest { DisplayName = "Ghost" };

        _mockService
            .Setup(s => s.UpdateMemberAsync(_testHouseholdId, memberId, request, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdateMember(_testHouseholdId, memberId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region RemoveMember Tests

    [Fact]
    public async Task RemoveMember_WithExistingMember_ReturnsNoContent()
    {
        // Arrange
        var memberId = Guid.NewGuid();

        _mockService
            .Setup(s => s.RemoveMemberAsync(_testHouseholdId, memberId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RemoveMember(_testHouseholdId, memberId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveMember_WithNonExistingMember_ReturnsNotFound()
    {
        // Arrange
        var memberId = Guid.NewGuid();

        _mockService
            .Setup(s => s.RemoveMemberAsync(_testHouseholdId, memberId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.RemoveMember(_testHouseholdId, memberId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion
}
