using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace HighSpeedDAL.Core.Resilience
{
    /// <summary>
    /// Provides retry policies for database operations to handle transient errors
    /// </summary>
    public sealed class DatabaseRetryPolicy
    {
        private readonly ILogger _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        public DatabaseRetryPolicy(ILogger logger, int maxRetryAttempts = 3, int delayMilliseconds = 100)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _retryPolicy = Policy
                .Handle<Exception>(IsTransientError)
                .WaitAndRetryAsync(
                    retryCount: maxRetryAttempts,
                    sleepDurationProvider: retryAttempt => 
                        TimeSpan.FromMilliseconds(delayMilliseconds * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Transient error detected. Retry attempt {RetryCount} after {Delay}ms. Error: {ErrorMessage}",
                            retryCount,
                            timeSpan.TotalMilliseconds,
                            exception.Message);
                    });
        }

        /// <summary>
        /// Executes an async operation with retry logic
        /// </summary>
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            try
            {
                return await _retryPolicy.ExecuteAsync(async (ct) =>
                {
                    return await operation();
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation failed after all retry attempts");
                throw;
            }
        }

        /// <summary>
        /// Determines if an exception is transient and should be retried
        /// </summary>
        private bool IsTransientError(Exception ex)
        {
            // Never retry cancellation
            if (ex is OperationCanceledException || ex is TaskCanceledException)
            {
                return false;
            }

            // SQL Server transient errors
            if (ex is SqlException sqlException)
            {
                foreach (SqlError error in sqlException.Errors)
                {
                    // Check for known transient error codes
                    switch (error.Number)
                    {
                        // Timeout errors
                        case -2: // Timeout
                        case -1: // Connection broken
                        case 1205: // Deadlock victim
                        case 1222: // Lock request timeout
                        
                        // Connection errors
                        case 233: // Connection initialization error
                        case 4060: // Cannot open database
                        case 4221: // Login timeout
                        case 40197: // Service error processing request
                        case 40501: // Service is busy
                        case 40613: // Database unavailable
                        case 49918: // Cannot process request
                        case 49919: // Cannot process create or update request
                        case 49920: // Cannot process request
                        
                        // Throttling errors
                        case 10928: // Resource limit reached
                        case 10929: // Resource limit reached
                        case 40544: // Database quota reached
                        case 40549: // Session terminated (long transaction)
                        case 40550: // Session terminated (lock acquisition)
                        case 40551: // Session terminated (tempdb usage)
                        case 40552: // Session terminated (transaction log)
                        case 40553: // Session terminated (memory usage)
                            return true;
                    }
                }
            }

            // SQLite transient errors
            if (ex.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("unable to open database file", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // General transient errors
            return ex is TimeoutException ||
                ex is InvalidOperationException && ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Factory for creating database retry policies
    /// </summary>
    public sealed class RetryPolicyFactory
    {
        private readonly ILogger _logger;
        private readonly int _maxRetryAttempts;
        private readonly int _delayMilliseconds;

        public RetryPolicyFactory(
            ILogger logger,
            int maxRetryAttempts = 3,
            int delayMilliseconds = 100)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxRetryAttempts = maxRetryAttempts;
            _delayMilliseconds = delayMilliseconds;
        }

        public DatabaseRetryPolicy CreatePolicy()
        {
            return new DatabaseRetryPolicy(_logger, _maxRetryAttempts, _delayMilliseconds);
        }
    }
}
