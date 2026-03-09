using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Services;

/// <summary>
/// Tests for <see cref="UsdaFmapImportService"/>.
/// Uses a real temporary CSV file to test the parse+import pipeline.
/// </summary>
public class UsdaFmapImportServiceTests
{
    private static UsdaFmapImportService BuildSut(IPriceRepository repo)
    {
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<UsdaFmapImportService>>();
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        return new UsdaFmapImportService(repo, new PriceUnitNormalizer(), loggerMock.Object, configMock.Object);
    }

    [Fact]
    public async Task ImportFromFileAsync_ValidCsv_InsertsRecordsViaRepository()
    {
        // Arrange
        var csv = "food_group,area,date,mean_unit_value,unit\n" +
                  "\"Milk, fresh\",\"Chicago, IL\",2023-01,3.49,gal\n" +
                  "\"White bread\",\"Northeast\",2023-02,2.99,lb\n";

        var tmpPath = Path.GetTempFileName() + ".csv";
        await File.WriteAllTextAsync(tmpPath, csv);

        var insertedBatches = new List<IEnumerable<PriceHistoryRecord>>();
        var repoMock = new Mock<IPriceRepository>();
        repoMock.Setup(r => r.BulkInsertPriceHistoryAsync(It.IsAny<IEnumerable<PriceHistoryRecord>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<PriceHistoryRecord>, CancellationToken>((records, _) => insertedBatches.Add(records))
                .Returns(Task.CompletedTask);
        repoMock.Setup(r => r.LogImportAsync(It.IsAny<PriceImportLogDto>()))
                .ReturnsAsync(new PriceImportLogDto());

        var sut = BuildSut(repoMock.Object);

        try
        {
            // Act
            var result = await sut.ImportFromFileAsync(tmpPath, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Imported.Should().Be(2);
            insertedBatches.SelectMany(b => b).Should().HaveCount(2);
            insertedBatches.SelectMany(b => b).All(r => r.DataSource == UsdaFmapImportService.DataSourceCode).Should().BeTrue();
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }

    [Fact]
    public async Task ImportFromFileAsync_FileNotFound_ReturnsFailure()
    {
        var repoMock = new Mock<IPriceRepository>();
        var sut = BuildSut(repoMock.Object);

        var result = await sut.ImportFromFileAsync("/nonexistent/path/file.csv", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
