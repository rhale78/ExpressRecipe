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

public class AllergyManagementControllerTests
{
    private readonly Mock<IEnhancedAllergenRepository> _mockRepository;
    private readonly Mock<ILogger<AllergyManagementController>> _mockLogger;
    private readonly AllergyManagementController _controller;
    private readonly Guid _testUserId;

    public AllergyManagementControllerTests()
    {
        _mockRepository = new Mock<IEnhancedAllergenRepository>();
        _mockLogger = new Mock<ILogger<AllergyManagementController>>();

        _controller = new AllergyManagementController(
            _mockRepository.Object,
            _mockLogger.Object
        );

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);
    }

    #region GetSummary Tests

    [Fact]
    public async Task GetSummary_WhenAuthenticated_ReturnsAllergenSummary()
    {
        // Arrange
        var summary = new UserAllergenSummaryDto
        {
            TotalAllergens = 3,
            SevereAllergens = 1,
            RequiringEpiPen = 1,
            IngredientAllergies = 2,
            TotalIncidents = 5
        };

        _mockRepository
            .Setup(r => r.GetAllergenSummaryAsync(_testUserId))
            .ReturnsAsync(summary);

        // Act
        var result = await _controller.GetSummary();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSummary = okResult.Value.Should().BeAssignableTo<UserAllergenSummaryDto>().Subject;
        returnedSummary.TotalAllergens.Should().Be(3);
        returnedSummary.SevereAllergens.Should().Be(1);
    }

    [Fact]
    public async Task GetSummary_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        // Act
        var result = await _controller.GetSummary();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region GetReactionTypes Tests

    [Fact]
    public async Task GetReactionTypes_ActiveOnly_ReturnsTypes()
    {
        // Arrange
        var reactionTypes = new List<AllergenReactionTypeDto>
        {
            new AllergenReactionTypeDto { Id = Guid.NewGuid(), Name = "Hives", Severity = "Mild", IsCommon = true },
            new AllergenReactionTypeDto { Id = Guid.NewGuid(), Name = "Anaphylaxis", Severity = "Life-Threatening", RequiresMedicalAttention = true }
        };

        _mockRepository
            .Setup(r => r.GetReactionTypesAsync(true))
            .ReturnsAsync(reactionTypes);

        // Act
        var result = await _controller.GetReactionTypes(activeOnly: true);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var types = okResult.Value.Should().BeAssignableTo<List<AllergenReactionTypeDto>>().Subject;
        types.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetReactionTypes_AllTypes_ReturnsAll()
    {
        // Arrange
        var allTypes = new List<AllergenReactionTypeDto>
        {
            new AllergenReactionTypeDto { Id = Guid.NewGuid(), Name = "Hives", Severity = "Mild" },
            new AllergenReactionTypeDto { Id = Guid.NewGuid(), Name = "Anaphylaxis", Severity = "Life-Threatening" },
            new AllergenReactionTypeDto { Id = Guid.NewGuid(), Name = "Archived Reaction", Severity = "Mild" }
        };

        _mockRepository
            .Setup(r => r.GetReactionTypesAsync(false))
            .ReturnsAsync(allTypes);

        // Act
        var result = await _controller.GetReactionTypes(activeOnly: false);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var types = okResult.Value.Should().BeAssignableTo<List<AllergenReactionTypeDto>>().Subject;
        types.Should().HaveCount(3);
    }

    #endregion

    #region GetIngredientAllergies Tests

    [Fact]
    public async Task GetIngredientAllergies_ForCurrentUser_ReturnsAllergies()
    {
        // Arrange
        var allergies = new List<UserIngredientAllergyDto>
        {
            new UserIngredientAllergyDto
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                IngredientName = "Peanuts",
                SeverityLevel = "Severe",
                RequiresEpiPen = true
            },
            new UserIngredientAllergyDto
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                IngredientName = "Tree Nuts",
                SeverityLevel = "Moderate"
            }
        };

        _mockRepository
            .Setup(r => r.GetUserIngredientAllergiesAsync(_testUserId, true))
            .ReturnsAsync(allergies);

        // Act
        var result = await _controller.GetIngredientAllergies(includeReactions: true);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedAllergies = okResult.Value.Should().BeAssignableTo<List<UserIngredientAllergyDto>>().Subject;
        returnedAllergies.Should().HaveCount(2);
        returnedAllergies[0].IngredientName.Should().Be("Peanuts");
    }

    [Fact]
    public async Task GetIngredientAllergies_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        // Act
        var result = await _controller.GetIngredientAllergies();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region GetIngredientAllergy Tests

    [Fact]
    public async Task GetIngredientAllergy_WhenExists_AndOwner_ReturnsOk()
    {
        // Arrange
        var allergyId = Guid.NewGuid();
        var allergy = new UserIngredientAllergyDto
        {
            Id = allergyId,
            UserId = _testUserId,
            IngredientName = "Shellfish",
            SeverityLevel = "Severe"
        };

        _mockRepository
            .Setup(r => r.GetIngredientAllergyByIdAsync(allergyId, true))
            .ReturnsAsync(allergy);

        // Act
        var result = await _controller.GetIngredientAllergy(allergyId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedAllergy = okResult.Value.Should().BeAssignableTo<UserIngredientAllergyDto>().Subject;
        returnedAllergy.IngredientName.Should().Be("Shellfish");
    }

    [Fact]
    public async Task GetIngredientAllergy_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var allergyId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetIngredientAllergyByIdAsync(allergyId, true))
            .ReturnsAsync((UserIngredientAllergyDto?)null);

        // Act
        var result = await _controller.GetIngredientAllergy(allergyId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetIngredientAllergy_WhenOtherUsersAllergy_ReturnsForbid()
    {
        // Arrange
        var allergyId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var allergy = new UserIngredientAllergyDto
        {
            Id = allergyId,
            UserId = otherUserId, // Belongs to a different user
            IngredientName = "Milk",
            SeverityLevel = "Mild"
        };

        _mockRepository
            .Setup(r => r.GetIngredientAllergyByIdAsync(allergyId, true))
            .ReturnsAsync(allergy);

        // Act
        var result = await _controller.GetIngredientAllergy(allergyId);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region CreateIngredientAllergy Tests

    [Fact]
    public async Task CreateIngredientAllergy_WhenAuthenticated_ReturnsCreated()
    {
        // Arrange
        var request = new CreateUserIngredientAllergyRequest
        {
            IngredientName = "Wheat",
            SeverityLevel = "Moderate",
            RequiresEpiPen = false
        };

        var newAllergyId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.CreateIngredientAllergyAsync(_testUserId, request))
            .ReturnsAsync(newAllergyId);

        // Act
        var result = await _controller.CreateIngredientAllergy(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AllergyManagementController.GetIngredientAllergy));
        createdResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateIngredientAllergy_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);
        var request = new CreateUserIngredientAllergyRequest
        {
            IngredientName = "Soy",
            SeverityLevel = "Mild"
        };

        // Act
        var result = await _controller.CreateIngredientAllergy(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region UpdateIngredientAllergy Tests

    [Fact]
    public async Task UpdateIngredientAllergy_WhenSuccess_ReturnsNoContent()
    {
        // Arrange
        var allergyId = Guid.NewGuid();
        var request = new UpdateUserIngredientAllergyRequest
        {
            SeverityLevel = "Severe",
            RequiresEpiPen = true
        };

        _mockRepository
            .Setup(r => r.UpdateIngredientAllergyAsync(allergyId, _testUserId, request))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateIngredientAllergy(allergyId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateIngredientAllergy_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var allergyId = Guid.NewGuid();
        var request = new UpdateUserIngredientAllergyRequest { SeverityLevel = "Mild" };

        _mockRepository
            .Setup(r => r.UpdateIngredientAllergyAsync(allergyId, _testUserId, request))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdateIngredientAllergy(allergyId, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteIngredientAllergy Tests

    [Fact]
    public async Task DeleteIngredientAllergy_WhenFound_ReturnsNoContent()
    {
        // Arrange
        var allergyId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.DeleteIngredientAllergyAsync(allergyId, _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteIngredientAllergy(allergyId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteIngredientAllergy_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var allergyId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.DeleteIngredientAllergyAsync(allergyId, _testUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteIngredientAllergy(allergyId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetIncidents Tests

    [Fact]
    public async Task GetIncidents_ValidLimit_ReturnsIncidents()
    {
        // Arrange
        var incidents = new List<AllergyIncidentDto>
        {
            new AllergyIncidentDto
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                IncidentDate = DateTime.UtcNow.AddDays(-1),
                SeverityLevel = "Moderate",
                EpiPenUsed = false
            },
            new AllergyIncidentDto
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                IncidentDate = DateTime.UtcNow.AddDays(-7),
                SeverityLevel = "Severe",
                EpiPenUsed = true
            }
        };

        _mockRepository
            .Setup(r => r.GetUserIncidentsAsync(_testUserId, 50))
            .ReturnsAsync(incidents);

        // Act
        var result = await _controller.GetIncidents(limit: 50);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedIncidents = okResult.Value.Should().BeAssignableTo<List<AllergyIncidentDto>>().Subject;
        returnedIncidents.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task GetIncidents_InvalidLimit_ReturnsBadRequest(int invalidLimit)
    {
        // Act
        var result = await _controller.GetIncidents(limit: invalidLimit);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetIncident Tests

    [Fact]
    public async Task GetIncident_WhenExists_AndOwner_ReturnsOk()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        var incident = new AllergyIncidentDto
        {
            Id = incidentId,
            UserId = _testUserId,
            IncidentDate = DateTime.UtcNow.AddHours(-3),
            SeverityLevel = "Mild",
            Symptoms = "Rash"
        };

        _mockRepository
            .Setup(r => r.GetIncidentByIdAsync(incidentId))
            .ReturnsAsync(incident);

        // Act
        var result = await _controller.GetIncident(incidentId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedIncident = okResult.Value.Should().BeAssignableTo<AllergyIncidentDto>().Subject;
        returnedIncident.Symptoms.Should().Be("Rash");
    }

    [Fact]
    public async Task GetIncident_WhenOtherUsersIncident_ReturnsForbid()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var incident = new AllergyIncidentDto
        {
            Id = incidentId,
            UserId = otherUserId, // Belongs to a different user
            IncidentDate = DateTime.UtcNow,
            SeverityLevel = "Mild"
        };

        _mockRepository
            .Setup(r => r.GetIncidentByIdAsync(incidentId))
            .ReturnsAsync(incident);

        // Act
        var result = await _controller.GetIncident(incidentId);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region CreateIncident Tests

    [Fact]
    public async Task CreateIncident_WhenAuthenticated_ReturnsCreated()
    {
        // Arrange
        var request = new CreateAllergyIncidentRequest
        {
            IncidentDate = DateTime.UtcNow,
            SeverityLevel = "Moderate",
            Symptoms = "Hives, itching",
            EpiPenUsed = false,
            HospitalVisit = false
        };

        var newIncidentId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.CreateIncidentAsync(_testUserId, request))
            .ReturnsAsync(newIncidentId);

        // Act
        var result = await _controller.CreateIncident(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AllergyManagementController.GetIncident));
        createdResult.Value.Should().NotBeNull();
    }

    #endregion

    #region End-to-End Flow Tests

    [Fact]
    public async Task EndToEnd_AddAllergyAndRecordIncident_Flow()
    {
        // Step 1: Create ingredient allergy
        var createAllergyRequest = new CreateUserIngredientAllergyRequest
        {
            IngredientName = "Sesame",
            SeverityLevel = "Severe",
            RequiresEpiPen = true
        };

        var allergyId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.CreateIngredientAllergyAsync(_testUserId, createAllergyRequest))
            .ReturnsAsync(allergyId);

        var createAllergyResult = await _controller.CreateIngredientAllergy(createAllergyRequest);
        createAllergyResult.Result.Should().BeOfType<CreatedAtActionResult>();

        // Step 2: Record an incident for the allergy
        var createIncidentRequest = new CreateAllergyIncidentRequest
        {
            UserIngredientAllergyId = allergyId,
            IncidentDate = DateTime.UtcNow,
            SeverityLevel = "Severe",
            Symptoms = "Throat tightening, hives",
            EpiPenUsed = true,
            HospitalVisit = true
        };

        var incidentId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.CreateIncidentAsync(_testUserId, createIncidentRequest))
            .ReturnsAsync(incidentId);

        var createIncidentResult = await _controller.CreateIncident(createIncidentRequest);
        createIncidentResult.Result.Should().BeOfType<CreatedAtActionResult>();

        // Step 3: Verify summary reflects the new data
        var summary = new UserAllergenSummaryDto
        {
            TotalAllergens = 1,
            SevereAllergens = 1,
            RequiringEpiPen = 1,
            IngredientAllergies = 1,
            TotalIncidents = 1,
            LastIncidentDate = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetAllergenSummaryAsync(_testUserId))
            .ReturnsAsync(summary);

        var summaryResult = await _controller.GetSummary();
        var okResult = summaryResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSummary = okResult.Value.Should().BeAssignableTo<UserAllergenSummaryDto>().Subject;
        returnedSummary.TotalIncidents.Should().Be(1);
        returnedSummary.RequiringEpiPen.Should().Be(1);
    }

    #endregion
}
