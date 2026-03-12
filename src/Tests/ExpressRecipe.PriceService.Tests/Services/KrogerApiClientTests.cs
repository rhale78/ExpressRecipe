using ExpressRecipe.PriceService.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static HybridCache BuildHybridCache()
    {
#pragma warning disable EXTEXP0018
        var services = new ServiceCollection();
        services.AddHybridCache();
        services.AddLogging();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
#pragma warning restore EXTEXP0018
    }

    [Fact]
    public async Task GetPricesAsync_WhenDisabled_ReturnsEmptyWithoutHttpCall()
    {
        var handler = new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK, "should not be called");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.kroger.com/v1") };
        var config = BuildConfig(enabled: false);
        var cache = BuildHybridCache();
        var logger = NullLogger<KrogerApiClient>.Instance;

        var client = new KrogerApiClient(httpClient, cache, config, logger);

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
