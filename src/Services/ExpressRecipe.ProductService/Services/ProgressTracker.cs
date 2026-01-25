using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Services
{
    /// <summary>
    /// Tracks processing progress and provides metrics like items/second, ETA, and percentage.
    /// Logs progress at regular intervals with both in-memory and SQL operation visibility.
    /// </summary>
    public class ProgressTracker
    {
        private readonly ILogger _logger;
        private readonly int _totalItems;
        private readonly Stopwatch _stopwatch;
        private int _processedItems;
        private int _successCount;
        private int _failureCount;
        private DateTime _lastLogTime;
        private int _lastLoggedCount;
        private const int LOG_INTERVAL_MS = 5000; // Log every 5 seconds

        public ProgressTracker(ILogger logger, int totalItems)
        {
            _logger = logger;
            _totalItems = totalItems;
            _stopwatch = Stopwatch.StartNew();
            _processedItems = 0;
            _successCount = 0;
            _failureCount = 0;
            _lastLogTime = DateTime.UtcNow;
            _lastLoggedCount = 0;
        }

        /// <summary>
        /// Update progress and log if enough time has passed
        /// </summary>
        public void Update(int successCount, int failureCount)
        {
            _successCount = successCount;
            _failureCount = failureCount;
            _processedItems = successCount + failureCount;

            DateTime now = DateTime.UtcNow;
            if ((now - _lastLogTime).TotalMilliseconds >= LOG_INTERVAL_MS)
            {
                LogProgress();
                _lastLogTime = now;
            }
        }

        /// <summary>
        /// Log current progress with metrics
        /// </summary>
        private void LogProgress()
        {
            if (_totalItems == 0)
            {
                return;
            }

            TimeSpan elapsed = _stopwatch.Elapsed;
            double itemsPerSecond = elapsed.TotalSeconds > 0
                ? _processedItems / elapsed.TotalSeconds
                : 0;

            var percentComplete = (_processedItems / (double)_totalItems) * 100;

            var remainingItems = _totalItems - _processedItems;
            var estimatedSecondsRemaining = itemsPerSecond > 0
                ? remainingItems / itemsPerSecond
                : 0;

            TimeSpan eta = estimatedSecondsRemaining > 0
                ? TimeSpan.FromSeconds(estimatedSecondsRemaining)
                : TimeSpan.Zero;

            var itemsInInterval = _processedItems - _lastLoggedCount;
            var intervalSeconds = elapsed.TotalSeconds;

            _logger.LogInformation(
                "[PROGRESS] {Percentage:F1}% complete | Items: {Processed}/{Total} | " +
                "Speed: {ItemsPerSecond:F1} items/sec | ETA: {EtaHours}h {EtaMinutes}m {EtaSeconds}s | " +
                "Success: {Success} | Failed: {Failed} | Elapsed: {Elapsed}",
                percentComplete,
                _processedItems,
                _totalItems,
                itemsPerSecond,
                (int)eta.TotalHours,
                eta.Minutes,
                eta.Seconds,
                _successCount,
                _failureCount,
                FormatTimespan(elapsed)
            );

            _lastLoggedCount = _processedItems;
        }

        /// <summary>
        /// Log completion summary with final metrics
        /// </summary>
        public void LogCompletion()
        {
            _stopwatch.Stop();
            TimeSpan elapsed = _stopwatch.Elapsed;
            var itemsPerSecond = elapsed.TotalSeconds > 0
                ? _processedItems / elapsed.TotalSeconds
                : 0;

            _logger.LogInformation(
                "[COMPLETION] Processing finished | Total items: {Total} | " +
                "Success: {Success} | Failed: {Failed} | " +
                "Total time: {TotalTime} | Average speed: {ItemsPerSecond:F1} items/sec",
                _totalItems,
                _successCount,
                _failureCount,
                FormatTimespan(elapsed),
                itemsPerSecond
            );
        }

        private static string FormatTimespan(TimeSpan ts)
        {
            return ts.TotalHours >= 1
                ? $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s"
                : ts.TotalMinutes >= 1 ? $"{ts.Minutes}m {ts.Seconds}s" : $"{ts.Seconds}s";
        }
    }
}
