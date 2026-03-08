using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Services;

/// <summary>
/// Tests for <see cref="PriceUnitNormalizer"/> — unit normalization and per-unit price computation.
/// </summary>
public class PriceUnitNormalizerTests
{
    private readonly PriceUnitNormalizer _sut = new();

    // ── Unit normalization ────────────────────────────────────────────────────

    [Theory]
    [InlineData("oz", "oz")]
    [InlineData("Oz", "oz")]
    [InlineData("OZ", "oz")]
    [InlineData("fl oz", "oz")]
    [InlineData("fluid oz", "oz")]
    [InlineData("ounce", "oz")]
    [InlineData("ounces", "oz")]
    [InlineData("g", "g")]
    [InlineData("gram", "g")]
    [InlineData("lb", "lb")]
    [InlineData("lbs", "lb")]
    [InlineData("pound", "lb")]
    [InlineData("kg", "kg")]
    [InlineData("ml", "ml")]
    [InlineData("l", "l")]
    [InlineData("liter", "l")]
    [InlineData("each", "each")]
    [InlineData("ea", "each")]
    [InlineData("ct", "each")]
    [InlineData("100g", "100g")]
    public void NormalizeUnit_KnownUnit_ReturnsCanonical(string input, string expected)
    {
        _sut.NormalizeUnit(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("banana")]
    public void NormalizeUnit_UnknownUnit_ReturnsNull(string? input)
    {
        _sut.NormalizeUnit(input).Should().BeNull();
    }

    // ── 12-pack of 12-oz cans → 144 oz total → price/oz ─────────────────────

    [Fact]
    public void ComputeUnitPrices_12PackOf12OzCans_PricePerOzCorrect()
    {
        // A 12-pack of 12 oz cans = 144 oz total. If priced at $7.99
        // the price/oz should be $7.99 / 144 ≈ 0.055486
        const decimal price = 7.99m;
        const decimal quantity = 144m;
        const string unit = "oz";

        var result = _sut.ComputeUnitPrices(price, unit, quantity);

        result.PricePerOz.Should().BeApproximately(price / quantity, 0.0001m);
        result.NormalizedUnit.Should().Be("oz");
    }

    // ── 2-liter bottle → 67.63 fl oz → price/oz ──────────────────────────────

    [Fact]
    public void ComputeUnitPrices_2LiterBottle_PricePerOzApprox67Oz()
    {
        // 2 liters = 2000 ml / 29.5735 ml per fl oz ≈ 67.63 oz
        const decimal price = 1.89m;
        const decimal quantity = 2m;
        const string unit = "l";

        var result = _sut.ComputeUnitPrices(price, unit, quantity);

        // Expected: price / 67.63 fl oz
        result.PricePerOz.Should().BeApproximately(price / 67.63m, 0.001m);
        result.NormalizedUnit.Should().Be("l");
    }

    // ── Per-100g calculation ──────────────────────────────────────────────────

    [Fact]
    public void ComputeUnitPrices_500g_PricePerHundredGCorrect()
    {
        const decimal price = 3.49m;
        const decimal quantity = 500m;
        const string unit = "g";

        var result = _sut.ComputeUnitPrices(price, unit, quantity);

        // price/500g * 100 = price/5
        result.PricePerHundredG.Should().BeApproximately(price / 5m, 0.0001m);
    }

    // ── No unit (each) returns no per-oz price ────────────────────────────────

    [Fact]
    public void ComputeUnitPrices_Each_NoOzPrice()
    {
        var result = _sut.ComputeUnitPrices(2.99m, "each", 1m);
        result.PricePerOz.Should().BeNull();
        result.PricePerHundredG.Should().BeNull();
        result.NormalizedUnit.Should().Be("each");
    }

    // ── Pound-based unit ──────────────────────────────────────────────────────

    [Fact]
    public void ComputeUnitPrices_OnePound_PricePerOzCorrect()
    {
        const decimal price = 4.00m;
        const decimal quantity = 1m;
        const string unit = "lb";

        var result = _sut.ComputeUnitPrices(price, unit, quantity);

        // 1 lb = 16 oz → price/oz = 4.00 / 16 = 0.25
        result.PricePerOz.Should().BeApproximately(0.25m, 0.0001m);
    }
}

/// <summary>
/// Tests for <see cref="EffectivePriceCalculator"/> — deal effective price computation.
/// </summary>
public class EffectivePriceCalculatorTests
{
    private readonly EffectivePriceCalculator _sut = new();

    private static DealDto BuildDeal(
        string discountType,
        decimal basePrice,
        decimal salePrice = 0,
        int? buyQty = null,
        int? getQty = null,
        decimal? getPercentOff = null,
        decimal? rebateAmount = null)
    {
        return new DealDto
        {
            ProductId = Guid.NewGuid(),
            StoreId = Guid.NewGuid(),
            DealType = discountType,
            DiscountType = discountType,
            OriginalPrice = basePrice,
            SalePrice = salePrice > 0 ? salePrice : basePrice,
            BuyQuantity = buyQty,
            GetQuantity = getQty,
            GetPercentOff = getPercentOff,
            RebateAmount = rebateAmount
        };
    }

    // ── BOGO — buy 1 get 1 free: qty=1 → 50% effective ─────────────────────

    [Fact]
    public void Calculate_BOGO_Qty1_EffectiveIs50Pct()
    {
        var deal = BuildDeal("BuyOneGetOne", basePrice: 3.00m, buyQty: 1, getQty: 1);
        var result = _sut.Calculate(3.00m, deal, 2);

        result.EffectivePricePerUnit.Should().Be(1.50m);
        result.TotalCost.Should().Be(3.00m); // 1 paid × $3
        result.SavingsPct.Should().Be(50m);
    }

