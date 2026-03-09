using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;
using ExpressRecipe.InventoryService.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class EquipmentControllerTests
{
    private readonly Mock<IEquipmentRepository> _mockEquipment;
    private readonly Mock<IEquipmentCapabilityResolver> _mockResolver;
    private readonly EquipmentController _controller;
    private readonly Guid _householdId;

    public EquipmentControllerTests()
    {
        _mockEquipment = new Mock<IEquipmentRepository>();
        _mockResolver = new Mock<IEquipmentCapabilityResolver>();
        _controller = new EquipmentController(_mockEquipment.Object, _mockResolver.Object);
        _householdId = Guid.NewGuid();
        _controller.ControllerContext = CreateContextWithHousehold(_householdId);
    }

    private static ControllerContext CreateContextWithHousehold(Guid householdId)
    {
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("household_id", householdId.ToString())
        };
        ClaimsIdentity identity = new(claims, "TestAuthentication");
        ClaimsPrincipal principal = new(identity);
        DefaultHttpContext httpContext = new() { User = principal };
        return new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task GetTemplates_ReturnsOkWithTemplates()
    {
        // Arrange
        List<EquipmentTemplateDto> templates = new()
        {
            new EquipmentTemplateDto { Id = Guid.NewGuid(), Name = "Instant Pot", Category = "Appliance", IsBuiltIn = true }
        };
        _mockEquipment.Setup(r => r.GetTemplatesAsync(default)).ReturnsAsync(templates);

        // Act
        IActionResult result = await _controller.GetTemplates(default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(templates);
    }

    [Fact]
    public async Task GetInstances_ReturnsOkWithInstances()
    {
        // Arrange
        List<EquipmentInstanceDto> instances = new()
        {
            new EquipmentInstanceDto
            {
                Id = Guid.NewGuid(), HouseholdId = _householdId,
                TemplateName = "Instant Pot", IsActive = true
            }
        };
        _mockEquipment.Setup(r => r.GetInstancesAsync(_householdId, null, true, default)).ReturnsAsync(instances);

        // Act
        IActionResult result = await _controller.GetInstances(null, true, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(instances);
    }

    [Fact]
    public async Task AddInstance_WithTemplateIdAndNoCapabilities_CopiesTemplateDefaults()
    {
        // Arrange
        Guid templateId = Guid.NewGuid();
        Guid instanceId = Guid.NewGuid();
        List<EquipmentTemplateDto> templates = new()
        {
            new EquipmentTemplateDto
            {
                Id = templateId, Name = "Instant Pot", Category = "Appliance", IsBuiltIn = true,
                DefaultCapabilities = new List<string> { "PressureCook", "SlowCook" }
            }
        };
        _mockEquipment.Setup(r => r.AddInstanceAsync(_householdId, null, templateId, null, null, null, null, null, null, default))
                      .ReturnsAsync(instanceId);
        _mockEquipment.Setup(r => r.GetTemplatesAsync(default)).ReturnsAsync(templates);
        _mockEquipment.Setup(r => r.SetCapabilitiesAsync(instanceId, It.IsAny<IEnumerable<string>>(), default))
                      .Returns(Task.CompletedTask);

        AddEquipmentRequest req = new() { TemplateId = templateId };

        // Act
        IActionResult result = await _controller.AddInstance(req, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockEquipment.Verify(r => r.SetCapabilitiesAsync(instanceId,
            It.Is<IEnumerable<string>>(caps => caps.SequenceEqual(new[] { "PressureCook", "SlowCook" })), default), Times.Once);
    }

    [Fact]
    public async Task AddInstance_WithTemplateIdAndExplicitCapabilities_UsesProvidedCapabilities()
    {
        // Arrange
        Guid templateId = Guid.NewGuid();
        Guid instanceId = Guid.NewGuid();
        List<string> explicitCaps = new() { "SlowCook" };
        _mockEquipment.Setup(r => r.AddInstanceAsync(_householdId, null, templateId, null, null, null, null, null, null, default))
                      .ReturnsAsync(instanceId);
        _mockEquipment.Setup(r => r.SetCapabilitiesAsync(instanceId, explicitCaps, default))
                      .Returns(Task.CompletedTask);

        AddEquipmentRequest req = new() { TemplateId = templateId, Capabilities = explicitCaps };

        // Act
        IActionResult result = await _controller.AddInstance(req, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockEquipment.Verify(r => r.GetTemplatesAsync(default), Times.Never);
        _mockEquipment.Verify(r => r.SetCapabilitiesAsync(instanceId, explicitCaps, default), Times.Once);
    }

    [Fact]
    public async Task SetCapabilities_ReturnsNoContent()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        SetCapabilitiesRequest req = new() { Capabilities = new List<string> { "SlowCook" } };
        _mockEquipment.Setup(r => r.SetCapabilitiesAsync(id, req.Capabilities, default)).Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.SetCapabilities(id, req, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockEquipment.Verify(r => r.SetCapabilitiesAsync(id, req.Capabilities, default), Times.Once);
    }

    [Fact]
    public async Task Deactivate_ReturnsNoContent()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        _mockEquipment.Setup(r => r.UpdateInstanceAsync(id, null, null, null, null, null, null, false, default))
                      .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.Deactivate(id, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Resolve_ReturnsOkWithInstances()
    {
        // Arrange
        List<EquipmentInstanceDto> instances = new()
        {
            new EquipmentInstanceDto { Id = Guid.NewGuid(), HouseholdId = _householdId, TemplateName = "Instant Pot", IsActive = true }
        };
        _mockResolver.Setup(r => r.ResolveAsync(_householdId, "SlowCook", default)).ReturnsAsync(instances);

        // Act
        IActionResult result = await _controller.Resolve("SlowCook", default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(instances);
    }

    [Fact]
    public async Task Substitute_FoundSubstitute_ReturnsTrueWithMessage()
    {
        // Arrange
        string expectedMessage = "Your 'Instant Pot' can substitute — it supports SlowCook.";
        _mockResolver.Setup(r => r.GetSubstituteMessageAsync(_householdId, "Crock Pot", default))
                     .ReturnsAsync(expectedMessage);

        // Act
        IActionResult result = await _controller.Substitute("Crock Pot", default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        object? value = ((OkObjectResult)result).Value;
        value.Should().NotBeNull();
        System.Type type = value!.GetType();
        ((string?)type.GetProperty("message")?.GetValue(value)).Should().Be(expectedMessage);
        ((bool?)type.GetProperty("found")?.GetValue(value)).Should().BeTrue();
    }

    [Fact]
    public async Task Substitute_NoSubstitute_ReturnsFalseWithNullMessage()
    {
        // Arrange
        _mockResolver.Setup(r => r.GetSubstituteMessageAsync(_householdId, "Crock Pot", default))
                     .ReturnsAsync((string?)null);

        // Act
        IActionResult result = await _controller.Substitute("Crock Pot", default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        object? value = ((OkObjectResult)result).Value;
        value.Should().NotBeNull();
        System.Type type = value!.GetType();
        ((string?)type.GetProperty("message")?.GetValue(value)).Should().BeNull();
        ((bool?)type.GetProperty("found")?.GetValue(value)).Should().BeFalse();
    }
}
