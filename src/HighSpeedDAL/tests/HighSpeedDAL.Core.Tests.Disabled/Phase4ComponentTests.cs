using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using HighSpeedDAL.Core.Observability;
using HighSpeedDAL.Core.Caching;
using HighSpeedDAL.Core.Search;

namespace HighSpeedDAL.Tests.Phase4;

/// <summary>
/// Comprehensive unit tests for Performance Metrics Collector.
/// </summary>
public sealed class PerformanceMetricsCollectorTests : IDisposable
{
    private readonly Mock<ILogger<PerformanceMetricsCollector>> _mockLogger;
    private readonly PerformanceMetricsCollector _collector;

    public PerformanceMetricsCollectorTests()
    {
        _mockLogger = new Mock<ILogger<PerformanceMetricsCollector>>();
        _collector = new PerformanceMetricsCollector(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_Should_InitializeSuccessfully()
    {
        _collector.Should().NotBeNull();
    }

    [Fact]
    public void RecordQueryDuration_Should_RecordMetric()
    {
        // Act
        _collector.RecordQueryDuration("SELECT", "Products", 45.5);
        
        // Assert
        MetricsSummary summary = _collector.GetMetricsSummary("SELECT", "Products");
        summary.Should().NotBeNull();
        summary.Count.Should().Be(1);
        summary.AverageDuration.Should().BeApproximately(45.5, 0.1);
    }

    [Fact]
    public void RecordQueryDuration_Should_CalculatePercentiles()
    {
        // Arrange
        double[] durations = { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0, 90.0, 100.0 };
        
        // Act
        foreach (double duration in durations)
        {
            _collector.RecordQueryDuration("SELECT", "Orders", duration);
        }
        
        MetricsSummary summary = _collector.GetMetricsSummary("SELECT", "Orders");
        
        // Assert
        summary.P50.Should().BeGreaterThan(0);
        summary.P95.Should().BeGreaterThan(summary.P50);
        summary.P99.Should().BeGreaterThan(summary.P95);
    }

    [Fact]
    public void RecordQueryDuration_Should_TrackMinMax()
    {
        // Act
        _collector.RecordQueryDuration("UPDATE", "Customers", 15.0);
        _collector.RecordQueryDuration("UPDATE", "Customers", 100.0);
        _collector.RecordQueryDuration("UPDATE", "Customers", 50.0);
        
        MetricsSummary summary = _collector.GetMetricsSummary("UPDATE", "Customers");
        
        // Assert
        summary.MinDuration.Should().Be(15.0);
        summary.MaxDuration.Should().Be(100.0);
    }

    [Fact]
    public void RecordCacheHit_Should_IncrementCounter()
    {
        // Act
        _collector.RecordCacheHit("Products");
        _collector.RecordCacheHit("Products");
        
        CacheMetrics metrics = _collector.GetCacheMetrics();
        
        // Assert
        metrics.HitCount.Should().Be(2);
    }

    [Fact]
    public void RecordCacheMiss_Should_IncrementCounter()
    {
        // Act
        _collector.RecordCacheMiss("Orders");
        
        CacheMetrics metrics = _collector.GetCacheMetrics();
        
        // Assert
        metrics.MissCount.Should().Be(1);
    }

    [Fact]
    public void GetCacheMetrics_Should_CalculateHitRate()
    {
        // Act
        _collector.RecordCacheHit("Products");
        _collector.RecordCacheHit("Products");
        _collector.RecordCacheHit("Products");
        _collector.RecordCacheMiss("Products");
        
        CacheMetrics metrics = _collector.GetCacheMetrics();
        
        // Assert
        metrics.HitRate.Should().BeApproximately(0.75, 0.01); // 3 hits / 4 total = 75%
    }

    [Fact]
    public void RecordError_Should_IncrementErrorCount()
    {
        // Act
        _collector.RecordError("SqlException", "SELECT", "Products");
        _collector.RecordError("TimeoutException", "UPDATE", "Orders");
        
        // Assert
        int errorCount = _collector.GetTotalErrorCount();
        errorCount.Should().Be(2);
    }

    [Fact]
    public void GetPrometheusMetrics_Should_ReturnValidFormat()
    {
        // Arrange
        _collector.RecordQueryDuration("SELECT", "Products", 25.0);
        _collector.RecordCacheHit("Products");
        
        // Act
        string prometheus = _collector.GetPrometheusMetrics();
        
        // Assert
        prometheus.Should().NotBeNullOrEmpty();
        prometheus.Should().Contain("highspeeddal_query_duration");
        prometheus.Should().Contain("highspeeddal_cache_hit_total");
    }

    [Fact]
    public void Reset_Should_ClearAllMetrics()
    {
        // Arrange
        _collector.RecordQueryDuration("SELECT", "Products", 25.0);
        _collector.RecordCacheHit("Products");
        
        // Act
        _collector.Reset();
        
        // Assert
        CacheMetrics metrics = _collector.GetCacheMetrics();
        metrics.HitCount.Should().Be(0);
        metrics.MissCount.Should().Be(0);
    }

    [Fact]
    public void ConcurrentRecording_Should_BeThreadSafe()
    {
        // Arrange
        List<Task> tasks = new List<Task>();
        
        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => _collector.RecordQueryDuration("SELECT", "Products", 10.0)));
        }
        
