using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Data.Common
{
    /// <summary>
    /// Tracks batch update statistics including skipped updates due to no changes.
    /// Reports metrics and logs information about update batches.
    /// </summary>
    public sealed class BatchUpdateTracker
    {
        private int _totalAttempted;
        private int _successfulUpdates;
        private int _skippedUpdates;
        private int _failedUpdates;
        private readonly string _batchName;
        private readonly ILogger? _logger;
        private readonly DateTime _startTime;

        public BatchUpdateTracker(string batchName, ILogger? logger = null)
        {
            _batchName = batchName ?? "UnnamedBatch";
            _logger = logger;
            _startTime = CachedDateTimeUtc.UtcNow;
        }

        /// <summary>
        /// Records a successful update.
        /// </summary>
        public void RecordSuccess()
        {
            _totalAttempted++;
            _successfulUpdates++;
        }

        /// <summary>
        /// Records a skipped update (no changes detected).
        /// </summary>
        public void RecordSkipped()
        {
            _totalAttempted++;
            _skippedUpdates++;
        }

        /// <summary>
        /// Records a failed update.
        /// </summary>
        public void RecordFailure()
        {
            _totalAttempted++;
            _failedUpdates++;
        }

        /// <summary>
        /// Records multiple skipped updates at once (common in batch operations).
        /// </summary>
        public void RecordSkippedBatch(int count)
        {
            _totalAttempted += count;
            _skippedUpdates += count;
        }

        /// <summary>
        /// Gets the total number of items attempted.
        /// </summary>
        public int TotalAttempted => _totalAttempted;

        /// <summary>
        /// Gets the total number of successful updates.
        /// </summary>
        public int SuccessfulUpdates => _successfulUpdates;

        /// <summary>
        /// Gets the total number of skipped updates.
        /// </summary>
        public int SkippedUpdates => _skippedUpdates;

        /// <summary>
        /// Gets the total number of failed updates.
        /// </summary>
        public int FailedUpdates => _failedUpdates;

        /// <summary>
        /// Gets the percentage of updates that were skipped.
        /// </summary>
        public double SkipPercentage => _totalAttempted > 0 ? (_skippedUpdates * 100.0) / _totalAttempted : 0;

        /// <summary>
        /// Gets the percentage of updates that succeeded.
        /// </summary>
        public double SuccessPercentage => _totalAttempted > 0 ? (_successfulUpdates * 100.0) / _totalAttempted : 0;

        /// <summary>
        /// Reports the batch statistics to logs and returns a summary.
        /// </summary>
        public BatchUpdateSummary Report()
        {
            TimeSpan elapsed = CachedDateTimeUtc.UtcNow - _startTime;
            BatchUpdateSummary summary = new BatchUpdateSummary
            {
                BatchName = _batchName,
                TotalAttempted = _totalAttempted,
                SuccessfulUpdates = _successfulUpdates,
                SkippedUpdates = _skippedUpdates,
                FailedUpdates = _failedUpdates,
                SkipPercentage = SkipPercentage,
                SuccessPercentage = SuccessPercentage,
                ElapsedMilliseconds = (long)elapsed.TotalMilliseconds
            };

            if (_logger != null)
            {
                if (_skippedUpdates > 0)
                {
                    _logger.LogInformation(
                        "Batch update completed: {BatchName} | Total: {Total} | Updated: {Updated} | Skipped: {Skipped} ({SkipPercent}%) | Failed: {Failed} | Duration: {Duration}ms",
                        _batchName,
                        _totalAttempted,
                        _successfulUpdates,
                        _skippedUpdates,
                        SkipPercentage.ToString("F1"),
                        _failedUpdates,
                        elapsed.TotalMilliseconds.ToString("F0"));
                }
                else
                {
                    _logger.LogInformation(
                        "Batch update completed: {BatchName} | Total: {Total} | Updated: {Updated} | Failed: {Failed} | Duration: {Duration}ms",
                        _batchName,
                        _totalAttempted,
                        _successfulUpdates,
                        _failedUpdates,
                        elapsed.TotalMilliseconds.ToString("F0"));
                }
            }

            return summary;
        }

        /// <summary>
        /// Returns a concise summary string.
        /// </summary>
        public override string ToString()
        {
            return _totalAttempted == 0
                ? $"{_batchName}: No items processed"
                : $"{_batchName}: {_successfulUpdates}/{_totalAttempted} updated, {_skippedUpdates} skipped ({SkipPercentage:F1}%), {_failedUpdates} failed";
        }
    }

    /// <summary>
    /// Summary information from a batch update operation.
    /// </summary>
    public sealed class BatchUpdateSummary
    {
        public string BatchName { get; set; } = string.Empty;
        public int TotalAttempted { get; set; }
        public int SuccessfulUpdates { get; set; }
        public int SkippedUpdates { get; set; }
        public int FailedUpdates { get; set; }
        public double SkipPercentage { get; set; }
        public double SuccessPercentage { get; set; }
        public long ElapsedMilliseconds { get; set; }

        public override string ToString()
        {
            return $"Batch: {BatchName}, Total: {TotalAttempted}, Updated: {SuccessfulUpdates}, Skipped: {SkippedUpdates} ({SkipPercentage:F1}%), Failed: {FailedUpdates}, Duration: {ElapsedMilliseconds}ms";
        }
    }
}
