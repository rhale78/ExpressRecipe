using ExpressRecipe.GroceryStoreLocationService.Data;
using ExpressRecipe.GroceryStoreLocationService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace ExpressRecipe.GroceryStoreLocationService.Tests.Services;

public class StoreChainNormalizerTests
{
    private readonly Mock<IGroceryStoreRepository> _repoMock;
    private readonly Mock<ILogger<StoreChainNormalizer>> _loggerMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    public StoreChainNormalizerTests()
    {
        _repoMock = new Mock<IGroceryStoreRepository>();
        _loggerMock = new Mock<ILogger<StoreChainNormalizer>>();
        _scopeMock = new Mock<IServiceScope>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IGroceryStoreRepository)))
            .Returns(_repoMock.Object);

        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
    }

    private static List<StoreChainDto> CreateTestChains()
    {
        return new List<StoreChainDto>
        {
            new StoreChainDto { Id = Guid.NewGuid(), CanonicalName = "Walmart", Aliases = "[\"WAL-MART\",\"Walmart Supercenter\",\"WALMART\"]", IsNational = true },
            new StoreChainDto { Id = Guid.NewGuid(), CanonicalName = "Kroger", Aliases = "[\"KROGER\",\"Kroger Food & Drug\"]", IsNational = true },
            new StoreChainDto { Id = Guid.NewGuid(), CanonicalName = "Trader Joe's", Aliases = "[\"TRADER JOES\",\"Trader Joe's\"]", IsNational = true }
        };
    }

    [Fact]
    public async Task Normalize_CanonicalName_ReturnsSelf()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(CreateTestChains());
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);
        await normalizer.EnsureLoadedAsync();

        // Act
        var result = normalizer.Normalize("Walmart");

        // Assert
        result.Should().Be("Walmart");
    }

    [Fact]
    public async Task Normalize_KnownAlias_ReturnsCanonical()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(CreateTestChains());
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);
        await normalizer.EnsureLoadedAsync();

        // Act
        var result = normalizer.Normalize("WAL-MART");

        // Assert
        result.Should().Be("Walmart");
    }

    [Fact]
    public async Task Normalize_CaseInsensitive_ReturnsCanonical()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(CreateTestChains());
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);
        await normalizer.EnsureLoadedAsync();

        // Act
        var result = normalizer.Normalize("walmart supercenter");

        // Assert
        result.Should().Be("Walmart");
    }

    [Fact]
    public async Task Normalize_UnknownChain_ReturnsNull()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(CreateTestChains());
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);
        await normalizer.EnsureLoadedAsync();

        // Act
        var result = normalizer.Normalize("Corner Bodega");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Normalize_BeforeLoad_ThrowsInvalidOperationException()
    {
        // Arrange: do NOT call EnsureLoadedAsync
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);

        // Act & Assert - map not yet loaded, should throw to surface the contract violation
        Action act = () => normalizer.Normalize("Walmart");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*EnsureLoadedAsync*");
    }

    [Fact]
    public async Task Normalize_NullInput_ReturnsNull()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(CreateTestChains());
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);
        await normalizer.EnsureLoadedAsync();

        // Act & Assert
        normalizer.Normalize(string.Empty).Should().BeNull();
        normalizer.Normalize("   ").Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ReloadsMap()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(CreateTestChains());
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);
        await normalizer.EnsureLoadedAsync();

        var initialResult = normalizer.Normalize("WAL-MART");
        initialResult.Should().Be("Walmart");

        // Act: refresh should re-query (cache is null since no HybridCacheService provided)
        await normalizer.RefreshAsync();

        // Assert: still works after refresh
        normalizer.Normalize("WAL-MART").Should().Be("Walmart");
        _repoMock.Verify(r => r.GetAllChainsAsync(), Times.AtLeast(2));
    }

    [Fact]
    public async Task Normalize_MultiWordAlias_ReturnsCanonical()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(CreateTestChains());
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);
        await normalizer.EnsureLoadedAsync();

        // Act
        var result = normalizer.Normalize("Kroger Food & Drug");

        // Assert
        result.Should().Be("Kroger");
    }

    [Fact]
    public async Task EnsureLoadedAsync_CalledTwice_OnlyLoadsOnce()
    {
        // Arrange
        _repoMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(CreateTestChains());
        var normalizer = new StoreChainNormalizer(_scopeFactoryMock.Object, _loggerMock.Object);

        // Act: call twice
        await normalizer.EnsureLoadedAsync();
        await normalizer.EnsureLoadedAsync();

        // Assert: repo called only once
        _repoMock.Verify(r => r.GetAllChainsAsync(), Times.Once);
    }
}