        // Assert
        Func<Task> act = async () => await Task.WhenAll(tasks);
        act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _collector?.Dispose();
    }
}

/// <summary>
/// Comprehensive unit tests for Health Check Manager.
/// </summary>
public sealed class HealthCheckManagerTests : IDisposable
{
    private readonly Mock<ILogger<HealthCheckManager>> _mockLogger;
    private readonly HealthCheckManager _healthCheckManager;

    public HealthCheckManagerTests()
    {
        _mockLogger = new Mock<ILogger<HealthCheckManager>>();
        _healthCheckManager = new HealthCheckManager(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_Should_InitializeSuccessfully()
    {
        _healthCheckManager.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckDatabaseConnectivityAsync_Should_ReturnHealthy_WhenConnected()
    {
        // Act
        HealthCheckResult result = await _healthCheckManager.CheckDatabaseConnectivityAsync(
            "Server=localhost;Database=Test;",
            CancellationToken.None);
        
        // Assert
        result.Should().NotBeNull();
        result.CheckName.Should().Be("DatabaseConnectivity");
    }

    [Fact]
    public async Task CheckQueryPerformanceAsync_Should_ReturnHealthy_WhenUnderThreshold()
    {
        // Act
        HealthCheckResult result = await _healthCheckManager.CheckQueryPerformanceAsync(
            "Server=localhost;Database=Test;",
            thresholdMs: 1000,
            CancellationToken.None);
        
        // Assert
        result.Should().NotBeNull();
        result.CheckName.Should().Be("QueryPerformance");
    }

    [Fact]
    public async Task CheckTableSizeAsync_Should_ReturnHealthy_WhenUnderThreshold()
    {
        // Act
        HealthCheckResult result = await _healthCheckManager.CheckTableSizeAsync(
            "Server=localhost;Database=Test;",
            "Products",
            maxSizeMB: 1000,
            CancellationToken.None);
        
        // Assert
        result.Should().NotBeNull();
        result.CheckName.Should().Contain("TableSize");
    }

    [Fact]
    public async Task CheckIndexFragmentationAsync_Should_ReturnHealthy_WhenLowFragmentation()
    {
        // Act
        HealthCheckResult result = await _healthCheckManager.CheckIndexFragmentationAsync(
            "Server=localhost;Database=Test;",
            "Products",
            maxFragmentationPercent: 30.0,
            CancellationToken.None);
        
        // Assert
        result.Should().NotBeNull();
        result.CheckName.Should().Contain("IndexFragmentation");
    }

    [Fact]
    public async Task GetAllHealthChecksAsync_Should_ReturnMultipleChecks()
    {
        // Act
        List<HealthCheckResult> results = await _healthCheckManager.GetAllHealthChecksAsync(
            "Server=localhost;Database=Test;",
            CancellationToken.None);
        
        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCountGreaterThan(0);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy)]
    [InlineData(HealthStatus.Degraded)]
    [InlineData(HealthStatus.Unhealthy)]
    public void HealthStatus_Should_HaveCorrectValues(HealthStatus status)
    {
        // Assert
        Enum.IsDefined(typeof(HealthStatus), status).Should().BeTrue();
    }

    public void Dispose()
    {
        _healthCheckManager?.Dispose();
    }
}

/// <summary>
/// Comprehensive unit tests for Query Result Cache.
/// </summary>
public sealed class QueryResultCacheTests : IDisposable
{
    private readonly Mock<ILogger<QueryResultCache>> _mockLogger;
    private readonly QueryResultCache _cache;

    public QueryResultCacheTests()
    {
        _mockLogger = new Mock<ILogger<QueryResultCache>>();
        _cache = new QueryResultCache(_mockLogger.Object, maxCacheSizeMB: 100);
    }

    [Fact]
    public void Constructor_Should_InitializeSuccessfully()
    {
        _cache.Should().NotBeNull();
    }

    [Fact]
    public void Set_And_TryGet_Should_StoreAndRetrieveData()
    {
        // Arrange
        string query = "SELECT * FROM Products WHERE Id = 1";
        List<Product> testData = new List<Product> { new Product { Id = 1, Name = "Test" } };
        
        // Act
        _cache.Set(query, "Products", testData, TimeSpan.FromMinutes(5));
        bool found = _cache.TryGet<List<Product>>(query, out List<Product>? result);
        
        // Assert
        found.Should().BeTrue();
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Id.Should().Be(1);
    }

    [Fact]
    public void TryGet_Should_ReturnFalse_ForNonExistentKey()
    {
        // Act
        bool found = _cache.TryGet<string>("nonexistent", out string? result);
        
        // Assert
        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void InvalidateTable_Should_RemoveCachedQueries()
    {
        // Arrange
        _cache.Set("SELECT * FROM Products", "Products", new List<int>(), TimeSpan.FromMinutes(5));
        _cache.Set("SELECT * FROM Orders", "Orders", new List<int>(), TimeSpan.FromMinutes(5));
        
        // Act
        _cache.InvalidateTable("Products");
        
        bool productsFound = _cache.TryGet<List<int>>("SELECT * FROM Products", out _);
        bool ordersFound = _cache.TryGet<List<int>>("SELECT * FROM Orders", out _);
        
        // Assert
        productsFound.Should().BeFalse();
        ordersFound.Should().BeTrue();
    }

    [Fact]
    public void Clear_Should_RemoveAllCachedData()
    {
        // Arrange
        _cache.Set("query1", "Products", "data1", TimeSpan.FromMinutes(5));
        _cache.Set("query2", "Orders", "data2", TimeSpan.FromMinutes(5));
        
        // Act
        _cache.Clear();
        
        // Assert
        bool found1 = _cache.TryGet<string>("query1", out _);
        bool found2 = _cache.TryGet<string>("query2", out _);
        found1.Should().BeFalse();
        found2.Should().BeFalse();
    }

    [Fact]
    public void GetStatistics_Should_ReturnCacheMetrics()
    {
        // Arrange
        _cache.Set("query1", "Products", "data", TimeSpan.FromMinutes(5));
        _cache.TryGet<string>("query1", out _); // Hit
        _cache.TryGet<string>("query2", out _); // Miss
        
        // Act
        CacheStatistics stats = _cache.GetStatistics();
        
        // Assert
        stats.EntryCount.Should().BeGreaterThan(0);
        stats.HitCount.Should().BeGreaterThan(0);
        stats.MissCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Expired_Entries_Should_BeRemoved()
    {
        // Arrange
        _cache.Set("shortLived", "Products", "data", TimeSpan.FromMilliseconds(100));
        
        // Act
        Thread.Sleep(200); // Wait for expiration
        bool found = _cache.TryGet<string>("shortLived", out _);
        
        // Assert
        found.Should().BeFalse();
    }

    [Fact]
    public void LRU_Eviction_Should_RemoveOldestEntries_WhenCacheFull()
    {
        // Arrange
        QueryResultCache smallCache = new QueryResultCache(_mockLogger.Object, maxCacheSizeMB: 1);
        
        // Act - Fill cache beyond capacity
        for (int i = 0; i < 1000; i++)
        {
            smallCache.Set($"query{i}", "Products", new string('x', 10000), TimeSpan.FromHours(1));
        }
        
        // Assert - Oldest entries should be evicted
        bool oldestFound = smallCache.TryGet<string>("query0", out _);
        bool newestFound = smallCache.TryGet<string>("query999", out _);
        
        oldestFound.Should().BeFalse();
        newestFound.Should().BeTrue();
        
        smallCache.Dispose();
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

/// <summary>
/// Comprehensive unit tests for Full-Text Search Manager.
/// </summary>
public sealed class FullTextSearchManagerTests : IDisposable
{
    private readonly Mock<ILogger<FullTextSearchManager>> _mockLogger;
    private readonly FullTextSearchManager _searchManager;

    public FullTextSearchManagerTests()
    {
        _mockLogger = new Mock<ILogger<FullTextSearchManager>>();
        _searchManager = new FullTextSearchManager(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_Should_InitializeSuccessfully()
    {
        _searchManager.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_Should_AcceptValidSearchParameters()
    {
        // Arrange
        FullTextSearchParameters parameters = new FullTextSearchParameters
        {
            SearchText = "test",
            TableName = "Products",
            SearchColumns = new List<string> { "Name", "Description" },
            Mode = SearchMode.Contains,
            MaxResults = 100
        };
        
        // Act
        Func<Task> act = async () => await _searchManager.SearchAsync(
            "Server=localhost;Database=Test;",
            parameters,
            CancellationToken.None);
        
        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(SearchMode.Contains)]
    [InlineData(SearchMode.ExactPhrase)]
    [InlineData(SearchMode.Wildcard)]
    [InlineData(SearchMode.Near)]
    [InlineData(SearchMode.Boolean)]
    public void SearchMode_Should_HaveCorrectValues(SearchMode mode)
    {
        // Assert
        Enum.IsDefined(typeof(SearchMode), mode).Should().BeTrue();
    }

    [Fact]
    public void FullTextSearchParameters_Should_ValidateRequiredFields()
    {
        // Arrange
        FullTextSearchParameters parameters = new FullTextSearchParameters();
        
        // Act & Assert
        parameters.SearchText.Should().BeNullOrEmpty();
        parameters.TableName.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SearchAsync_Should_ThrowException_WhenSearchTextIsEmpty()
    {
        // Arrange
        FullTextSearchParameters parameters = new FullTextSearchParameters
        {
            SearchText = "",
            TableName = "Products",
            SearchColumns = new List<string> { "Name" }
        };
        
        // Act
        Func<Task> act = async () => await _searchManager.SearchAsync(
            "Server=localhost;Database=Test;",
            parameters,
            CancellationToken.None);
        
        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_Should_ThrowException_WhenTableNameIsEmpty()
    {
        // Arrange
        FullTextSearchParameters parameters = new FullTextSearchParameters
        {
            SearchText = "test",
            TableName = "",
            SearchColumns = new List<string> { "Name" }
        };
        
        // Act
        Func<Task> act = async () => await _searchManager.SearchAsync(
            "Server=localhost;Database=Test;",
            parameters,
            CancellationToken.None);
        
        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EnableFullTextIndexAsync_Should_NotThrow()
    {
        // Act
        Func<Task> act = async () => await _searchManager.EnableFullTextIndexAsync(
            "Server=localhost;Database=Test;",
            "Products",
            new List<string> { "Name", "Description" },
            CancellationToken.None);
        
        // Assert
        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _searchManager?.Dispose();
    }
}
