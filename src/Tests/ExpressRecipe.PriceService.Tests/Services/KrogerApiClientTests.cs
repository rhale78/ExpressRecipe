using ExpressRecipe.PriceService.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Services;

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
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    [Fact]
    public async Task GetPricesAsync_WhenDisabled_ReturnsEmptyWithoutHttpCall()
    {
        var handler = new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK, "should not be called");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.kroger.com/v1") };
        var config = BuildConfig(enabled: false);
        var cache = new Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<KrogerApiClient>>();

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
