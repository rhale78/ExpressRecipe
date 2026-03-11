using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class AllergyControllerTests
{
    private readonly Mock<IAllergyIncidentRepository>    _mockRepo;
    private readonly Mock<IFamilyMemberRepository>       _mockFamilyRepo;
    private readonly Mock<AllergyDifferentialAnalyzer>   _mockAnalyzer;
    private readonly Mock<ILogger<AllergyController>>    _mockLogger;
    private readonly AllergyController                   _controller;
    private readonly Guid                                _testUserId;

    public AllergyControllerTests()
    {
        _mockRepo       = new Mock<IAllergyIncidentRepository>();
        _mockFamilyRepo = new Mock<IFamilyMemberRepository>();
        _mockLogger     = new Mock<ILogger<AllergyController>>();

        // AllergyDifferentialAnalyzer needs concrete mocks — use a mock with all dependencies mocked
        _mockAnalyzer = new Mock<AllergyDifferentialAnalyzer>(
            MockBehavior.Loose,
            _mockRepo.Object,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILogger<AllergyDifferentialAnalyzer>>());

        _controller = new AllergyController(
            _mockRepo.Object,
            _mockFamilyRepo.Object,
            _mockAnalyzer.Object,
            _mockLogger.Object);

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);
    }

    // ─── CreateIncident ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateIncident_WhenUnauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        var result = await _controller.CreateIncident(new CreateAllergyIncidentV2Request
        {
            Products = new() { new() { ProductName = "Peanut Butter" } },
            Members  = new() { new() { MemberName = "Me", Severity = "Mild" } }
        }, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateIncident_WithValidRequest_ReturnsCreated()
    {
        var incidentId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.CreateIncidentAsync(_testUserId, It.IsAny<CreateAllergyIncidentV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(incidentId);

        var result = await _controller.CreateIncident(new CreateAllergyIncidentV2Request
        {
            Products = new() { new() { ProductName = "Peanut Butter" } },
            Members  = new() { new() { MemberName = "Me", Severity = "Mild" } }
        }, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.RouteValues!["id"].Should().Be(incidentId);
    }

    // ─── GetIncidents ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIncidents_WhenUnauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);
        var result = await _controller.GetIncidents(limit: 50);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetIncidents_LimitTooHigh_ReturnsBadRequest()
    {
        var result = await _controller.GetIncidents(limit: 300);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetIncidents_LimitZero_ReturnsBadRequest()
    {
        var result = await _controller.GetIncidents(limit: 0);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetIncidents_ValidRequest_ReturnsOk()
    {
        _mockRepo
            .Setup(r => r.GetIncidentsAsync(_testUserId, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AllergyIncidentV2Dto>());

        var result = await _controller.GetIncidents(memberId: null, limit: 50);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ─── GetIncident (by id) ───────────────────────────────────────────────────

    [Fact]
    public async Task GetIncident_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetIncidentByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AllergyIncidentV2Dto?)null);

        var result = await _controller.GetIncident(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetIncident_BelongingToOtherUser_ReturnsForbid()
    {
        var id       = Guid.NewGuid();
        var otherId  = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetIncidentByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AllergyIncidentV2Dto { Id = id, HouseholdId = otherId });

        var result = await _controller.GetIncident(id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetIncident_OwnedByCurrentUser_ReturnsOk()
    {
        var id = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetIncidentByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AllergyIncidentV2Dto { Id = id, HouseholdId = _testUserId });

        var result = await _controller.GetIncident(id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ─── GetSuspects ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSuspects_WhenUnauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);
        var result = await _controller.GetSuspects();
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSuspects_ReturnsOkWithList()
    {
        _mockRepo
            .Setup(r => r.GetSuspectedAllergensAsync(_testUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SuspectedAllergenDto>());

        var result = await _controller.GetSuspects();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ─── ConfirmSuspect ────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmSuspect_WhenUnauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);
        var result = await _controller.ConfirmSuspect(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ConfirmSuspect_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetSuspectedAllergenByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SuspectedAllergenDto?)null);

        var result = await _controller.ConfirmSuspect(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ConfirmSuspect_OwnedByOtherHousehold_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetSuspectedAllergenByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SuspectedAllergenDto { Id = id, HouseholdId = Guid.NewGuid() });

        var result = await _controller.ConfirmSuspect(id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ConfirmSuspect_OwnedByCurrentUser_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetSuspectedAllergenByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SuspectedAllergenDto { Id = id, HouseholdId = _testUserId });

        var result = await _controller.ConfirmSuspect(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.PromoteSuspectedAllergenAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── ClearSuspect ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearSuspect_WhenUnauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);
        var result = await _controller.ClearSuspect(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ClearSuspect_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetSuspectedAllergenByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SuspectedAllergenDto?)null);

        var result = await _controller.ClearSuspect(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ClearSuspect_OwnedByOtherHousehold_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetSuspectedAllergenByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SuspectedAllergenDto { Id = id, HouseholdId = Guid.NewGuid() });

        var result = await _controller.ClearSuspect(id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ClearSuspect_OwnedByCurrentUser_InvokesTransactionalClear_AndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.GetSuspectedAllergenByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SuspectedAllergenDto { Id = id, HouseholdId = _testUserId });

        var result = await _controller.ClearSuspect(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(
            r => r.ClearSuspectTransactionalAsync(id, _testUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── GetReport ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReport_WhenUnauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);
        var result = await _controller.GetReport(null, CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetReport_ForSelf_ReturnsOkWithPopulatedReport()
    {
        _mockRepo.Setup(r => r.GetSuspectedAllergensAsync(_testUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SuspectedAllergenDto>
            {
                new() { Id = Guid.NewGuid(), HouseholdId = _testUserId, IngredientName = "Peanuts",
                        ConfidenceScore = 0.80m, IsPromotedToConfirmed = false }
            });
        _mockRepo.Setup(r => r.GetConfirmedAllergensAsync(_testUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConfirmedAllergenDto>());
        _mockRepo.Setup(r => r.GetIncidentsAsync(_testUserId, null, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AllergyIncidentV2Dto>());
        _mockRepo.Setup(r => r.GetClearedIngredientsAsync(_testUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClearedIngredientDto>());

        var result = await _controller.GetReport(null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var report = ok.Value.Should().BeAssignableTo<AllergyReportModel>().Subject;
        report.MemberName.Should().Be("Me");
        report.SuspectedAllergens.Should().HaveCount(1);
        report.SuspectedAllergens[0].IngredientName.Should().Be("Peanuts");
    }

    [Fact]
    public async Task GetReport_ForFamilyMemberBelongingToOtherUser_ReturnsForbid()
    {
        var memberId    = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _mockFamilyRepo
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync(new ExpressRecipe.Shared.DTOs.User.FamilyMemberDto
            {
                Id            = memberId,
                Name          = "Child",
                PrimaryUserId = otherUserId   // belongs to a different user
            });

        var result = await _controller.GetReport(memberId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetReport_ForNonExistentMember_ReturnsNotFound()
    {
        var memberId = Guid.NewGuid();
        _mockFamilyRepo
            .Setup(r => r.GetByIdAsync(memberId))
            .ReturnsAsync((ExpressRecipe.Shared.DTOs.User.FamilyMemberDto?)null);

        var result = await _controller.GetReport(memberId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
