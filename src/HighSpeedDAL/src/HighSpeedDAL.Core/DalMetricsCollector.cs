using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;

namespace HighSpeedDAL.Core;

/// <summary>
/// Collects and emits metrics for DAL operations with per-table granularity.
/// Metrics are named per-table (e.g., dal.products.operations, dal.ingredients.cache.hits)
/// plus a total counter for all operations combined.
/// </summary>
public class DalMetricsCollector : IDisposable
{
    private readonly Meter _meter;
    // Key: tableName_operationType, Value: counter
    private readonly Dictionary<string, Counter<long>> _tableOperationCounters = new();
    // Key: tableName, Value: hits counter
    private readonly Dictionary<string, Counter<long>> _tableCacheHitCounters = new();
    // Key: tableName, Value: misses counter
    private readonly Dictionary<string, Counter<long>> _tableCacheMissCounters = new();
    // Key: tableName, Value: item count gauge
    private readonly Dictionary<string, ObservableGauge<int>> _tableItemCountGauges = new();
    // Key: tableName, Value: cache size gauge
    private readonly Dictionary<string, ObservableGauge<int>> _tableCacheSizeGauges = new();
    // Store current counts
    private readonly Dictionary<string, int> _itemCounts = new();
    private readonly Dictionary<string, int> _cacheSizes = new();
    private readonly object _lockObject = new object();

    // Total counters (across all tables)
    private Counter<long>? _totalOperationsCounter;
    private Counter<long>? _totalCacheHitsCounter;
    private Counter<long>? _totalCacheMissesCounter;

    // Key: tableName_operationType, Value: duration histogram
    private readonly Dictionary<string, Histogram<long>> _durationHistograms = new();
    // Key: tableName_operationType, Value: per-item duration histogram (for batch ops)
    private readonly Dictionary<string, Histogram<double>> _perItemDurationHistograms = new();
    // Key: tableName_operationType, Value: items-per-second histogram (for batch ops)
    private readonly Dictionary<string, Histogram<long>> _itemsPerSecondHistograms = new();

    public DalMetricsCollector(string serviceName = "ExpressRecipe")
    {
        _meter = new Meter($"{serviceName}.DAL", "1.0.0");

        // Create total counters
        _totalOperationsCounter = _meter.CreateCounter<long>(
            "dal.operations.total",
            description: "Total DAL operations across all tables");
        _totalCacheHitsCounter = _meter.CreateCounter<long>(
            "dal.cache.hits.total",
            description: "Total cache hits across all tables");
        _totalCacheMissesCounter = _meter.CreateCounter<long>(
            "dal.cache.misses.total",
            description: "Total cache misses across all tables");
    }

    /// <summary>
    /// Records a DAL operation (Insert, Update, Delete, Query, BulkInsert)
    /// Creates per-table, per-operation metrics: dal.{tableName}.operations.{operationType}
    /// Example: dal.products.operations.query, dal.ingredients.operations.insert
    /// </summary>
    public void RecordOperation(string tableName, string operationType, long count = 1, long durationMs = 0)
    {
        tableName = NormalizeTableName(tableName);
        operationType = operationType?.ToLower() ?? "unknown";
        var counterKey = $"{tableName}_{operationType}";

        lock (_lockObject)
        {
            if (!_tableOperationCounters.ContainsKey(counterKey))
            {
                // Create per-table, per-operation metric: dal.products.operations.query, dal.ingredients.operations.insert, etc.
                _tableOperationCounters[counterKey] = _meter.CreateCounter<long>(
                    $"dal.{tableName.ToLower()}.operations.{operationType}",
                    description: $"{operationType} operations on {tableName}");
            }

            _tableOperationCounters[counterKey].Add(count);
        }

        // Also record total (with table and operation tags for breakdown)
        if (_totalOperationsCounter != null)
        {
            var totalTags = new[] {
                new KeyValuePair<string, object?>("table", (object)tableName),
                new KeyValuePair<string, object?>("operation", (object)operationType)
            };
            _totalOperationsCounter.Add(count, totalTags);
        }
    }

    /// <summary>
    /// Records cache hit
    /// Creates metric: dal.{tableName}.cache.hits
    /// </summary>
    public void RecordCacheHit(string tableName)
    {
        tableName = NormalizeTableName(tableName);

        lock (_lockObject)
        {
            if (!_tableCacheHitCounters.ContainsKey(tableName))
            {
                _tableCacheHitCounters[tableName] = _meter.CreateCounter<long>(
                    $"dal.{tableName.ToLower()}.cache.hits",
                    description: $"Cache hits for {tableName}");
            }
            _tableCacheHitCounters[tableName].Add(1);
        }

        if (_totalCacheHitsCounter != null)
        {
            var tags = new[] { new KeyValuePair<string, object?>("table", (object)tableName) };
            _totalCacheHitsCounter.Add(1, tags);
        }
    }

    /// <summary>
    /// Records cache miss
    /// Creates metric: dal.{tableName}.cache.misses
    /// </summary>
    public void RecordCacheMiss(string tableName)
    {
        tableName = NormalizeTableName(tableName);

        lock (_lockObject)
        {
            if (!_tableCacheMissCounters.ContainsKey(tableName))
            {
                _tableCacheMissCounters[tableName] = _meter.CreateCounter<long>(
                    $"dal.{tableName.ToLower()}.cache.misses",
                    description: $"Cache misses for {tableName}");
            }
            _tableCacheMissCounters[tableName].Add(1);
        }

        if (_totalCacheMissesCounter != null)
        {
            var tags = new[] { new KeyValuePair<string, object?>("table", (object)tableName) };
            _totalCacheMissesCounter.Add(1, tags);
        }
    }

