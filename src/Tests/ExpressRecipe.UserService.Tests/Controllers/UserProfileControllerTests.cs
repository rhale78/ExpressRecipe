using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class UserProfileControllerTests
{
    private readonly Mock<IUserProfileRepository> _mockRepository;
    private readonly Mock<ILogger<UserProfileController>> _mockLogger;
    private readonly UserProfileController _controller;
    private readonly Guid _testUserId;

    public UserProfileControllerTests()
    {
        _mockRepository = new Mock<IUserProfileRepository>();
        _mockLogger = new Mock<ILogger<UserProfileController>>();

        _controller = new UserProfileController(
            _mockRepository.Object,
            _mockLogger.Object
        );

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);
    }

    #region GetMyProfile Tests

    [Fact]
    public async Task GetMyProfile_WhenProfileExists_ReturnsOk()
    {
        // Arrange
        var expectedProfile = new UserProfileDto
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@example.com"
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(expectedProfile);

        // Act
        var result = await _controller.GetMyProfile();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var profile = okResult.Value.Should().BeAssignableTo<UserProfileDto>().Subject;
        profile.UserId.Should().Be(_testUserId);
        profile.FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetMyProfile_WhenProfileNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync((UserProfileDto?)null);

        // Act
        var result = await _controller.GetMyProfile();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMyProfile_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        // Act
        var result = await _controller.GetMyProfile();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region GetByUserId Tests

    [Fact]
    public async Task GetByUserId_WhenProfileExists_ReturnsOk()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var expectedProfile = new UserProfileDto
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            FirstName = "Bob",
            LastName = "Jones",
            Email = "bob@example.com"
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(targetUserId))
            .ReturnsAsync(expectedProfile);

        // Act
        var result = await _controller.GetByUserId(targetUserId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var profile = okResult.Value.Should().BeAssignableTo<UserProfileDto>().Subject;
        profile.UserId.Should().Be(targetUserId);
    }

    [Fact]
    public async Task GetByUserId_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(targetUserId))
            .ReturnsAsync((UserProfileDto?)null);

        // Act
        var result = await _controller.GetByUserId(targetUserId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region CreateForNewUser Tests

    [Fact]
    public async Task CreateForNewUser_WhenProfileDoesNotExist_CreatesAndReturnsProfile()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        var request = new CreateUserProfileForNewUserRequest
        {
            UserId = newUserId,
            FirstName = "Charlie",
            LastName = "Brown",
            Email = "charlie@example.com"
        };

        var createdProfile = new UserProfileDto
        {
            Id = Guid.NewGuid(),
            UserId = newUserId,
            FirstName = "Charlie",
            LastName = "Brown",
            Email = "charlie@example.com"
        };

        _mockRepository
            .Setup(r => r.UserProfileExistsAsync(newUserId))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<CreateUserProfileRequest>(), newUserId))
            .ReturnsAsync(Guid.NewGuid());

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(newUserId))
            .ReturnsAsync(createdProfile);

        // Act
        var result = await _controller.CreateForNewUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var profile = okResult.Value.Should().BeAssignableTo<UserProfileDto>().Subject;
        profile.UserId.Should().Be(newUserId);
        profile.FirstName.Should().Be("Charlie");
    }

    [Fact]
    public async Task CreateForNewUser_WhenProfileAlreadyExists_ReturnsExistingProfile()
    {
        // Arrange
        var existingUserId = Guid.NewGuid();
        var request = new CreateUserProfileForNewUserRequest
        {
            UserId = existingUserId,
            FirstName = "Diana",
            LastName = "Prince",
            Email = "diana@example.com"
        };

        var existingProfile = new UserProfileDto
        {
            Id = Guid.NewGuid(),
            UserId = existingUserId,
            FirstName = "Diana",
            LastName = "Prince",
            Email = "diana@example.com"
        };

        _mockRepository
            .Setup(r => r.UserProfileExistsAsync(existingUserId))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(existingUserId))
            .ReturnsAsync(existingProfile);

        // Act
        var result = await _controller.CreateForNewUser(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var profile = okResult.Value.Should().BeAssignableTo<UserProfileDto>().Subject;
        profile.UserId.Should().Be(existingUserId);

        // CreateAsync should NOT have been called
        _mockRepository.Verify(r => r.CreateAsync(It.IsAny<CreateUserProfileRequest>(), It.IsAny<Guid?>()), Times.Never);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WhenCreatingOwnProfile_ReturnsCreatedResult()
    {
        // Arrange
        var request = new CreateUserProfileRequest
        {
            UserId = _testUserId,
            FirstName = "Eve",
            LastName = "Adams",
            Email = "eve@example.com"
        };

        var profileId = Guid.NewGuid();
        var createdProfile = new UserProfileDto
        {
            Id = profileId,
            UserId = _testUserId,
            FirstName = "Eve",
            LastName = "Adams",
            Email = "eve@example.com"
        };

        _mockRepository
            .Setup(r => r.UserProfileExistsAsync(_testUserId))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(r => r.CreateAsync(request, _testUserId))
            .ReturnsAsync(profileId);

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(createdProfile);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(UserProfileController.GetByUserId));
        var profile = createdResult.Value.Should().BeAssignableTo<UserProfileDto>().Subject;
        profile.UserId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task Create_WhenCreatingForAnotherUser_ReturnsForbid()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var request = new CreateUserProfileRequest
        {
            UserId = otherUserId,
            FirstName = "Frank",
            LastName = "Castle",
            Email = "frank@example.com"
        };

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Create_WhenProfileAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var request = new CreateUserProfileRequest
        {
            UserId = _testUserId,
            FirstName = "George",
            LastName = "Banks",
            Email = "george@example.com"
        };

        _mockRepository
            .Setup(r => r.UserProfileExistsAsync(_testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    #endregion

    #region UpdateMyProfile Tests

    [Fact]
    public async Task UpdateMyProfile_WhenProfileExists_ReturnsOk()
    {
        // Arrange
        var request = new UpdateUserProfileRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast"
        };

        var updatedProfile = new UserProfileDto
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast"
        };

        _mockRepository
            .Setup(r => r.UpdateAsync(_testUserId, request, _testUserId))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(updatedProfile);

        // Act
        var result = await _controller.UpdateMyProfile(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var profile = okResult.Value.Should().BeAssignableTo<UserProfileDto>().Subject;
        profile.FirstName.Should().Be("UpdatedFirst");
    }

    [Fact]
    public async Task UpdateMyProfile_WhenProfileNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateUserProfileRequest { FirstName = "NoOne" };

        _mockRepository
            .Setup(r => r.UpdateAsync(_testUserId, request, _testUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdateMyProfile(request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteMyProfile Tests

    [Fact]
    public async Task DeleteMyProfile_WhenProfileExists_ReturnsNoContent()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.DeleteAsync(_testUserId, _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteMyProfile();

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteMyProfile_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.DeleteAsync(_testUserId, _testUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteMyProfile();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region End-to-End Flow Tests

    [Fact]
    public async Task EndToEnd_CreateUpdateDeleteProfile_Flow()
    {
        // Step 1: Create profile
        var createRequest = new CreateUserProfileRequest
        {
            UserId = _testUserId,
            FirstName = "Harry",
            LastName = "Potter",
            Email = "harry@hogwarts.com"
        };

        var profileId = Guid.NewGuid();
        var createdProfile = new UserProfileDto
        {
            Id = profileId,
            UserId = _testUserId,
            FirstName = "Harry",
            LastName = "Potter",
            Email = "harry@hogwarts.com"
        };

        _mockRepository
            .Setup(r => r.UserProfileExistsAsync(_testUserId))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(r => r.CreateAsync(createRequest, _testUserId))
            .ReturnsAsync(profileId);

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(createdProfile);

        var createResult = await _controller.Create(createRequest);
        createResult.Result.Should().BeOfType<CreatedAtActionResult>();

        // Step 2: Get profile
        var getResult = await _controller.GetMyProfile();
        var okGet = getResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var fetchedProfile = okGet.Value.Should().BeAssignableTo<UserProfileDto>().Subject;
        fetchedProfile.FirstName.Should().Be("Harry");

        // Step 3: Update profile
        var updateRequest = new UpdateUserProfileRequest { FirstName = "Harry Updated" };
        var updatedProfile = new UserProfileDto
        {
            Id = profileId,
            UserId = _testUserId,
            FirstName = "Harry Updated",
            LastName = "Potter"
        };

        _mockRepository
            .Setup(r => r.UpdateAsync(_testUserId, updateRequest, _testUserId))
            .ReturnsAsync(true);

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(updatedProfile);

        var updateResult = await _controller.UpdateMyProfile(updateRequest);
        var okUpdate = updateResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var afterUpdate = okUpdate.Value.Should().BeAssignableTo<UserProfileDto>().Subject;
        afterUpdate.FirstName.Should().Be("Harry Updated");

        // Step 4: Delete profile
        _mockRepository
            .Setup(r => r.DeleteAsync(_testUserId, _testUserId))
            .ReturnsAsync(true);

        var deleteResult = await _controller.DeleteMyProfile();
        deleteResult.Should().BeOfType<NoContentResult>();
    }

    #endregion
}
