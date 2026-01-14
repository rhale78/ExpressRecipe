using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.Observability;

/// <summary>
/// OpenTelemetry integration for distributed tracing and metrics.
/// 
/// Features:
/// - Distributed tracing with Activity API
/// - Custom metrics with Meter API
/// - Automatic trace context propagation
/// - Integration with OTLP exporters
/// - Support for Jaeger, Zipkin, Application Insights
/// 
/// Metrics exported:
/// - dal.queries.total (counter)
/// - dal.query.duration (histogram)
/// - dal.errors.total (counter)
/// - dal.cache.hits (counter)
/// - dal.cache.misses (counter)
/// - dal.connections.active (observable gauge)
/// 
/// Example:
/// OpenTelemetryIntegration otel = new OpenTelemetryIntegration(logger, "HighSpeedDAL");
/// 
/// using (Activity? activity = otel.StartActivity("GetProducts"))
/// {
///     activity?.SetTag("category", "Electronics");
///     var products = await repository.GetProductsAsync();
///     activity?.SetTag("result.count", products.Count);
/// }
/// 
/// HighSpeedDAL Framework v0.1 - Phase 4
/// </summary>
public sealed class OpenTelemetryIntegration : IDisposable
{
    private readonly ILogger<OpenTelemetryIntegration> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    
    // Counters
    private readonly Counter<long> _queriesTotal;
    private readonly Counter<long> _errorsTotal;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    
    // Histograms
    private readonly Histogram<double> _queryDuration;
    
    // Observable Gauges
    private readonly ObservableGauge<int> _activeConnections;
    
    private int _currentActiveConnections;
    private bool _disposed;

    /// <summary>
    /// Activity source name for distributed tracing.
    /// Default: "HighSpeedDAL"
    /// </summary>
    public const string ActivitySourceName = "HighSpeedDAL";

    /// <summary>
    /// Meter name for metrics collection.
    /// Default: "HighSpeedDAL.Metrics"
    /// </summary>
    public const string MeterName = "HighSpeedDAL.Metrics";

    /// <summary>
    /// Version for instrumentation library.
    /// </summary>
    public const string InstrumentationVersion = "0.1.0";

    public OpenTelemetryIntegration(
        ILogger<OpenTelemetryIntegration> logger,
        string serviceName = "HighSpeedDAL",
        string serviceVersion = "0.1")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create Activity Source for distributed tracing
        _activitySource = new ActivitySource(ActivitySourceName, InstrumentationVersion);
        
        // Create Meter for metrics
        _meter = new Meter(MeterName, InstrumentationVersion);
        
        // Initialize Counters
        _queriesTotal = _meter.CreateCounter<long>(
            name: "dal.queries.total",
            unit: "{query}",
            description: "Total number of database queries executed");
            
        _errorsTotal = _meter.CreateCounter<long>(
            name: "dal.errors.total",
            unit: "{error}",
            description: "Total number of database operation errors");
            
        _cacheHits = _meter.CreateCounter<long>(
            name: "dal.cache.hits",
            unit: "{hit}",
            description: "Total number of cache hits");
            
        _cacheMisses = _meter.CreateCounter<long>(
            name: "dal.cache.misses",
            unit: "{miss}",
            description: "Total number of cache misses");
        
        // Initialize Histograms
        _queryDuration = _meter.CreateHistogram<double>(
            name: "dal.query.duration",
            unit: "ms",
            description: "Duration of database query operations in milliseconds");
        
        // Initialize Observable Gauges
        _activeConnections = _meter.CreateObservableGauge<int>(
            name: "dal.connections.active",
            observeValue: () => _currentActiveConnections,
            unit: "{connection}",
            description: "Number of active database connections");

