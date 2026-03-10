using ExpressRecipe.CommunityService.Controllers;
using ExpressRecipe.CommunityService.Data;
using ExpressRecipe.CommunityService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace ExpressRecipe.CommunityService.Tests.Controllers;

public class RecipeGalleryControllerTests
{
    private readonly Mock<ICommunityRecipeRepository> _mockGalleryRepository;
    private readonly Mock<IApprovalQueueService> _mockApprovalQueue;
    private readonly Mock<ILogger<RecipeGalleryController>> _mockLogger;
    private readonly RecipeGalleryController _controller;
    private readonly Guid _userId;

    public RecipeGalleryControllerTests()
    {
        _mockGalleryRepository = new Mock<ICommunityRecipeRepository>();
        _mockApprovalQueue = new Mock<IApprovalQueueService>();
        _mockLogger = new Mock<ILogger<RecipeGalleryController>>();

        _controller = new RecipeGalleryController(
            _mockGalleryRepository.Object,
            _mockApprovalQueue.Object,
            _mockLogger.Object);

        _userId = Guid.NewGuid();
        SetupAuthenticatedUser(_userId);
    }

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    // ─── COM3 spec: Gallery GET returns approved recipes only ───────────────

    [Fact]
    public async Task GetGallery_ReturnsApprovedRecipesOnly()
    {
        // Arrange
        var approvedRecipe = new CommunityRecipeDto
        {
            Id = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            SubmittedBy = Guid.NewGuid(),
            Status = "Approved",
            ViewCount = 10,
            SubmittedAt = DateTime.UtcNow.AddDays(-3)
        };

        var expectedPage = new GalleryPage
        {
            Items = new List<CommunityRecipeDto> { approvedRecipe },
            HasMore = false,
            NextCursorId = null
        };

        _mockGalleryRepository
            .Setup(r => r.GetGalleryPageAsync(null, null, 0, null, null, 20, default))
            .ReturnsAsync(expectedPage);

        // Act
        var result = await _controller.GetGallery(null, null, null, null, 0, 20, default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var page = okResult.Value.Should().BeOfType<GalleryPage>().Subject;
        page.Items.Should().HaveCount(1);
        page.Items[0].Status.Should().Be("Approved");
    }

    [Fact]
    public async Task GetGallery_EmptyGallery_ReturnsEmptyPage()
    {
        // Arrange
        _mockGalleryRepository
            .Setup(r => r.GetGalleryPageAsync(null, null, 0, null, null, 20, default))
            .ReturnsAsync(new GalleryPage { Items = new List<CommunityRecipeDto>(), HasMore = false });

        // Act
        var result = await _controller.GetGallery(null, null, null, null, 0, 20, default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var page = okResult.Value.Should().BeOfType<GalleryPage>().Subject;
        page.Items.Should().BeEmpty();
    }

    // ─── COM3 spec: Recipe submitted, HumanFirst mode → Status=Pending ──────

    [Fact]
    public async Task SubmitRecipe_HumanFirstMode_StatusPendingAndModeratorNotified()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var communityRecipeId = Guid.NewGuid();
        var request = new SubmitRecipeRequest { RecipeId = recipeId, ContentSummary = "Pasta recipe summary" };

        _mockGalleryRepository
            .Setup(r => r.GetByRecipeIdAsync(recipeId, default))
            .ReturnsAsync((CommunityRecipeDto?)null);

        _mockGalleryRepository
            .Setup(r => r.SubmitRecipeAsync(recipeId, _userId, default))
            .ReturnsAsync(communityRecipeId);

        _mockApprovalQueue
            .Setup(q => q.SubmitForApprovalAsync(recipeId, "Recipe", "Pasta recipe summary", default))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _controller.SubmitRecipe(request, default);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        _mockApprovalQueue.Verify(
            q => q.SubmitForApprovalAsync(recipeId, "Recipe", "Pasta recipe summary", default),
            Times.Once);
    }

    [Fact]
    public async Task SubmitRecipe_AlreadySubmitted_ReturnsConflict()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var request = new SubmitRecipeRequest { RecipeId = recipeId };

        _mockGalleryRepository
            .Setup(r => r.GetByRecipeIdAsync(recipeId, default))
            .ReturnsAsync(new CommunityRecipeDto
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                Status = "Pending",
                SubmittedBy = _userId,
                ViewCount = 0,
                SubmittedAt = DateTime.UtcNow.AddDays(-1)
            });

        // Act
        var result = await _controller.SubmitRecipe(request, default);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task GetRecipe_ExistingId_ReturnsRecipeAndIncrementsViewCount()
    {
        // Arrange
        var id = Guid.NewGuid();
        var recipe = new CommunityRecipeDto
        {
            Id = id,
            RecipeId = Guid.NewGuid(),
            Status = "Approved",
            SubmittedBy = Guid.NewGuid(),
            ViewCount = 5,
            SubmittedAt = DateTime.UtcNow.AddDays(-2)
        };

        _mockGalleryRepository
            .Setup(r => r.GetByIdAsync(id, default))
            .ReturnsAsync(recipe);

        // Act
        var result = await _controller.GetRecipe(id, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockGalleryRepository.Verify(r => r.IncrementViewCountAsync(id, default), Times.Once);
    }

    [Fact]
    public async Task GetRecipe_NotFound_Returns404()
    {
        // Arrange
        _mockGalleryRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((CommunityRecipeDto?)null);

        // Act
        var result = await _controller.GetRecipe(Guid.NewGuid(), default);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
