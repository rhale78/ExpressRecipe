using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.MealPlanningService.Controllers;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Tests.Helpers;

namespace ExpressRecipe.MealPlanningService.Tests.Controllers;

public class MealPlanningControllerTests
{
    private readonly Mock<IMealPlanningRepository> _mockRepository;
    private readonly Mock<ILogger<MealPlanningController>> _mockLogger;
    private readonly MealPlanningController _controller;
    private readonly Guid _testUserId;

    public MealPlanningControllerTests()
    {
        _mockRepository = new Mock<IMealPlanningRepository>();
        _mockLogger = new Mock<ILogger<MealPlanningController>>();
        _controller = new MealPlanningController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    // ──────────── GetMealPlan ────────────

    [Fact]
    public async Task GetMealPlan_WhenPlanExists_ReturnsOk()
    {
        var planId = Guid.NewGuid();
        var plan = new MealPlanDto { Id = planId, UserId = _testUserId, Name = "Test Plan", Status = "Active" };
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync(plan);

        var result = await _controller.GetMealPlan(planId);

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(plan);
    }

    [Fact]
    public async Task GetMealPlan_WhenPlanNotFound_ReturnsNotFound()
    {
        var planId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync((MealPlanDto?)null);

        var result = await _controller.GetMealPlan(planId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMealPlan_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var result = await _controller.GetMealPlan(Guid.NewGuid());
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ──────────── CreateMealPlan ────────────

    [Fact]
    public async Task CreateMealPlan_WithValidRequest_ReturnsOkWithId()
    {
        var planId = Guid.NewGuid();
        var request = new CreateMealPlanApiRequest
        {
            Name = "My Plan",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(6)
        };
        _mockRepository.Setup(r => r.CreateMealPlanAsync(_testUserId, request.StartDate, request.EndDate, request.Name))
                       .ReturnsAsync(planId);

        var result = await _controller.CreateMealPlan(request);

        result.Should().BeOfType<OkObjectResult>();
        var body = ((OkObjectResult)result).Value;
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateMealPlan_WithEmptyName_ReturnsBadRequest()
    {
        var request = new CreateMealPlanApiRequest { Name = "", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(6) };

        var result = await _controller.CreateMealPlan(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateMealPlan_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var result = await _controller.CreateMealPlan(new CreateMealPlanApiRequest { Name = "X", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(6) });
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ──────────── DeleteMealPlan ────────────

    [Fact]
    public async Task DeleteMealPlan_WhenPlanExists_ReturnsNoContent()
    {
        var planId = Guid.NewGuid();
        var plan = new MealPlanDto { Id = planId, UserId = _testUserId };
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync(plan);
        _mockRepository.Setup(r => r.DeleteMealPlanAsync(planId, _testUserId)).Returns(Task.CompletedTask);

        var result = await _controller.DeleteMealPlan(planId);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.DeleteMealPlanAsync(planId, _testUserId), Times.Once);
    }

    [Fact]
    public async Task DeleteMealPlan_WhenPlanNotFound_ReturnsNotFound()
    {
        var planId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync((MealPlanDto?)null);

        var result = await _controller.DeleteMealPlan(planId);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepository.Verify(r => r.DeleteMealPlanAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    // ──────────── GetMealPlanSummary ────────────

    [Fact]
    public async Task GetMealPlanSummary_WhenAuthenticated_ReturnsOk()
    {
        var summary = new MealPlanSummaryData { TotalActivePlans = 2, MealsThisWeek = 5 };
        _mockRepository.Setup(r => r.GetMealPlanSummaryAsync(_testUserId)).ReturnsAsync(summary);

        var result = await _controller.GetMealPlanSummary();

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(summary);
    }

    // ──────────── SearchMealPlans ────────────

    [Fact]
    public async Task SearchMealPlans_ReturnsPaginatedResults()
    {
        var plans = Enumerable.Range(0, 5).Select(i => new MealPlanDto
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = $"Plan {i}",
            Status = "Active"
        }).ToList();

        _mockRepository.Setup(r => r.GetUserMealPlansAsync(_testUserId, "Active")).ReturnsAsync(plans);

        var result = await _controller.SearchMealPlans(new SearchMealPlansRequest { Status = "Active", Page = 1, PageSize = 3 });

        result.Should().BeOfType<OkObjectResult>();
    }

    // ──────────── CompleteMealPlan ────────────

    [Fact]
    public async Task CompleteMealPlan_WhenPlanExists_ReturnsNoContent()
    {
        var planId = Guid.NewGuid();
        var plan = new MealPlanDto { Id = planId, UserId = _testUserId, Status = "Active" };
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync(plan);
        _mockRepository.Setup(r => r.SetMealPlanStatusAsync(planId, _testUserId, "Completed")).Returns(Task.CompletedTask);

        var result = await _controller.CompleteMealPlan(planId);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.SetMealPlanStatusAsync(planId, _testUserId, "Completed"), Times.Once);
    }

    [Fact]
    public async Task ArchiveMealPlan_WhenPlanExists_ReturnsNoContent()
    {
        var planId = Guid.NewGuid();
        var plan = new MealPlanDto { Id = planId, UserId = _testUserId, Status = "Active" };
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync(plan);
        _mockRepository.Setup(r => r.SetMealPlanStatusAsync(planId, _testUserId, "Archived")).Returns(Task.CompletedTask);

        var result = await _controller.ArchiveMealPlan(planId);

        result.Should().BeOfType<NoContentResult>();
    }

    // ──────────── AddMealEntry ────────────

    [Fact]
    public async Task AddMealEntry_WithValidRequest_ReturnsOkWithEntryId()
    {
        var planId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var plan = new MealPlanDto { Id = planId, UserId = _testUserId };
        var request = new AddMealEntryApiRequest
        {
            MealPlanId = planId,
            Date = DateTime.Today,
            MealType = "Dinner",
            RecipeId = Guid.NewGuid(),
            Servings = 4
        };

        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync(plan);
        _mockRepository.Setup(r => r.AddPlannedMealAsync(planId, _testUserId, request.RecipeId, request.Date, request.MealType, request.Servings, null, null))
                       .ReturnsAsync(entryId);

        var result = await _controller.AddMealEntry(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddMealEntry_WhenPlanNotFound_ReturnsNotFound()
    {
        var planId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync((MealPlanDto?)null);

        var result = await _controller.AddMealEntry(new AddMealEntryApiRequest { MealPlanId = planId, Date = DateTime.Today, MealType = "Dinner", Servings = 2 });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ──────────── DeleteMealEntry ────────────

    [Fact]
    public async Task DeleteMealEntry_WhenEntryOwnedByUser_ReturnsNoContent()
    {
        var entryId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var entry = new PlannedMealDto { Id = entryId, MealPlanId = planId };
        var plan = new MealPlanDto { Id = planId, UserId = _testUserId };

        _mockRepository.Setup(r => r.GetPlannedMealAsync(entryId)).ReturnsAsync(entry);
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync(plan);
        _mockRepository.Setup(r => r.RemovePlannedMealAsync(entryId)).Returns(Task.CompletedTask);

        var result = await _controller.DeleteMealEntry(entryId);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.RemovePlannedMealAsync(entryId), Times.Once);
    }

    [Fact]
    public async Task DeleteMealEntry_WhenEntryNotFound_ReturnsNotFound()
    {
        var entryId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetPlannedMealAsync(entryId)).ReturnsAsync((PlannedMealDto?)null);

        var result = await _controller.DeleteMealEntry(entryId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteMealEntry_WhenPlanOwnedByOtherUser_ReturnsForbid()
    {
        var entryId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var entry = new PlannedMealDto { Id = entryId, MealPlanId = planId };

        _mockRepository.Setup(r => r.GetPlannedMealAsync(entryId)).ReturnsAsync(entry);
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync((MealPlanDto?)null);

        var result = await _controller.DeleteMealEntry(entryId);

        result.Should().BeOfType<ForbidResult>();
    }

    // ──────────── MarkMealPrepared ────────────

    [Fact]
    public async Task MarkMealPrepared_WhenEntryOwnedByUser_ReturnsNoContent()
    {
        var entryId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var entry = new PlannedMealDto { Id = entryId, MealPlanId = planId };
        var plan = new MealPlanDto { Id = planId, UserId = _testUserId };

        _mockRepository.Setup(r => r.GetPlannedMealAsync(entryId)).ReturnsAsync(entry);
        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync(plan);
        _mockRepository.Setup(r => r.MarkMealAsPreparedAsync(entryId, true)).Returns(Task.CompletedTask);

        var result = await _controller.MarkMealPrepared(new MarkMealPreparedApiRequest { EntryId = entryId, IsPrepared = true });

        result.Should().BeOfType<NoContentResult>();
    }

    // ──────────── UpdateMealPlan ────────────

    [Fact]
    public async Task UpdateMealPlan_WhenPlanExists_ReturnsNoContent()
    {
        var planId = Guid.NewGuid();
        var plan = new MealPlanDto { Id = planId, UserId = _testUserId };
        var request = new UpdateMealPlanApiRequest
        {
            Name = "Updated Plan",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(6),
            Status = "Active"
        };

        _mockRepository.Setup(r => r.GetMealPlanAsync(planId, _testUserId)).ReturnsAsync(plan);
        _mockRepository.Setup(r => r.UpdateMealPlanAsync(planId, _testUserId, request.Name, request.StartDate, request.EndDate, "Active"))
                       .Returns(Task.CompletedTask);

        var result = await _controller.UpdateMealPlan(planId, request);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateMealPlan_WhenPlanNotFound_ReturnsNotFound()
    {
        _mockRepository.Setup(r => r.GetMealPlanAsync(It.IsAny<Guid>(), _testUserId)).ReturnsAsync((MealPlanDto?)null);

        var result = await _controller.UpdateMealPlan(Guid.NewGuid(), new UpdateMealPlanApiRequest
        {
            Name = "Test",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(6)
        });

        result.Should().BeOfType<NotFoundResult>();
    }

    // ──────────── CreateQuickMealPlan ────────────

    [Fact]
    public async Task CreateQuickMealPlan_WhenAuthenticated_ReturnsOkWithId()
    {
        var planId = Guid.NewGuid();
        _mockRepository.Setup(r => r.CreateMealPlanAsync(_testUserId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                       .ReturnsAsync(planId);

        var result = await _controller.CreateQuickMealPlan(new QuickMealPlanApiRequest
        {
            StartDate = DateTime.Today,
            DurationDays = 7
        });

        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.CreateMealPlanAsync(_testUserId, DateTime.Today, DateTime.Today.AddDays(6), It.IsAny<string>()), Times.Once);
    }
}
