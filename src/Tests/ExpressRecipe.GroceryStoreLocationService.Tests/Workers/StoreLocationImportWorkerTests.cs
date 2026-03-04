using ExpressRecipe.GroceryStoreLocationService.Data;
using ExpressRecipe.GroceryStoreLocationService.Services;
using ExpressRecipe.GroceryStoreLocationService.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using Xunit;

namespace ExpressRecipe.GroceryStoreLocationService.Tests.Workers;

public class StoreLocationImportWorkerTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<StoreLocationImportWorker>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IOpenPricesLocationImportService> _importServiceMock;
    private readonly Mock<IGroceryStoreRepository> _repoMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;

    public StoreLocationImportWorkerTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<StoreLocationImportWorker>>();
        _configMock = new Mock<IConfiguration>();
        
        _importServiceMock = new Mock<IOpenPricesLocationImportService>();
        _repoMock = new Mock<IGroceryStoreRepository>();
        
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        
        _serviceProviderMock.Setup(x => x.GetService(typeof(IOpenPricesLocationImportService))).Returns(_importServiceMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IGroceryStoreRepository))).Returns(_repoMock.Object);
        
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(_scopeFactoryMock.Object);
    }

    [Fact]
    public async Task RunImportAsync_FileExists_UsesFile_DoesNotCallWeb()
    {
        // Arrange
        var filePath = "test_locations.jsonl";
        
        _configMock.Setup(x => x["StoreLocationImport:OpenPricesFilePath"]).Returns(filePath);
        _configMock.Setup(x => x["StoreLocationImport:OpenPricesUrl"]).Returns("http://web.com");
        
        _importServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _importServiceMock.Setup(x => x.FetchStoresFromFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UpsertGroceryStoreRequest> { new UpsertGroceryStoreRequest { Name = "Test" } }, null));

        var worker = new StoreLocationImportWorker(_serviceProviderMock.Object, _loggerMock.Object, _configMock.Object);

        // Act
        await worker.RunImportAsync("openprices", CancellationToken.None);

        // Assert
        _importServiceMock.Verify(x => x.FetchStoresFromFileAsync(filePath, It.IsAny<CancellationToken>()), Times.Once);
        _importServiceMock.Verify(x => x.FetchStoresFromUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunImportAsync_WebFails_FallsBackToFile()
    {
        // Arrange
        var filePath = "fallback_locations.jsonl";

        _configMock.Setup(x => x["StoreLocationImport:OpenPricesFilePath"]).Returns(filePath);
        _configMock.Setup(x => x["StoreLocationImport:OpenPricesUrl"]).Returns("http://web.com");
        
        // Setup sequence for FileExists
        _importServiceMock.SetupSequence(x => x.FileExists(filePath))
            .Returns(false) // First check
            .Returns(true);  // Fallback check

        // Mock web call to throw
        _importServiceMock.Setup(x => x.FetchStoresFromUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Web Down"));

        _importServiceMock.Setup(x => x.FetchStoresFromFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UpsertGroceryStoreRequest> { new UpsertGroceryStoreRequest { Name = "Fallback" } }, null));

        var worker = new StoreLocationImportWorker(_serviceProviderMock.Object, _loggerMock.Object, _configMock.Object);

        // Act
        await worker.RunImportAsync("openprices", CancellationToken.None);

        // Assert
        _importServiceMock.Verify(x => x.FetchStoresFromUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _importServiceMock.Verify(x => x.FetchStoresFromFileAsync(filePath, It.IsAny<CancellationToken>()), Times.Once);
    }
}
