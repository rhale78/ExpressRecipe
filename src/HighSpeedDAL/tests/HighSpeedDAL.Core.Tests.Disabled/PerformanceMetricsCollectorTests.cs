using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HighSpeedDAL.Core.Observability;

namespace HighSpeedDAL.Tests.Observability;

/// <summary>
/// Comprehensive unit tests for Performance Metrics Collector.
/// 
/// Tests cover:
/// - Operation tracking
/// - Cache hit/miss tracking
/// - Error tracking
/// - Percentile calculations (P50, P95, P99)
/// - Prometheus export format
/// - Thread safety
/// - Metrics reset
/// 
/// HighSpeedDAL Framework v0.1 - Phase 4
/// </summary>
public sealed class PerformanceMetricsCollectorTests : IDisposable
{
    private readonly Mock<ILogger<PerformanceMetricsCollector>> _mockLogger;
    private readonly PerformanceMetricsCollector _metricsCollector;
    private bool _disposed;

    public PerformanceMetricsCollectorTests()
    {
        _mockLogger = new Mock<ILogger<PerformanceMetricsCollector>>();
        _metricsCollector = new PerformanceMetricsCollector(_mockLogger.Object, aggregationIntervalSeconds: 999999);
    }

    [Fact]
    public void TrackOperation_SimpleOperation_RecordsMetrics()
    {
        // Arrange
        string operationName = "TestOperation";

        // Act
        using (IDisposable scope = _metricsCollector.TrackOperation(operationName))
        {
            System.Threading.Thread.Sleep(10); // Simulate work
        }

        // Assert
        OperationMetrics? metrics = _metricsCollector.GetMetrics(operationName);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.TotalCount);
        Assert.True(metrics.MinDuration > 0);
        Assert.True(metrics.MaxDuration >= metrics.MinDuration);
    }

    [Fact]
    public async Task ExecuteWithMetricsAsync_SuccessfulOperation_RecordsSuccess()
    {
        // Arrange
        string operationName = "AsyncOperation";
        int expectedResult = 42;

        // Act
        int result = await _metricsCollector.ExecuteWithMetricsAsync(
            operationName,
            async () =>
            {
                await Task.Delay(10);
                return expectedResult;
            });

        // Assert
        Assert.Equal(expectedResult, result);
        OperationMetrics? metrics = _metricsCollector.GetMetrics(operationName);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.TotalCount);
        Assert.Equal(0, metrics.ErrorCount);
    }

    [Fact]
    public async Task ExecuteWithMetricsAsync_OperationThrows_RecordsErrorAndRethrows()
    {
        // Arrange
        string operationName = "FailingOperation";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _metricsCollector.ExecuteWithMetricsAsync<int>(
                operationName,
                async () =>
                {
                    await Task.Delay(5);
                    throw new InvalidOperationException("Test exception");
                });
        });

        OperationMetrics? metrics = _metricsCollector.GetMetrics(operationName);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.TotalCount);
        Assert.Equal(1, metrics.ErrorCount);
    }

    [Fact]
    public void RecordCacheHit_MultipleCalls_IncrementsCounter()
    {
        // Arrange
        string cacheName = "TestCache";

        // Act
        _metricsCollector.RecordCacheHit(cacheName);
        _metricsCollector.RecordCacheHit(cacheName);
        _metricsCollector.RecordCacheHit(cacheName);

        // Assert
        OperationMetrics? metrics = _metricsCollector.GetMetrics($"Cache_{cacheName}");
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics.CacheHits);
    }

    [Fact]
    public void RecordCacheMiss_MultipleCalls_IncrementsCounter()
    {
        // Arrange
        string cacheName = "TestCache";

        // Act
        _metricsCollector.RecordCacheMiss(cacheName);
        _metricsCollector.RecordCacheMiss(cacheName);

        // Assert
        OperationMetrics? metrics = _metricsCollector.GetMetrics($"Cache_{cacheName}");
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics.CacheMisses);
    }

    [Fact]
    public void RecordError_MultipleCalls_IncrementsErrorCounter()
    {
        // Arrange
        string operationName = "FailingOp";

        // Act
        _metricsCollector.RecordError(operationName);
        _metricsCollector.RecordError(operationName);
        _metricsCollector.RecordError(operationName);

        // Assert
        OperationMetrics? metrics = _metricsCollector.GetMetrics(operationName);
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics.ErrorCount);
    }

    [Fact]
    public void TrackOperation_MultipleOperations_CalculatesCorrectAverage()
    {
        // Arrange
        string operationName = "AverageTest";

        // Act
        for (int i = 0; i < 10; i++)
        {
            using (IDisposable scope = _metricsCollector.TrackOperation(operationName))
            {
                System.Threading.Thread.Sleep(10 + i); // Variable duration
            }
        }

        // Assert
        OperationMetrics? metrics = _metricsCollector.GetMetrics(operationName);
        Assert.NotNull(metrics);
        Assert.Equal(10, metrics.TotalCount);
        Assert.True(metrics.AverageDuration > metrics.MinDuration);
        Assert.True(metrics.AverageDuration < metrics.MaxDuration);
    }

    [Fact]
    public void CalculatePercentiles_WithData_CalculatesCorrectly()
    {
        // Arrange
        string operationName = "PercentileTest";
        OperationMetrics metrics = new OperationMetrics(operationName);

        // Record specific durations for predictable percentiles
        for (int i = 1; i <= 100; i++)
        {
            metrics.RecordDuration(i);
        }

        // Act
        metrics.CalculatePercentiles();

        // Assert
        Assert.Equal(50.5, metrics.P50, 1); // Median of 1-100 is 50.5
        Assert.True(metrics.P95 >= 95 && metrics.P95 <= 96);
        Assert.True(metrics.P99 >= 99 && metrics.P99 <= 100);
    }

    [Fact]
    public void GetAllMetrics_MultipleOperations_ReturnsAllMetrics()
    {
        // Arrange
        using (_metricsCollector.TrackOperation("Op1")) { }
        using (_metricsCollector.TrackOperation("Op2")) { }
        using (_metricsCollector.TrackOperation("Op3")) { }

        // Act
        Dictionary<string, OperationMetrics> allMetrics = _metricsCollector.GetAllMetrics();

        // Assert
        Assert.Equal(3, allMetrics.Count);
        Assert.Contains("Op1", allMetrics.Keys);
        Assert.Contains("Op2", allMetrics.Keys);
        Assert.Contains("Op3", allMetrics.Keys);
    }

    [Fact]
    public void ExportPrometheusFormat_WithMetrics_ReturnsValidFormat()
    {
        // Arrange
        using (_metricsCollector.TrackOperation("TestOp"))
        {
            System.Threading.Thread.Sleep(10);
        }

        _metricsCollector.RecordCacheHit("TestCache");
        _metricsCollector.RecordCacheMiss("TestCache");

        // Act
        string prometheusData = _metricsCollector.ExportPrometheusFormat();

        // Assert
        Assert.Contains("# HELP", prometheusData);
        Assert.Contains("# TYPE", prometheusData);
        Assert.Contains("highspeedal_cache_hits_total", prometheusData);
        Assert.Contains("highspeedal_cache_misses_total", prometheusData);
        Assert.Contains("highspeedal_operation_count_total", prometheusData);
        Assert.Contains("highspeedal_operation_duration_min_ms", prometheusData);
        Assert.Contains("highspeedal_operation_duration_max_ms", prometheusData);
        Assert.Contains("highspeedal_operation_duration_avg_ms", prometheusData);
    }

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        // Arrange
        using (_metricsCollector.TrackOperation("Op1")) { }
        _metricsCollector.RecordCacheHit("Cache1");
        _metricsCollector.RecordError("Op1");

        // Act
        _metricsCollector.Reset();

        // Assert
        Dictionary<string, OperationMetrics> allMetrics = _metricsCollector.GetAllMetrics();
        Assert.Empty(allMetrics);

        string prometheusData = _metricsCollector.ExportPrometheusFormat();
        Assert.Contains("highspeedal_cache_hits_total 0", prometheusData);
        Assert.Contains("highspeedal_cache_misses_total 0", prometheusData);
    }

    [Fact]
    public void TrackOperation_NullOrEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _metricsCollector.TrackOperation(null!));
        Assert.Throws<ArgumentException>(() => _metricsCollector.TrackOperation(""));
        Assert.Throws<ArgumentException>(() => _metricsCollector.TrackOperation("   "));
    }

    [Fact]
    public async Task ExecuteWithMetricsAsync_NullOperation_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _metricsCollector.ExecuteWithMetricsAsync<int>("TestOp", null!));
    }

    [Fact]
    public void RecordCacheHit_EmptyCacheName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _metricsCollector.RecordCacheHit(""));
    }

    [Fact]
    public async Task ConcurrentOperations_MultipleThreads_ThreadSafe()
    {
        // Arrange
        int threadCount = 10;
        int operationsPerThread = 100;
        List<Task> tasks = new List<Task>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            Task task = Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    using (IDisposable scope = _metricsCollector.TrackOperation($"Op{threadId}"))
                    {
                        // Simulate work
                        System.Threading.Thread.SpinWait(100);
                    }
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        for (int i = 0; i < threadCount; i++)
        {
            OperationMetrics? metrics = _metricsCollector.GetMetrics($"Op{i}");
            Assert.NotNull(metrics);
            Assert.Equal(operationsPerThread, metrics.TotalCount);
        }
    }

    [Fact]
    public void OperationMetrics_MinMaxTracking_CorrectValues()
    {
        // Arrange
        OperationMetrics metrics = new OperationMetrics("TestOp");

        // Act
        metrics.RecordDuration(100.0);
        metrics.RecordDuration(50.0);
        metrics.RecordDuration(200.0);
        metrics.RecordDuration(75.0);

        // Assert
        Assert.Equal(50.0, metrics.MinDuration);
        Assert.Equal(200.0, metrics.MaxDuration);
        Assert.Equal(4, metrics.TotalCount);
    }

    [Fact]
    public void OperationMetrics_AverageCalculation_Accurate()
    {
        // Arrange
        OperationMetrics metrics = new OperationMetrics("TestOp");

        // Act
        metrics.RecordDuration(10.0);
        metrics.RecordDuration(20.0);
        metrics.RecordDuration(30.0);
        metrics.RecordDuration(40.0);

        // Assert
        Assert.Equal(25.0, metrics.AverageDuration); // (10+20+30+40)/4 = 25
    }

    [Fact]
    public void CalculatePercentiles_EmptyData_ReturnsZero()
    {
        // Arrange
        OperationMetrics metrics = new OperationMetrics("TestOp");

        // Act
        metrics.CalculatePercentiles();

        // Assert
        Assert.Equal(0, metrics.P50);
        Assert.Equal(0, metrics.P95);
        Assert.Equal(0, metrics.P99);
    }

    [Fact]
    public void CalculatePercentiles_SingleValue_ReturnsThatValue()
    {
        // Arrange
        OperationMetrics metrics = new OperationMetrics("TestOp");
        metrics.RecordDuration(100.0);

        // Act
        metrics.CalculatePercentiles();

        // Assert
        Assert.Equal(100.0, metrics.P50);
        Assert.Equal(100.0, metrics.P95);
        Assert.Equal(100.0, metrics.P99);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _metricsCollector?.Dispose();
        _disposed = true;
    }
}