    // ── B2G1 free — buy 3 units: 33% savings ─────────────────────────────────

    [Fact]
    public void Calculate_B2G1Free_Qty3_33PctSavings()
    {
        var deal = BuildDeal("BuyNGetMFree", basePrice: 2.00m, buyQty: 2, getQty: 1);
        var result = _sut.Calculate(2.00m, deal, 3);

        // Pay for 2 out of 3 → total = 4.00; effective per unit = 4/3 ≈ 1.333
        result.TotalCost.Should().Be(4.00m);
        result.SavingsPct.Should().BeApproximately(33.33m, 0.01m);
    }

    // ── Coupon $1.00 off ─────────────────────────────────────────────────────

    [Fact]
    public void Calculate_Coupon_1DollarOff_PriceReducedBy1()
    {
        var deal = BuildDeal("Coupon", basePrice: 5.00m, rebateAmount: 1.00m);
        var result = _sut.Calculate(5.00m, deal, 1);

        result.EffectivePricePerUnit.Should().Be(4.00m);
        result.Savings.Should().Be(1.00m);
    }

    // ── GetPercentOff 25% ─────────────────────────────────────────────────────

    [Fact]
    public void Calculate_GetPercentOff25_PriceReducedBy25Pct()
    {
        var deal = BuildDeal("FlyerSale", basePrice: 4.00m, getPercentOff: 25m);
        var result = _sut.Calculate(4.00m, deal, 1);

        result.EffectivePricePerUnit.Should().Be(3.00m);
        result.SavingsPct.Should().Be(25m);
    }

    // ── No deal — returns base price unchanged ────────────────────────────────

    [Fact]
    public void Calculate_NoDeal_ReturnsBasePrice()
    {
        var result = _sut.Calculate(2.50m, null, 4);

        result.EffectivePricePerUnit.Should().Be(2.50m);
        result.TotalCost.Should().Be(10.00m);
        result.Savings.Should().Be(0m);
        result.AppliedDealType.Should().BeNull();
    }

    // ── InstantRebate $0.50 ──────────────────────────────────────────────────

    [Fact]
    public void Calculate_InstantRebate_ReducesByRebateAmount()
    {
        var deal = BuildDeal("InstantRebate", basePrice: 3.00m, rebateAmount: 0.50m);
        var result = _sut.Calculate(3.00m, deal, 2);

        result.EffectivePricePerUnit.Should().Be(2.50m);
        result.TotalCost.Should().Be(5.00m);
    }
}

/// <summary>
/// Tests for <see cref="UsdaFmapImportService"/>.
/// Uses a real temporary CSV file to test the parse+import pipeline.
/// </summary>
public class UsdaFmapImportServiceTests
{
    private static UsdaFmapImportService BuildSut(IPriceRepository repo)
    {
        var loggerMock = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<UsdaFmapImportService>>();
        var configMock = new Moq.Mock<Microsoft.Extensions.Configuration.IConfiguration>();
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
        var repoMock = new Moq.Mock<IPriceRepository>();
        repoMock.Setup(r => r.BulkInsertPriceHistoryAsync(Moq.It.IsAny<IEnumerable<PriceHistoryRecord>>(), Moq.It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<PriceHistoryRecord>, CancellationToken>((records, _) => insertedBatches.Add(records))
                .Returns(Task.CompletedTask);
        repoMock.Setup(r => r.LogImportAsync(Moq.It.IsAny<PriceImportLogDto>()))
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
        var repoMock = new Moq.Mock<IPriceRepository>();
        var sut = BuildSut(repoMock.Object);

        var result = await sut.ImportFromFileAsync("/nonexistent/path/file.csv", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}

/// <summary>Tests that KrogerApiClient.IsEnabled=false returns empty lists without HTTP calls.</summary>
public class KrogerApiClientTests
{
    private static IConfiguration BuildConfig(bool enabled)
    {
        var dict = new Dictionary<string, string?>
        {
            ["ExternalApis:Kroger:Enabled"] = enabled ? "true" : "false",
            ["ExternalApis:Kroger:BaseUrl"] = "https://api.kroger.com/v1"
        };
        return new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    [Fact]
    public async Task GetPricesAsync_WhenDisabled_ReturnsEmptyWithoutHttpCall()
    {
        var handler = new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK, "should not be called");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.kroger.com/v1") };
        var config = BuildConfig(enabled: false);
        var cache = new Moq.Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        var logger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<KrogerApiClient>>();

        var client = new KrogerApiClient(httpClient, cache.Object, config, logger.Object);

        client.IsEnabled.Should().BeFalse();
        var result = await client.GetPricesAsync("012345678901", CancellationToken.None);
        result.Should().BeEmpty();
        handler.CallCount.Should().Be(0);
    }
}

/// <summary>HTTP message handler that tracks call count and always returns a fixed response.</summary>
internal sealed class FakeHttpMessageHandler : System.Net.Http.DelegatingHandler
{
    private readonly System.Net.HttpStatusCode _statusCode;
    private readonly string _content;
    public int CallCount { get; private set; }

    public FakeHttpMessageHandler(System.Net.HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
        System.Net.Http.HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(new System.Net.Http.HttpResponseMessage(_statusCode)
        {
            Content = new System.Net.Http.StringContent(_content)
        });
    }
}