        _logger.LogInformation(
            "OpenTelemetry Integration initialized for service: {Service}, version: {Version}",
            serviceName, serviceVersion);
    }

    /// <summary>
    /// Starts a new activity (span) for distributed tracing.
    /// </summary>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="kind">Activity kind (Internal, Server, Client, Producer, Consumer)</param>
    /// <param name="tags">Optional tags to add to the activity</param>
    /// <returns>Activity instance or null if tracing is not enabled</returns>
    public Activity? StartActivity(
        string operationName,
        ActivityKind kind = ActivityKind.Internal,
        Dictionary<string, object?>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));
        }

        Activity? activity = _activitySource.StartActivity(operationName, kind);

        if (activity != null && tags != null)
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }

        _logger.LogDebug("Started activity: {Operation}, Kind: {Kind}", operationName, kind);

        return activity;
    }

    /// <summary>
    /// Executes an operation with automatic tracing and metrics.
    /// </summary>
    /// <typeparam name="TResult">Return type of the operation</typeparam>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="operation">Operation to execute</param>
    /// <param name="tags">Optional tags for the activity</param>
    /// <returns>Result of the operation</returns>
    public async Task<TResult> TraceOperationAsync<TResult>(
        string operationName,
        Func<Task<TResult>> operation,
        Dictionary<string, object?>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));
        }
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        using (Activity? activity = StartActivity(operationName, ActivityKind.Internal, tags))
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool success = true;

            try
            {
                TResult result = await operation();
                
                activity?.SetTag("success", true);
                
                return result;
            }
            catch (Exception ex)
            {
                success = false;
                
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", ex.GetType().Name);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                RecordError(operationName);
                
                throw;
            }
            finally
            {
                stopwatch.Stop();
                
                double durationMs = stopwatch.Elapsed.TotalMilliseconds;
                
                activity?.SetTag("duration.ms", durationMs);
                
                if (success)
                {
                    RecordQueryDuration(durationMs, operationName);
                    RecordQuery(operationName);
                }
            }
        }
    }

    /// <summary>
    /// Records a query execution.
    /// </summary>
    /// <param name="operationName">Operation name</param>
    /// <param name="tableName">Optional table name</param>
    public void RecordQuery(string operationName, string? tableName = null)
    {
        TagList tags = new TagList
        {
            { "operation", operationName }
        };

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            tags.Add("table", tableName);
        }

        _queriesTotal.Add(1, tags);

        _logger.LogDebug("Recorded query: {Operation}", operationName);
    }

    /// <summary>
    /// Records query duration.
    /// </summary>
    /// <param name="durationMs">Duration in milliseconds</param>
    /// <param name="operationName">Operation name</param>
    /// <param name="tableName">Optional table name</param>
    public void RecordQueryDuration(double durationMs, string operationName, string? tableName = null)
    {
        TagList tags = new TagList
        {
            { "operation", operationName }
        };

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            tags.Add("table", tableName);
        }

        _queryDuration.Record(durationMs, tags);

        _logger.LogDebug("Recorded query duration: {Duration}ms for {Operation}", durationMs, operationName);
    }

    /// <summary>
    /// Records an error.
    /// </summary>
    /// <param name="operationName">Operation name</param>
    /// <param name="errorType">Optional error type</param>
    public void RecordError(string operationName, string? errorType = null)
    {
        TagList tags = new TagList
        {
            { "operation", operationName }
        };

        if (!string.IsNullOrWhiteSpace(errorType))
        {
            tags.Add("error.type", errorType);
        }

        _errorsTotal.Add(1, tags);

        _logger.LogDebug("Recorded error for operation: {Operation}", operationName);
    }

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    /// <param name="cacheName">Cache name</param>
    public void RecordCacheHit(string cacheName)
    {
        TagList tags = new TagList
        {
            { "cache", cacheName }
        };

        _cacheHits.Add(1, tags);

        _logger.LogDebug("Recorded cache hit: {Cache}", cacheName);
    }

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    /// <param name="cacheName">Cache name</param>
    public void RecordCacheMiss(string cacheName)
    {
        TagList tags = new TagList
        {
            { "cache", cacheName }
        };

        _cacheMisses.Add(1, tags);

        _logger.LogDebug("Recorded cache miss: {Cache}", cacheName);
    }

    /// <summary>
    /// Sets the number of active connections for gauge reporting.
    /// </summary>
    /// <param name="count">Number of active connections</param>
    public void SetActiveConnections(int count)
    {
        _currentActiveConnections = count;

        _logger.LogDebug("Updated active connections: {Count}", count);
    }

    /// <summary>
    /// Increments active connection count.
    /// </summary>
    public void IncrementActiveConnections()
    {
        System.Threading.Interlocked.Increment(ref _currentActiveConnections);
    }

    /// <summary>
    /// Decrements active connection count.
    /// </summary>
    public void DecrementActiveConnections()
    {
        System.Threading.Interlocked.Decrement(ref _currentActiveConnections);
    }

    /// <summary>
    /// Adds an event to the current activity.
    /// </summary>
    /// <param name="name">Event name</param>
    /// <param name="tags">Optional event tags</param>
    public void AddEvent(string name, Dictionary<string, object?>? tags = null)
    {
        Activity? activity = Activity.Current;

        if (activity != null)
        {
            ActivityTagsCollection tagsCollection = new ActivityTagsCollection();

            if (tags != null)
            {
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    tagsCollection.Add(tag.Key, tag.Value);
                }
            }

            activity.AddEvent(new ActivityEvent(name, tags: tagsCollection));

            _logger.LogDebug("Added event to activity: {Event}", name);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _activitySource?.Dispose();
        _meter?.Dispose();
        _disposed = true;

        _logger.LogInformation("OpenTelemetry Integration disposed");
    }
}