    /// <summary>
    /// Updates the item count for a table
    /// Creates metric: dal.{tableName}.item_count
    /// </summary>
    public void SetItemCount(string tableName, int count)
    {
        tableName = NormalizeTableName(tableName);

        lock (_lockObject)
        {
            _itemCounts[tableName] = count;

            if (!_tableItemCountGauges.ContainsKey(tableName))
            {
                _tableItemCountGauges[tableName] = _meter.CreateObservableGauge(
                    $"dal.{tableName.ToLower()}.item_count",
                    () => GetItemCountMeasurement(tableName),
                    description: $"Current item count in {tableName}");
            }
        }
    }

    /// <summary>
    /// Updates the cache size for a table
    /// Creates metric: dal.{tableName}.cache.size
    /// </summary>
    public void SetCacheSize(string tableName, int size)
    {
        tableName = NormalizeTableName(tableName);

        lock (_lockObject)
        {
            _cacheSizes[tableName] = size;

            if (!_tableCacheSizeGauges.ContainsKey(tableName))
            {
                _tableCacheSizeGauges[tableName] = _meter.CreateObservableGauge(
                    $"dal.{tableName.ToLower()}.cache.size",
                    () => GetCacheSizeMeasurement(tableName),
                    description: $"Current cache size for {tableName}");
            }
        }
    }

    /// <summary>
    /// Records operation duration in milliseconds
    /// Creates histogram: dal.{tableName}.{operationType}.duration_ms
    /// Example: dal.products.getbyid.duration_ms
    /// </summary>
    public void RecordOperationDuration(string tableName, string operationType, long durationMs)
    {
        tableName = NormalizeTableName(tableName);
        operationType = operationType?.ToLower() ?? "unknown";
        var histogramKey = $"{tableName}_{operationType}";

        lock (_lockObject)
        {
            if (!_durationHistograms.ContainsKey(histogramKey))
            {
                _durationHistograms[histogramKey] = _meter.CreateHistogram<long>(
                    $"dal.{tableName.ToLower()}.{operationType}.duration_ms",
                    description: $"Duration in milliseconds for {operationType} operations on {tableName}");
            }

            _durationHistograms[histogramKey].Record(durationMs);
        }
    }

    /// <summary>
    /// Records batch operation with item count, calculates per-item duration and items-per-second
    /// Creates histograms:
    ///   - dal.{tableName}.{operationType}.duration_ms (total)
    ///   - dal.{tableName}.{operationType}.per_item_duration_ms (average per item)
    ///   - dal.{tableName}.{operationType}.items_per_second (throughput)
    /// Example: 5000 items in 100ms = 50ms/item, 50,000 items/sec
    /// </summary>
    public void RecordBatchOperation(string tableName, string operationType, long itemCount, long durationMs)
    {
        if (itemCount <= 0 || durationMs < 0) return;

        tableName = NormalizeTableName(tableName);
        operationType = operationType?.ToLower() ?? "unknown";
        var histogramKey = $"{tableName}_{operationType}";

        // Avoid division by zero
        if (durationMs == 0) durationMs = 1;

        // Calculate metrics
        double perItemDurationMs = (double)durationMs / itemCount;
        long itemsPerSecond = (long)(itemCount / (durationMs / 1000.0));

        lock (_lockObject)
        {
            // Create/record total duration histogram
            if (!_durationHistograms.ContainsKey(histogramKey))
            {
                _durationHistograms[histogramKey] = _meter.CreateHistogram<long>(
                    $"dal.{tableName.ToLower()}.{operationType}.duration_ms",
                    description: $"Duration in milliseconds for {operationType} operations on {tableName}");
            }
            _durationHistograms[histogramKey].Record(durationMs);

            // Create/record per-item duration histogram
            if (!_perItemDurationHistograms.ContainsKey(histogramKey))
            {
                _perItemDurationHistograms[histogramKey] = _meter.CreateHistogram<double>(
                    $"dal.{tableName.ToLower()}.{operationType}.per_item_duration_ms",
                    description: $"Average duration per item (ms) for {operationType} batch operations on {tableName}");
            }
            _perItemDurationHistograms[histogramKey].Record(perItemDurationMs);

            // Create/record items-per-second histogram
            if (!_itemsPerSecondHistograms.ContainsKey(histogramKey))
            {
                _itemsPerSecondHistograms[histogramKey] = _meter.CreateHistogram<long>(
                    $"dal.{tableName.ToLower()}.{operationType}.items_per_second",
                    description: $"Items processed per second for {operationType} batch operations on {tableName}");
            }
            _itemsPerSecondHistograms[histogramKey].Record(itemsPerSecond);
        }

        // Also record to total operation counter for aggregate counts
        RecordOperation(tableName, operationType, itemCount, durationMs);
    }

    private List<Measurement<int>> GetItemCountMeasurement(string tableName)
    {
        lock (_lockObject)
        {
            var count = _itemCounts.ContainsKey(tableName) ? _itemCounts[tableName] : 0;
            return new List<Measurement<int>> { new(count) };
        }
    }

    private List<Measurement<int>> GetCacheSizeMeasurement(string tableName)
    {
        lock (_lockObject)
        {
            var size = _cacheSizes.ContainsKey(tableName) ? _cacheSizes[tableName] : 0;
            return new List<Measurement<int>> { new(size) };
        }
    }

    private string NormalizeTableName(string tableName)
    {
        return tableName?.Trim() ?? "Unknown";
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}
