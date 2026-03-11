using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class AllergyControllerTests
{
    private readonly Mock<IAllergyIncidentRepository>    _mockRepo;
    private readonly Mock<IFamilyMemberRepository>       _mockFamilyRepo;
    private readonly Mock<IEnhancedAllergenRepository>   _mockAllergenRepo;
    private readonly Mock<IHttpClientFactory>            _mockHttpFactory;
    private readonly AllergyDifferentialAnalyzer         _analyzer;
    private readonly AllergyController                   _controller;
    private readonly Guid                                _testUserId;

    public AllergyControllerTests()
    {
        _mockRepo        = new Mock<IAllergyIncidentRepository>();
        _mockFamilyRepo  = new Mock<IFamilyMemberRepository>();
        _mockAllergenRepo = new Mock<IEnhancedAllergenRepository>();
        _mockHttpFactory = new Mock<IHttpClientFactory>();

        // Wire up a fake HTTP handler so the analyzer can be constructed without side-effects
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        _mockHttpFactory
            .Setup(f => f.CreateClient("NotificationService"))
            .Returns(new HttpClient(handler.Object));

        _analyzer = new AllergyDifferentialAnalyzer(
            _mockRepo.Object,
            _mockHttpFactory.Object,
            Mock.Of<ILogger<AllergyDifferentialAnalyzer>>());

        _controller = new AllergyController(
            _mockRepo.Object,
            _mockFamilyRepo.Object,
            _mockAllergenRepo.Object,
            _analyzer,
            Mock.Of<ILogger<AllergyController>>());

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);
    }

    // ─── CreateIncident ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateIncident_Authenticated_ReturnsCreated()
    {
        // Arrange
        var request = BuildCreateRequest();
        var newId   = Guid.NewGuid();
        _mockRepo.Setup(r => r.CreateIncidentAsync(_testUserId, request, default)).ReturnsAsync(newId);

        // Act
        var result = await _controller.CreateIncident(request, default);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.RouteValues!["id"].Should().Be(newId);
    }

    [Fact]
    public async Task CreateIncident_Unauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        var result = await _controller.CreateIncident(BuildCreateRequest(), default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─── GetIncidents ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIncidents_NullMember_ScopesToPrimaryUser()
    {
        // Arrange — null memberId means "Me" (primary user)
        var expected = new List<AllergyIncidentV2Dto>
        {
            new() { Id = Guid.NewGuid(), HouseholdId = _testUserId }
        };
        _mockRepo.Setup(r => r.GetIncidentsAsync(_testUserId, null, 50, default))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetIncidents(memberId: null, limit: 50);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<AllergyIncidentV2Dto>>()
            .Which.Should().HaveCount(1);

        // Verify the repo was called with null (not "all members") — i.e. "Me" scoping
        _mockRepo.Verify(r => r.GetIncidentsAsync(_testUserId, null, 50, default), Times.Once);
    }

    [Fact]
    public async Task GetIncidents_WithMemberId_ScopesToMember()
    {
        var memberId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetIncidentsAsync(_testUserId, memberId, 50, default))
            .ReturnsAsync(new List<AllergyIncidentV2Dto>());

        await _controller.GetIncidents(memberId: memberId, limit: 50);

        _mockRepo.Verify(r => r.GetIncidentsAsync(_testUserId, memberId, 50, default), Times.Once);
    }

    [Fact]
    public async Task GetIncidents_InvalidLimit_ReturnsBadRequest()
    {
        var result = await _controller.GetIncidents(limit: 0);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetIncidents_Unauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        var result = await _controller.GetIncidents();
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─── GetSuspects ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSuspects_NullMember_ScopesToPrimaryUser()
    {
        var expected = new List<SuspectedAllergenDto>
        {
            new() { Id = Guid.NewGuid(), HouseholdId = _testUserId, IngredientName = "Peanut" }
        };
        _mockRepo.Setup(r => r.GetSuspectedAllergensAsync(_testUserId, null, default))
            .ReturnsAsync(expected);

        var result = await _controller.GetSuspects(memberId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<SuspectedAllergenDto>>()
            .Which.Should().HaveCount(1);

        _mockRepo.Verify(r => r.GetSuspectedAllergensAsync(_testUserId, null, default), Times.Once);
    }

    [Fact]
    public async Task GetSuspects_Unauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        var result = await _controller.GetSuspects();
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─── ConfirmSuspect ────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmSuspect_OwnedSuspect_ReturnsNoContent()
    {
        var suspectId = Guid.NewGuid();
        var suspect   = new SuspectedAllergenDto
        {
            Id             = suspectId,
            HouseholdId    = _testUserId,
            IngredientName = "Peanut"
        };
        _mockRepo.Setup(r => r.GetSuspectedAllergenByIdAsync(suspectId, default))
            .ReturnsAsync(suspect);
        _mockRepo.Setup(r => r.PromoteSuspectedAllergenAsync(suspectId, default))
            .Returns(Task.CompletedTask);
        _mockAllergenRepo.Setup(r => r.CreateIngredientAllergyAsync(_testUserId, It.IsAny<CreateUserIngredientAllergyRequest>()))
            .ReturnsAsync(Guid.NewGuid());

        var result = await _controller.ConfirmSuspect(suspectId, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.PromoteSuspectedAllergenAsync(suspectId, default), Times.Once);
        _mockAllergenRepo.Verify(r => r.CreateIngredientAllergyAsync(
            _testUserId,
            It.Is<CreateUserIngredientAllergyRequest>(req => req.IngredientName == "Peanut")),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmSuspect_SuspectNotFound_ReturnsNotFound()
    {
        var suspectId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetSuspectedAllergenByIdAsync(suspectId, default))
            .ReturnsAsync((SuspectedAllergenDto?)null);

        var result = await _controller.ConfirmSuspect(suspectId, default);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.PromoteSuspectedAllergenAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task ConfirmSuspect_SuspectBelongsToOtherUser_ReturnsForbid()
    {
        var suspectId  = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetSuspectedAllergenByIdAsync(suspectId, default))
            .ReturnsAsync(new SuspectedAllergenDto { Id = suspectId, HouseholdId = otherOwner });

        var result = await _controller.ConfirmSuspect(suspectId, default);

        result.Should().BeOfType<ForbidResult>();
        _mockRepo.Verify(r => r.PromoteSuspectedAllergenAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task ConfirmSuspect_Unauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        var result = await _controller.ConfirmSuspect(Guid.NewGuid(), default);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─── ClearSuspect ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearSuspect_OwnedSuspect_CallsAtomicMethodAndReturnsNoContent()
    {
        var suspectId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetSuspectedAllergenByIdAsync(suspectId, default))
            .ReturnsAsync(new SuspectedAllergenDto { Id = suspectId, HouseholdId = _testUserId });
        _mockRepo.Setup(r => r.ClearSuspectTransactionalAsync(suspectId, _testUserId, default))
            .Returns(Task.CompletedTask);

        var result = await _controller.ClearSuspect(suspectId, default);

        result.Should().BeOfType<NoContentResult>();
        // Verify the atomic transactional method is used (not two separate calls)
        _mockRepo.Verify(r => r.ClearSuspectTransactionalAsync(suspectId, _testUserId, default), Times.Once);
    }

    [Fact]
    public async Task ClearSuspect_SuspectNotFound_ReturnsNotFound()
    {
        var suspectId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetSuspectedAllergenByIdAsync(suspectId, default))
            .ReturnsAsync((SuspectedAllergenDto?)null);

        var result = await _controller.ClearSuspect(suspectId, default);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.ClearSuspectTransactionalAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task ClearSuspect_SuspectBelongsToOtherUser_ReturnsForbid()
    {
        var suspectId  = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetSuspectedAllergenByIdAsync(suspectId, default))
            .ReturnsAsync(new SuspectedAllergenDto { Id = suspectId, HouseholdId = otherOwner });

        var result = await _controller.ClearSuspect(suspectId, default);

        result.Should().BeOfType<ForbidResult>();
        _mockRepo.Verify(r => r.ClearSuspectTransactionalAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task ClearSuspect_Unauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        var result = await _controller.ClearSuspect(Guid.NewGuid(), default);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─── GetReport ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReport_NullMember_PopulatesConfirmedAllergens()
    {
        // Arrange
        var allergies = new List<UserIngredientAllergyDto>
        {
            new() { IngredientName = "Peanut", SeverityLevel = "Severe", RequiresEpiPen = true }
        };
        _mockAllergenRepo
            .Setup(r => r.GetUserIngredientAllergiesAsync(_testUserId, false))
            .ReturnsAsync(allergies);
        _mockRepo.Setup(r => r.GetSuspectedAllergensAsync(_testUserId, null, default))
            .ReturnsAsync(new List<SuspectedAllergenDto>());
        _mockRepo.Setup(r => r.GetIncidentsAsync(_testUserId, null, 200, default))
            .ReturnsAsync(new List<AllergyIncidentV2Dto>());
        _mockRepo.Setup(r => r.GetClearedIngredientsAsync(_testUserId, null, default))
            .ReturnsAsync(new List<ClearedIngredientDto>());

        // Act
        var result = await _controller.GetReport(memberId: null, default);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var report = ok.Value.Should().BeOfType<AllergyReportModel>().Subject;
        report.MemberName.Should().Be("Me");
        report.ConfirmedAllergens.Should().HaveCount(1);
        report.ConfirmedAllergens[0].AllergenName.Should().Be("Peanut");
    }

    [Fact]
    public async Task GetReport_FamilyMember_DoesNotQueryConfirmedAllergens()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var member   = new FamilyMemberDto { Id = memberId, Name = "Jane", PrimaryUserId = _testUserId };
        _mockFamilyRepo.Setup(r => r.GetByIdAsync(memberId)).ReturnsAsync(member);
        _mockRepo.Setup(r => r.GetSuspectedAllergensAsync(_testUserId, memberId, default))
            .ReturnsAsync(new List<SuspectedAllergenDto>());
        _mockRepo.Setup(r => r.GetIncidentsAsync(_testUserId, memberId, 200, default))
            .ReturnsAsync(new List<AllergyIncidentV2Dto>());
        _mockRepo.Setup(r => r.GetClearedIngredientsAsync(_testUserId, memberId, default))
            .ReturnsAsync(new List<ClearedIngredientDto>());

        // Act
        var result = await _controller.GetReport(memberId: memberId, default);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var report = ok.Value.Should().BeOfType<AllergyReportModel>().Subject;
        report.MemberName.Should().Be("Jane");
        report.ConfirmedAllergens.Should().BeEmpty();

        // Confirmed allergens endpoint should NOT be queried for family members
        _mockAllergenRepo.Verify(
            r => r.GetUserIngredientAllergiesAsync(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task GetReport_FamilyMemberBelongsToOtherUser_ReturnsForbid()
    {
        var memberId   = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var member     = new FamilyMemberDto { Id = memberId, Name = "Jane", PrimaryUserId = otherOwner };
        _mockFamilyRepo.Setup(r => r.GetByIdAsync(memberId)).ReturnsAsync(member);

        var result = await _controller.GetReport(memberId: memberId, default);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetReport_Unauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        var result = await _controller.GetReport(null, default);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─── GetClearedIngredients ─────────────────────────────────────────────────

    [Fact]
    public async Task GetClearedIngredients_NullMember_ScopesToPrimaryUser()
    {
        _mockRepo.Setup(r => r.GetClearedIngredientsAsync(_testUserId, null, default))
            .ReturnsAsync(new List<ClearedIngredientDto>
            {
                new() { Id = Guid.NewGuid(), IngredientName = "Milk" }
            });

        var result = await _controller.GetClearedIngredients(memberId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<ClearedIngredientDto>>()
            .Which.Should().HaveCount(1);

        _mockRepo.Verify(r => r.GetClearedIngredientsAsync(_testUserId, null, default), Times.Once);
    }

    [Fact]
    public async Task GetClearedIngredients_Unauthenticated_ReturnsUnauthorized()
    {
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        var result = await _controller.GetClearedIngredients();
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static CreateAllergyIncidentV2Request BuildCreateRequest() => new()
    {
        IncidentDate  = DateTime.UtcNow,
        ExposureType  = "Ingestion",
        Products      = new List<CreateIncidentProductRequest>
        {
            new() { ProductName = "Peanut Butter", HadReaction = true }
        },
        Members       = new List<CreateIncidentMemberRequest>
        {
            new() { MemberName = "Me", Severity = "Rash" }
        }
    };
}
