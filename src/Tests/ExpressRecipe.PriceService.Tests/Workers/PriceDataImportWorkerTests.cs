using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using ExpressRecipe.PriceService.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Workers;

public class PriceDataImportWorkerTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<PriceDataImportWorker>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IOpenPricesImportService> _importServiceMock;
    private readonly Mock<IPriceRepository> _priceRepoMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;

    public PriceDataImportWorkerTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<PriceDataImportWorker>>();
        _configMock = new Mock<IConfiguration>();
        
        _importServiceMock = new Mock<IOpenPricesImportService>();
        _priceRepoMock = new Mock<IPriceRepository>();
        
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        
        _serviceProviderMock.Setup(x => x.GetService(typeof(IOpenPricesImportService))).Returns(_importServiceMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IPriceRepository))).Returns(_priceRepoMock.Object);
        
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(_scopeFactoryMock.Object);
    }

    [Fact]
    public async Task RunImportAsync_FileExists_UsesFile_DoesNotCallWeb()
    {
        // Arrange
        var filePath = "test_prices.parquet";
        
        _configMock.Setup(x => x["PriceImport:OpenPricesFilePath"]).Returns(filePath);
        _configMock.Setup(x => x["PriceImport:OpenPricesUrl"]).Returns("http://web.com");
        
        // Mock FileExists to return true
        _importServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _importServiceMock.Setup(x => x.ImportFromFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResult { Success = true, Imported = 10, Processed = 10 });

        var worker = new PriceDataImportWorker(_serviceProviderMock.Object, _loggerMock.Object, _configMock.Object);

        // Act
        await worker.RunImportAsync(CancellationToken.None);

        // Assert
        _importServiceMock.Verify(x => x.ImportFromFileAsync(filePath, It.IsAny<CancellationToken>()), Times.Once);
        _importServiceMock.Verify(x => x.ImportFromUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunImportAsync_WebFails_FallsBackToFile()
    {
        // Arrange
        var filePath = "fallback_prices.parquet";

        _configMock.Setup(x => x["PriceImport:OpenPricesFilePath"]).Returns(filePath);
        _configMock.Setup(x => x["PriceImport:OpenPricesUrl"]).Returns("http://web.com");
        
        // Setup sequence for FileExists
        _importServiceMock.SetupSequence(x => x.FileExists(filePath))
            .Returns(false) // First check
            .Returns(true);  // Fallback check

        // Mock web call to throw
        _importServiceMock.Setup(x => x.ImportFromUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Web Down"));

        _importServiceMock.Setup(x => x.ImportFromFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResult { Success = true, Imported = 5 });

        var worker = new PriceDataImportWorker(_serviceProviderMock.Object, _loggerMock.Object, _configMock.Object);

        // Act
        await worker.RunImportAsync(CancellationToken.None);

        // Assert
        _importServiceMock.Verify(x => x.ImportFromUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _importServiceMock.Verify(x => x.ImportFromFileAsync(filePath, It.IsAny<CancellationToken>()), Times.Once);
    }
}
