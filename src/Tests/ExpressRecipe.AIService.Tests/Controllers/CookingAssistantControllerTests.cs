using ExpressRecipe.AIService.Controllers;
using ExpressRecipe.AIService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ExpressRecipe.AIService.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="CookingAssistantController"/>.
/// All service dependencies are mocked; HTTP context is set up with claims.
/// </summary>
public class CookingAssistantControllerTests
{
    private readonly Mock<ICookingAssistantService> _serviceMock = new();
    private readonly Guid _householdId = Guid.NewGuid();

    private CookingAssistantController CreateController(bool withHouseholdClaim = true)
    {
        var controller = new CookingAssistantController(_serviceMock.Object);

        if (withHouseholdClaim)
        {
            var claims = new List<Claim>
            {
                new Claim("household_id", _householdId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthentication");
            var principal = new ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }
        else
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        return controller;
    }

    private CookingAssistantRequest BuildRequest(string message = "My gravy is lumpy") =>
        new() { UserMessage = message, RecipeName = "Gravy" };

    private CookingAssistantResponse SuccessResponse(string suggestion = "Use a slurry") =>
        new() { Success = true, Suggestion = suggestion, Explanation = "Starch clumps", RelatedTips = new() };

    // ──────────────────────────────────────────────────────────────────────────
    // Missing household_id claim → 401
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SomethingOff_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        var controller = CreateController(withHouseholdClaim: false);
        var result = await controller.SomethingOff(BuildRequest(), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPairings_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        var controller = CreateController(withHouseholdClaim: false);
        var result = await controller.GetPairings(BuildRequest("Beef stew"), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task TroubleshootProblem_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        var controller = CreateController(withHouseholdClaim: false);
        var result = await controller.TroubleshootProblem(BuildRequest(), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetVariations_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        var controller = CreateController(withHouseholdClaim: false);
        var result = await controller.GetVariations(BuildRequest("Pasta carbonara"), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task FixIssue_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        var controller = CreateController(withHouseholdClaim: false);
        var result = await controller.FixIssue(BuildRequest(), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AdaptRecipe_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        var controller = CreateController(withHouseholdClaim: false);
        var result = await controller.AdaptRecipe(BuildRequest(), ct: CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Happy path — service called with correct HouseholdId, returns 200
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SomethingOff_ValidClaim_CallsServiceWithHouseholdId()
    {
        var response = SuccessResponse();
        _serviceMock
            .Setup(s => s.AskSomethingSeemsBrokenAsync(
                It.Is<CookingAssistantRequest>(r => r.HouseholdId == _householdId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController().SomethingOff(BuildRequest(), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(response);
        _serviceMock.Verify(s => s.AskSomethingSeemsBrokenAsync(
            It.Is<CookingAssistantRequest>(r => r.HouseholdId == _householdId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPairings_ValidClaim_CallsServiceWithHouseholdId()
    {
        var response = SuccessResponse("Pair with red wine");
        _serviceMock
            .Setup(s => s.GetPairingsAsync(
                It.Is<CookingAssistantRequest>(r => r.HouseholdId == _householdId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController().GetPairings(BuildRequest("Beef stew"), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.GetPairingsAsync(
            It.Is<CookingAssistantRequest>(r => r.HouseholdId == _householdId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TroubleshootProblem_ValidClaim_ReturnsOkWithServiceResult()
    {
        var response = SuccessResponse("Lumpy gravy fix");
        _serviceMock
            .Setup(s => s.TroubleshootProblemAsync(It.IsAny<CookingAssistantRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController().TroubleshootProblem(BuildRequest(), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GetVariations_ValidClaim_ReturnsOkWithServiceResult()
    {
        var response = SuccessResponse("3 variations");
        _serviceMock
            .Setup(s => s.GetVariationsAsync(It.IsAny<CookingAssistantRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController().GetVariations(BuildRequest("Carbonara"), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task FixIssue_ValidClaim_ReturnsOkWithServiceResult()
    {
        var response = SuccessResponse("Fix for sticking pan");
        _serviceMock
            .Setup(s => s.FixIssueAsync(It.IsAny<CookingAssistantRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController().FixIssue(BuildRequest(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AdaptRecipe_ValidClaim_DefaultsMethodToCrockpot()
    {
        var response = SuccessResponse("Adapted for crockpot");
        _serviceMock
            .Setup(s => s.AdaptRecipeAsync(
                It.IsAny<CookingAssistantRequest>(), "crockpot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController().AdaptRecipe(BuildRequest(), ct: CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.AdaptRecipeAsync(
            It.IsAny<CookingAssistantRequest>(), "crockpot", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdaptRecipe_ValidClaim_PassesCustomMethod()
    {
        var response = SuccessResponse("Adapted for instant pot");
        _serviceMock
            .Setup(s => s.AdaptRecipeAsync(
                It.IsAny<CookingAssistantRequest>(), "instant pot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController().AdaptRecipe(BuildRequest(), "instant pot", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.AdaptRecipeAsync(
            It.IsAny<CookingAssistantRequest>(), "instant pot", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HouseholdId is correctly stamped on the request passed to the service
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdaptRecipe_ValidClaim_StampsHouseholdIdOnRequest()
    {
        var response = SuccessResponse();
        CookingAssistantRequest? captured = null;
        _serviceMock
            .Setup(s => s.AdaptRecipeAsync(It.IsAny<CookingAssistantRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<CookingAssistantRequest, string, CancellationToken>((r, _, _) => captured = r)
            .ReturnsAsync(response);

        await CreateController().AdaptRecipe(BuildRequest(), ct: CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.HouseholdId.Should().Be(_householdId);
    }
}
