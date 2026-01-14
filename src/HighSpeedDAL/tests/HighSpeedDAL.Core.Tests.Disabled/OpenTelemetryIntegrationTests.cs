using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using HighSpeedDAL.Core.Observability;

namespace HighSpeedDAL.Tests.Observability;

/// <summary>
/// Comprehensive unit tests for OpenTelemetry integration.
/// Tests distributed tracing, metrics collection, and activity correlation.
/// </summary>
public sealed class OpenTelemetryIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<OpenTelemetryIntegration>> _mockLogger;
    private readonly OpenTelemetryIntegration _otel;
    private readonly MeterListener _meterListener;
    private readonly ActivityListener _activityListener;
    private readonly Dictionary<string, long> _counterValues;
    private readonly Dictionary<string, List<double>> _histogramValues;
    private readonly List<Activity> _activities;

    public OpenTelemetryIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<OpenTelemetryIntegration>>();
        _counterValues = new Dictionary<string, long>();
        _histogramValues = new Dictionary<string, List<double>>();
        _activities = new List<Activity>();
        
        // Setup meter listener to capture metrics
        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == OpenTelemetryIntegration.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            string key = instrument.Name;
            if (_counterValues.ContainsKey(key))
            {
                _counterValues[key] += measurement;
            }
            else
            {
                _counterValues[key] = measurement;
            }
        });
        
        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            string key = instrument.Name;
            if (!_histogramValues.ContainsKey(key))
            {
                _histogramValues[key] = new List<double>();
            }
            _histogramValues[key].Add(measurement);
        });
        
        _meterListener.Start();
        
        // Setup activity listener to capture activities
        _activityListener = new ActivityListener
        {
            ShouldListenTo = (activitySource) => activitySource.Name == OpenTelemetryIntegration.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _activities.Add(activity)
        };
        
        ActivitySource.AddActivityListener(_activityListener);
        
        _otel = new OpenTelemetryIntegration(
            _mockLogger.Object,
            getActiveConnections: () => 5,
            getCacheEntryCount: () => 100,
            getStagingQueueSize: () => 25);
    }

    [Fact]
    public void Constructor_Should_InitializeSuccessfully()
    {
        // Assert
        _otel.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_Should_ThrowException_When_LoggerIsNull()
    {
        // Act
        Action act = () => new OpenTelemetryIntegration(null!);
        
        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void StartQueryActivity_Should_CreateActivity_WithCorrectTags()
    {
        // Act
        using (Activity? activity = _otel.StartQueryActivity("SELECT", "Products"))
        {
            // Assert
            activity.Should().NotBeNull();
            activity!.OperationName.Should().Be("DB SELECT");
            activity.Tags.Should().Contain(tag => tag.Key == "db.system" && tag.Value == "sqlserver");
            activity.Tags.Should().Contain(tag => tag.Key == "db.operation" && tag.Value == "SELECT");
            activity.Tags.Should().Contain(tag => tag.Key == "db.table" && tag.Value == "Products");
            activity.Tags.Should().Contain(tag => tag.Key == "component" && tag.Value == "HighSpeedDAL");
        }
    }

    [Fact]
    public void StartQueryActivity_Should_AddAdditionalTags()
    {
        // Arrange
        Dictionary<string, object?> additionalTags = new Dictionary<string, object?>
        {
            { "custom.tag1", "value1" },
            { "custom.tag2", 42 }
        };
        
        // Act
        using (Activity? activity = _otel.StartQueryActivity("INSERT", "Orders", additionalTags))
        {
            // Assert
            activity.Should().NotBeNull();
            activity!.Tags.Should().Contain(tag => tag.Key == "custom.tag1" && tag.Value == "value1");
            activity!.Tags.Should().Contain(tag => tag.Key == "custom.tag2" && tag.Value == "42");
        }
    }

    [Fact]
    public void StartCacheActivity_Should_CreateActivity_WithCorrectTags()
    {
        // Act
        using (Activity? activity = _otel.StartCacheActivity("GET", "user:123", hit: true))
        {
            // Assert
            activity.Should().NotBeNull();
            activity!.OperationName.Should().Be("Cache GET");
            activity.Tags.Should().Contain(tag => tag.Key == "cache.operation" && tag.Value == "GET");
            activity.Tags.Should().Contain(tag => tag.Key == "cache.key" && tag.Value == "user:123");
            activity.Tags.Should().Contain(tag => tag.Key == "cache.hit" && tag.Value == "True");
            activity.Tags.Should().Contain(tag => tag.Key == "component" && tag.Value == "HighSpeedDAL.Cache");
        }
    }

    [Fact]
    public void StartStagingSyncActivity_Should_CreateActivity_WithCorrectTags()
    {
        // Act
        using (Activity? activity = _otel.StartStagingSyncActivity("Products", 150))
        {
            // Assert
            activity.Should().NotBeNull();
            activity!.OperationName.Should().Be("Staging Sync");
            activity.Tags.Should().Contain(tag => tag.Key == "staging.table" && tag.Value == "Products");
            activity.Tags.Should().Contain(tag => tag.Key == "staging.record_count" && tag.Value == "150");
            activity.Tags.Should().Contain(tag => tag.Key == "component" && tag.Value == "HighSpeedDAL.Staging");
        }
    }

    [Fact]
    public void StartCdcCaptureActivity_Should_CreateActivity_WithCorrectTags()
    {
        // Act
        using (Activity? activity = _otel.StartCdcCaptureActivity("Orders", "UPDATE"))
        {
            // Assert
            activity.Should().NotBeNull();
            activity!.OperationName.Should().Be("CDC Capture");
            activity.Tags.Should().Contain(tag => tag.Key == "cdc.table" && tag.Value == "Orders");
            activity.Tags.Should().Contain(tag => tag.Key == "cdc.operation" && tag.Value == "UPDATE");
            activity.Tags.Should().Contain(tag => tag.Key == "component" && tag.Value == "HighSpeedDAL.CDC");
        }
    }

    [Fact]
    public void RecordQueryMetric_Should_IncrementCounter()
    {
        // Act
        _otel.RecordQueryMetric("SELECT", "Products", 45.5, 100, success: true);
        _otel.RecordQueryMetric("SELECT", "Products", 32.1, 50, success: true);
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        _counterValues.Should().ContainKey("highspeeddal.query.count");
        _counterValues["highspeeddal.query.count"].Should().Be(2);
    }

    [Fact]
    public void RecordQueryMetric_Should_RecordDurationHistogram()
    {
        // Act
        _otel.RecordQueryMetric("UPDATE", "Orders", 123.45, 10, success: true);
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        _histogramValues.Should().ContainKey("highspeeddal.query.duration");
        _histogramValues["highspeeddal.query.duration"].Should().Contain(123.45);
    }

    [Fact]
    public void RecordQueryMetric_Should_IncrementErrorCounter_WhenNotSuccessful()
    {
        // Act
        _otel.RecordQueryMetric("DELETE", "Products", 10.0, 0, success: false);
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        _counterValues.Should().ContainKey("highspeeddal.error.count");
        _counterValues["highspeeddal.error.count"].Should().BeGreaterThan(0);
    }

    [Fact]
    public void RecordCacheMetric_Should_IncrementHitCounter()
    {
        // Act
        _otel.RecordCacheMetric("GET", hit: true, 0.5);
        _otel.RecordCacheMetric("GET", hit: true, 0.3);
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        _counterValues.Should().ContainKey("highspeeddal.cache.hit");
        _counterValues["highspeeddal.cache.hit"].Should().Be(2);
    }

    [Fact]
    public void RecordCacheMetric_Should_IncrementMissCounter()
    {
        // Act
        _otel.RecordCacheMetric("GET", hit: false, 1.2);
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        _counterValues.Should().ContainKey("highspeeddal.cache.miss");
        _counterValues["highspeeddal.cache.miss"].Should().Be(1);
    }

    [Fact]
    public void RecordStagingSyncMetric_Should_IncrementCounter()
    {
        // Act
        _otel.RecordStagingSyncMetric("Products", 500, 1234.5, success: true);
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        _counterValues.Should().ContainKey("highspeeddal.staging.sync.count");
        _counterValues["highspeeddal.staging.sync.count"].Should().Be(1);
    }

    [Fact]
    public void RecordCdcCaptureMetric_Should_IncrementCounter()
    {
        // Act
        _otel.RecordCdcCaptureMetric("Orders", "INSERT", success: true);
        _otel.RecordCdcCaptureMetric("Orders", "UPDATE", success: true);
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        _counterValues.Should().ContainKey("highspeeddal.cdc.capture.count");
        _counterValues["highspeeddal.cdc.capture.count"].Should().Be(2);
    }

    [Fact]
    public void RecordError_Should_IncrementErrorCounter()
    {
        // Act
        _otel.RecordError("SqlException", "DatabaseProvider");
        _otel.RecordError("TimeoutException", "QueryBuilder");
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        _counterValues.Should().ContainKey("highspeeddal.error.count");
        _counterValues["highspeeddal.error.count"].Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void ExecuteWithTracing_Should_CreateActivity_AndRecordMetrics()
    {
        // Arrange
        bool actionExecuted = false;
        
        // Act
        _otel.ExecuteWithTracing("SELECT", "Products", () => { actionExecuted = true; });
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        actionExecuted.Should().BeTrue();
        _activities.Should().Contain(a => a.OperationName == "DB SELECT");
        _counterValues.Should().ContainKey("highspeeddal.query.count");
    }

    [Fact]
    public void ExecuteWithTracing_Should_RecordException_OnFailure()
    {
        // Arrange
        Exception testException = new InvalidOperationException("Test error");
        
        // Act
        Action act = () => _otel.ExecuteWithTracing("UPDATE", "Orders", () => throw testException);
        
        // Assert
        act.Should().Throw<InvalidOperationException>();
        
        Activity? failedActivity = _activities.FirstOrDefault(a => a.Status == ActivityStatusCode.Error);
        failedActivity.Should().NotBeNull();
        failedActivity!.StatusDescription.Should().Be("Test error");
    }

    [Fact]
    public async Task ExecuteWithTracingAsync_Should_CreateActivity_AndRecordMetrics()
    {
        // Arrange
        List<string> testData = new List<string> { "item1", "item2", "item3" };
        
        // Act
        List<string> result = await _otel.ExecuteWithTracingAsync(
            "SELECT",
            "Products",
            async () =>
            {
                await Task.Delay(10);
                return testData;
            });
        
        // Small delay to allow metrics to be captured
        System.Threading.Thread.Sleep(100);
        
        // Assert
        result.Should().HaveCount(3);
        _activities.Should().Contain(a => a.OperationName == "DB SELECT");
        _counterValues.Should().ContainKey("highspeeddal.query.count");
    }

    [Fact]
    public async Task ExecuteWithTracingAsync_Should_RecordException_OnFailure()
    {
        // Arrange
        Exception testException = new TimeoutException("Query timeout");
        
        // Act
        Func<Task> act = async () => await _otel.ExecuteWithTracingAsync<int>(
            "SELECT",
            "Orders",
            async () =>
            {
                await Task.Delay(1);
                throw testException;
            });
        
        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        
        Activity? failedActivity = _activities.FirstOrDefault(a => a.Status == ActivityStatusCode.Error);
        failedActivity.Should().NotBeNull();
        failedActivity!.StatusDescription.Should().Be("Query timeout");
    }

    [Fact]
    public void MultipleActivities_Should_BeCorrelated_WithParentChild()
    {
        // Act
        using (Activity? parent = _otel.StartQueryActivity("SELECT", "Orders"))
        {
            using (Activity? child1 = _otel.StartCacheActivity("GET", "orders:123", hit: false))
            {
                child1.Should().NotBeNull();
                child1!.ParentId.Should().Be(parent!.Id);
            }
            
            using (Activity? child2 = _otel.StartQueryActivity("SELECT", "OrderItems"))
            {
                child2.Should().NotBeNull();
                child2!.ParentId.Should().Be(parent!.Id);
            }
        }
        
        // Assert
        _activities.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Dispose_Should_DisposeResources()
    {
        // Arrange
        OpenTelemetryIntegration tempOtel = new OpenTelemetryIntegration(_mockLogger.Object);
        
        // Act
        tempOtel.Dispose();
        
        // Assert - no exceptions should be thrown
        Action act = () => tempOtel.Dispose(); // Should be safe to call multiple times
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        _otel?.Dispose();
        _meterListener?.Dispose();
        _activityListener?.Dispose();
    }
}
