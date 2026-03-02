using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class FamilyMembersControllerTests
{
    private readonly Mock<IFamilyMemberRepository> _mockRepository;
    private readonly Mock<IFamilyRelationshipRepository> _mockRelationshipRepository;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<FamilyMembersController>> _mockLogger;
    private readonly FamilyMembersController _controller;
    private readonly Guid _testUserId;

    public FamilyMembersControllerTests()
    {
        _mockRepository = new Mock<IFamilyMemberRepository>();
        _mockRelationshipRepository = new Mock<IFamilyRelationshipRepository>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<FamilyMembersController>>();
        
        _controller = new FamilyMembersController(
            _mockRepository.Object,
            _mockRelationshipRepository.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object
        );

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);
    }

    #region GetMyFamilyMembers Tests

    [Fact]
    public async Task GetMyFamilyMembers_WithAuthenticatedUser_ReturnsOkWithFamilyMembers()
    {
        // Arrange
        var expectedMembers = new List<FamilyMemberDto>
        {
            new FamilyMemberDto { Id = Guid.NewGuid(), Name = "John Doe", PrimaryUserId = _testUserId },
            new FamilyMemberDto { Id = Guid.NewGuid(), Name = "Jane Doe", PrimaryUserId = _testUserId }
        };

        _mockRepository
            .Setup(r => r.GetByPrimaryUserIdAsync(_testUserId))
            .ReturnsAsync(expectedMembers);

        // Act
        var result = await _controller.GetMyFamilyMembers();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualMembers = okResult.Value.Should().BeAssignableTo<List<FamilyMemberDto>>().Subject;
        actualMembers.Should().HaveCount(2);
        actualMembers.Should().BeEquivalentTo(expectedMembers);
    }

    [Fact]
    public async Task GetMyFamilyMembers_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        // Act
        var result = await _controller.GetMyFamilyMembers();

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetMyFamilyMembers_WhenRepositoryThrows_ReturnsInternalServerError()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByPrimaryUserIdAsync(_testUserId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetMyFamilyMembers();

        // Assert
        result.Should().NotBeNull();
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WithValidIdAndOwnership_ReturnsOkWithFamilyMember()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var expectedMember = new FamilyMemberDto 
        { 
            Id = memberId, 
            Name = "John Doe", 
            PrimaryUserId = _testUserId 
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(expectedMember);

        // Act
        var result = await _controller.GetById(memberId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualMember = okResult.Value.Should().BeAssignableTo<FamilyMemberDto>().Subject;
        actualMember.Should().BeEquivalentTo(expectedMember);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync((FamilyMemberDto?)null);

        // Act
        var result = await _controller.GetById(memberId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_WithoutOwnership_ReturnsForbidden()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var member = new FamilyMemberDto 
        { 
            Id = memberId, 
            Name = "John Doe", 
            PrimaryUserId = otherUserId // Different user
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(member);

        // Act
        var result = await _controller.GetById(memberId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var request = new CreateFamilyMemberRequest
        {
            Name = "John Doe",
            Relationship = "Son",
            DateOfBirth = new DateTime(2010, 1, 1),
            UserRole = "Member"
        };

        var createdId = Guid.NewGuid();
        var createdMember = new FamilyMemberDto
        {
            Id = createdId,
            Name = request.Name,
            Relationship = request.Relationship,
            PrimaryUserId = _testUserId
        };

        _mockRepository
            .Setup(r => r.CreateAsync(_testUserId, request, _testUserId))
            .ReturnsAsync(createdId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(createdId))
            .ReturnsAsync(createdMember);

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Should().NotBeNull();
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(FamilyMembersController.GetById));
        var actualMember = createdResult.Value.Should().BeAssignableTo<FamilyMemberDto>().Subject;
        actualMember.Name.Should().Be(request.Name);
    }

    #endregion

    #region CreateWithAccount Tests

    [Fact]
    public async Task CreateWithAccount_WithValidRequest_CreatesUserAndReturnsCreatedAtAction()
    {
        // Arrange
        var request = new CreateFamilyMemberWithAccountRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Password = "Password123!",
            Relationship = "Son",
            UserRole = "Member",
            SendWelcomeEmail = true
        };

        var createdUserId = Guid.NewGuid();
        var createdMemberId = Guid.NewGuid();

        // Mock current user as Admin
        var adminMember = new FamilyMemberDto { Id = Guid.NewGuid(), PrimaryUserId = _testUserId, UserRole = "Admin" };
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(adminMember);

        // Mock AuthService HTTP response
        var authResponseContent = JsonSerializer.Serialize(new { userId = createdUserId.ToString(), email = request.Email });
        var authHttpMessageHandler = new Mock<HttpMessageHandler>();
        authHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri != null && req.RequestUri.ToString().Contains("register-internal")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(authResponseContent, Encoding.UTF8, "application/json")
            });

        var authHttpClient = new HttpClient(authHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        // Mock NotificationService HTTP response
        var notificationHttpMessageHandler = new Mock<HttpMessageHandler>();
        notificationHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri != null && req.RequestUri.ToString().Contains("send-email")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"message\":\"Email queued\"}", Encoding.UTF8, "application/json")
            });

        var notificationHttpClient = new HttpClient(notificationHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5015")
        };

        _mockHttpClientFactory
            .Setup(f => f.CreateClient("AuthService"))
            .Returns(authHttpClient);

        _mockHttpClientFactory
            .Setup(f => f.CreateClient("NotificationService"))
            .Returns(notificationHttpClient);

        _mockRepository
            .Setup(r => r.CreateWithAccountAsync(_testUserId, request, createdUserId, _testUserId))
            .ReturnsAsync(createdMemberId);

        var createdMember = new FamilyMemberDto
        {
            Id = createdMemberId,
            Name = request.Name,
            Email = request.Email,
            HasUserAccount = true,
            UserId = createdUserId,
            PrimaryUserId = _testUserId
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(createdMemberId))
            .ReturnsAsync(createdMember);

        // Act
        var result = await _controller.CreateWithAccount(request);

        // Assert
        result.Should().NotBeNull();
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var actualMember = createdResult.Value.Should().BeAssignableTo<FamilyMemberDto>().Subject;
        actualMember.HasUserAccount.Should().BeTrue();
        actualMember.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task CreateWithAccount_WhenAuthServiceFails_ReturnsInternalServerError()
    {
        // Arrange
        var request = new CreateFamilyMemberWithAccountRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Password = "Password123!",
            UserRole = "Member"
        };

        var authHttpMessageHandler = new Mock<HttpMessageHandler>();
        authHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            });

        var authHttpClient = new HttpClient(authHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        _mockHttpClientFactory
            .Setup(f => f.CreateClient("AuthService"))
            .Returns(authHttpClient);

        // Mock current user as Admin
        var adminMember = new FamilyMemberDto { Id = Guid.NewGuid(), PrimaryUserId = _testUserId, UserRole = "Admin" };
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(adminMember);

        // Act
        var result = await _controller.CreateWithAccount(request);

        // Assert
        result.Should().NotBeNull();
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_WithValidRequestAndOwnership_ReturnsOk()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var request = new UpdateFamilyMemberRequest
        {
            Name = "Updated Name",
            Relationship = "Daughter",
            UserRole = "Member"
        };

        var existingMember = new FamilyMemberDto
        {
            Id = memberId,
            Name = "Old Name",
            PrimaryUserId = _testUserId
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(existingMember);

        _mockRepository
            .Setup(r => r.UpdateAsync(memberId, request, _testUserId))
            .ReturnsAsync(true);

        var updatedMember = new FamilyMemberDto
        {
            Id = memberId,
            Name = request.Name,
            Relationship = request.Relationship,
            PrimaryUserId = _testUserId
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(updatedMember);

        // Act
        var result = await _controller.Update(memberId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualMember = okResult.Value.Should().BeAssignableTo<FamilyMemberDto>().Subject;
        actualMember.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task Update_WithoutOwnership_ReturnsForbidden()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var request = new UpdateFamilyMemberRequest { Name = "Updated Name" };

        var existingMember = new FamilyMemberDto
        {
            Id = memberId,
            Name = "Old Name",
            PrimaryUserId = otherUserId // Different user
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(existingMember);

        // Act
        var result = await _controller.Update(memberId, request);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_WithValidIdAndOwnership_ReturnsNoContent()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var member = new FamilyMemberDto
        {
            Id = memberId,
            Name = "John Doe",
            PrimaryUserId = _testUserId
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(member);

        _mockRepository
            .Setup(r => r.DeleteAsync(memberId, _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(memberId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        
        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync((FamilyMemberDto?)null);

        // Act
        var result = await _controller.Delete(memberId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DismissGuest Tests

    [Fact]
    public async Task DismissGuest_WithValidGuestMember_ReturnsNoContent()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var request = new DismissGuestRequest { Reason = "Event ended" };

        var guestMember = new FamilyMemberDto
        {
            Id = memberId,
            Name = "Guest User",
            PrimaryUserId = _testUserId,
            IsGuest = true
        };

        // Mock current user as Admin
        var adminMember = new FamilyMemberDto { Id = Guid.NewGuid(), PrimaryUserId = _testUserId, UserRole = "Admin" };
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(adminMember);

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(guestMember);

        _mockRepository
            .Setup(r => r.DismissGuestAsync(memberId, _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DismissGuest(memberId, request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DismissGuest_WithNonGuestMember_ReturnsBadRequest()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var request = new DismissGuestRequest { Reason = "Not applicable" };

        var regularMember = new FamilyMemberDto
        {
            Id = memberId,
            Name = "Regular User",
            PrimaryUserId = _testUserId,
            IsGuest = false
        };

        // Mock current user as Admin
        var adminMember2 = new FamilyMemberDto { Id = Guid.NewGuid(), PrimaryUserId = _testUserId, UserRole = "Admin" };
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(adminMember2);

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(regularMember);

        // Act
        var result = await _controller.DismissGuest(memberId, request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetRelationships Tests

    [Fact]
    public async Task GetRelationships_WithValidIdAndOwnership_ReturnsOkWithRelationships()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var member = new FamilyMemberDto
        {
            Id = memberId,
            Name = "John Doe",
            PrimaryUserId = _testUserId
        };

        var relationships = new List<FamilyRelationshipDto>
        {
            new FamilyRelationshipDto 
            { 
                Id = Guid.NewGuid(), 
                FamilyMemberId = memberId, 
                RelatedMemberId = Guid.NewGuid(),
                RelatedMemberName = "Jane Doe",
                RelationshipType = "Spouse" 
            }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(member);

        _mockRelationshipRepository
            .Setup(r => r.GetByFamilyMemberIdAsync(memberId))
            .ReturnsAsync(relationships);

        // Act
        var result = await _controller.GetRelationships(memberId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualRelationships = okResult.Value.Should().BeAssignableTo<List<FamilyRelationshipDto>>().Subject;
        actualRelationships.Should().HaveCount(1);
        actualRelationships[0].RelationshipType.Should().Be("Spouse");
    }

    #endregion

    #region CreateRelationship Tests

    [Fact]
    public async Task CreateRelationship_WithValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var memberId1 = Guid.NewGuid();
        var memberId2 = Guid.NewGuid();
        var request = new CreateFamilyRelationshipRequest
        {
            FamilyMemberId2 = memberId2,
            RelationshipType = "Parent",
            Notes = "Test relationship"
        };

        var member1 = new FamilyMemberDto { Id = memberId1, PrimaryUserId = _testUserId };
        var member2 = new FamilyMemberDto { Id = memberId2, PrimaryUserId = _testUserId };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId1))
            .ReturnsAsync(member1);

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId2))
            .ReturnsAsync(member2);

        var createdRelationshipId = Guid.NewGuid();
        _mockRelationshipRepository
            .Setup(r => r.CreateAsync(memberId1, request, _testUserId))
            .ReturnsAsync(createdRelationshipId);

        var createdRelationship = new FamilyRelationshipDto
        {
            Id = createdRelationshipId,
            FamilyMemberId = memberId1,
            RelatedMemberId = memberId2,
            RelationshipType = request.RelationshipType
        };

        _mockRelationshipRepository
            .Setup(r => r.GetByIdAsync(createdRelationshipId))
            .ReturnsAsync(createdRelationship);

        // Act
        var result = await _controller.CreateRelationship(memberId1, request);

        // Assert
        result.Should().NotBeNull();
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var actualRelationship = createdResult.Value.Should().BeAssignableTo<FamilyRelationshipDto>().Subject;
        actualRelationship.RelationshipType.Should().Be(request.RelationshipType);
    }

    [Fact]
    public async Task CreateRelationship_WhenMemberNotFound_ReturnsNotFound()
    {
        // Arrange
        var memberId1 = Guid.NewGuid();
        var memberId2 = Guid.NewGuid();
        var request = new CreateFamilyRelationshipRequest
        {
            FamilyMemberId2 = memberId2,
            RelationshipType = "Parent"
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId1))
            .ReturnsAsync((FamilyMemberDto?)null);

        // Act
        var result = await _controller.CreateRelationship(memberId1, request);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteRelationship Tests

    [Fact]
    public async Task DeleteRelationship_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var relationship = new FamilyRelationshipDto
        {
            Id = relationshipId,
            FamilyMemberId = memberId,
            RelatedMemberId = Guid.NewGuid(),
            RelationshipType = "Spouse"
        };

        var member = new FamilyMemberDto
        {
            Id = memberId,
            PrimaryUserId = _testUserId
        };

        _mockRelationshipRepository
            .Setup(r => r.GetByIdAsync(relationshipId))
            .ReturnsAsync(relationship);

        _mockRepository
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(member);

        _mockRelationshipRepository
            .Setup(r => r.DeleteAsync(relationshipId, _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteRelationship(relationshipId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteRelationship_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var relationshipId = Guid.NewGuid();

        _mockRelationshipRepository
            .Setup(r => r.GetByIdAsync(relationshipId))
            .ReturnsAsync((FamilyRelationshipDto?)null);

        // Act
        var result = await _controller.DeleteRelationship(relationshipId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion
}
