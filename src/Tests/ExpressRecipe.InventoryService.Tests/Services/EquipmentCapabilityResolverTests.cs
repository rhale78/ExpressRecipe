using Xunit;
using Moq;
using FluentAssertions;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;

namespace ExpressRecipe.InventoryService.Tests.Services;

public class EquipmentCapabilityResolverTests
{
    private readonly Mock<IEquipmentRepository> _mockRepo;
    private readonly EquipmentCapabilityResolver _resolver;
    private readonly Guid _householdId;

    public EquipmentCapabilityResolverTests()
    {
        _mockRepo = new Mock<IEquipmentRepository>();
        _resolver = new EquipmentCapabilityResolver(_mockRepo.Object);
        _householdId = Guid.NewGuid();
    }

    #region ResolveAsync Tests

    [Fact]
    public async Task ResolveAsync_DelegatesToRepository()
    {
        // Arrange
        const string capability = "SlowCook";
        List<EquipmentInstanceDto> expected = new()
        {
            new EquipmentInstanceDto
            {
                Id = Guid.NewGuid(), HouseholdId = _householdId,
                TemplateId = Guid.NewGuid(), TemplateName = "Instant Pot",
                IsActive = true, Capabilities = new List<string> { "SlowCook" }
            }
        };
        _mockRepo.Setup(r => r.GetInstancesByCapabilityAsync(_householdId, capability, default))
                 .ReturnsAsync(expected);

        // Act
        List<EquipmentInstanceDto> result = await _resolver.ResolveAsync(_householdId, capability);

        // Assert
        result.Should().BeEquivalentTo(expected);
        _mockRepo.Verify(r => r.GetInstancesByCapabilityAsync(_householdId, capability, default), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_UnknownCapability_ReturnsEmptyList()
    {
        // Arrange
        const string capability = "UnknownCap";
        _mockRepo.Setup(r => r.GetInstancesByCapabilityAsync(_householdId, capability, default))
                 .ReturnsAsync(new List<EquipmentInstanceDto>());

        // Act
        List<EquipmentInstanceDto> result = await _resolver.ResolveAsync(_householdId, capability);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetSubstituteMessageAsync Tests

    [Fact]
    public async Task GetSubstituteMessageAsync_CrockPotNeeded_InstantPotPresent_ReturnsMessage()
    {
        // Arrange
        EquipmentInstanceDto instantPot = new()
        {
            Id = Guid.NewGuid(), HouseholdId = _householdId,
            TemplateId = Guid.NewGuid(), TemplateName = "Instant Pot",
            IsActive = true, Capabilities = new List<string> { "SlowCook" }
        };
        _mockRepo.Setup(r => r.GetInstancesByCapabilityAsync(_householdId, "SlowCook", default))
                 .ReturnsAsync(new List<EquipmentInstanceDto> { instantPot });

        // Act
        string? message = await _resolver.GetSubstituteMessageAsync(_householdId, "Crock Pot");

        // Assert
        message.Should().NotBeNull();
        message.Should().Contain("Instant Pot");
        message.Should().Contain("SlowCook");
    }

    [Fact]
    public async Task GetSubstituteMessageAsync_UnknownEquipmentName_ReturnsNull()
    {
        // Arrange / Act
        string? message = await _resolver.GetSubstituteMessageAsync(_householdId, "Unknown Gadget");

        // Assert
        message.Should().BeNull();
        _mockRepo.Verify(r => r.GetInstancesByCapabilityAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSubstituteMessageAsync_NoCapableEquipment_ReturnsNull()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetInstancesByCapabilityAsync(_householdId, "SlowCook", default))
                 .ReturnsAsync(new List<EquipmentInstanceDto>());

        // Act
        string? message = await _resolver.GetSubstituteMessageAsync(_householdId, "Crock Pot");

        // Assert
        message.Should().BeNull();
    }

    [Fact]
    public async Task GetSubstituteMessageAsync_CustomNameEquipment_UsesCustomNameInMessage()
    {
        // Arrange
        EquipmentInstanceDto instance = new()
        {
            Id = Guid.NewGuid(), HouseholdId = _householdId,
            TemplateId = Guid.NewGuid(), TemplateName = "Instant Pot",
            CustomName = "My Big Instant Pot",
            IsActive = true, Capabilities = new List<string> { "SlowCook" }
        };
        _mockRepo.Setup(r => r.GetInstancesByCapabilityAsync(_householdId, "SlowCook", default))
                 .ReturnsAsync(new List<EquipmentInstanceDto> { instance });

        // Act
        string? message = await _resolver.GetSubstituteMessageAsync(_householdId, "Slow Cooker");

        // Assert
        message.Should().NotBeNull();
        message.Should().Contain("My Big Instant Pot");
    }

    [Fact]
    public async Task GetSubstituteMessageAsync_InstantPotForPressureCooker_ReturnsMessage()
    {
        // Arrange
        EquipmentInstanceDto instance = new()
        {
            Id = Guid.NewGuid(), HouseholdId = _householdId,
            TemplateId = Guid.NewGuid(), TemplateName = "Instant Pot",
            IsActive = true, Capabilities = new List<string> { "PressureCook" }
        };
        _mockRepo.Setup(r => r.GetInstancesByCapabilityAsync(_householdId, "PressureCook", default))
                 .ReturnsAsync(new List<EquipmentInstanceDto> { instance });

        // Act
        string? message = await _resolver.GetSubstituteMessageAsync(_householdId, "Pressure Cooker");

        // Assert
        message.Should().NotBeNull();
        message.Should().Contain("PressureCook");
    }

    [Fact]
    public async Task GetSubstituteMessageAsync_MultipleCapableEquipment_UsesFirstMatch()
    {
        // Arrange
        EquipmentInstanceDto first = new()
        {
            Id = Guid.NewGuid(), HouseholdId = _householdId,
            TemplateName = "Instant Pot", CustomName = "Primary Pot",
            IsActive = true, Capabilities = new List<string> { "SlowCook" }
        };
        EquipmentInstanceDto second = new()
        {
            Id = Guid.NewGuid(), HouseholdId = _householdId,
            TemplateName = "Dutch Oven",
            IsActive = true, Capabilities = new List<string> { "SlowCook" }
        };
        _mockRepo.Setup(r => r.GetInstancesByCapabilityAsync(_householdId, "SlowCook", default))
                 .ReturnsAsync(new List<EquipmentInstanceDto> { first, second });

        // Act
        string? message = await _resolver.GetSubstituteMessageAsync(_householdId, "Slow Cooker");

        // Assert
        message.Should().NotBeNull();
        message.Should().Contain("Primary Pot");
        message.Should().NotContain("Dutch Oven");
    }

    #endregion

    #region EquipmentInstanceDto DisplayName Tests

    [Fact]
    public void EquipmentInstanceDto_DisplayName_PrefersCustomName()
    {
        EquipmentInstanceDto dto = new()
        {
            Id = Guid.NewGuid(), HouseholdId = Guid.NewGuid(),
            TemplateName = "Instant Pot", CustomName = "My Pot",
            IsActive = true
        };

        dto.DisplayName.Should().Be("My Pot");
    }

    [Fact]
    public void EquipmentInstanceDto_DisplayName_FallsBackToTemplateName()
    {
        EquipmentInstanceDto dto = new()
        {
            Id = Guid.NewGuid(), HouseholdId = Guid.NewGuid(),
            TemplateName = "Instant Pot", CustomName = null,
            IsActive = true
        };

        dto.DisplayName.Should().Be("Instant Pot");
    }

    [Fact]
    public void EquipmentInstanceDto_DisplayName_FallsBackToUnknownEquipment()
    {
        EquipmentInstanceDto dto = new()
        {
            Id = Guid.NewGuid(), HouseholdId = Guid.NewGuid(),
            TemplateName = null, CustomName = null,
            IsActive = true
        };

        dto.DisplayName.Should().Be("Unknown Equipment");
    }

    #endregion
}
