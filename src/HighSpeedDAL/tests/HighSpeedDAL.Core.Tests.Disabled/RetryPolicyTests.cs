using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;

namespace HighSpeedDAL.Common.Tests;

/// <summary>
/// Comprehensive tests for database retry policies
/// </summary>
public class RetryPolicyTests
{
    private readonly Mock<ILogger> _mockLogger;

    public RetryPolicyTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    #region Transient Error Detection Tests

    [Theory]
    [InlineData(-2)]        // Timeout
    [InlineData(1204)]      // Deadlock
    [InlineData(1205)]      // Deadlock victim
    [InlineData(1222)]      // Lock timeout
    [InlineData(2601)]      // Duplicate key
    [InlineData(2627)]      // Constraint violation
    [InlineData(8645)]      // Timeout waiting for memory
    [InlineData(8651)]      // Low memory
    [InlineData(40197)]     // Network error
    [InlineData(40501)]     // Service busy
    [InlineData(40540)]     // Connection lost
    [InlineData(40613)]     // Database unavailable
    [InlineData(49918)]     // Cannot process request
    [InlineData(49919)]     // Too many create/update requests
    [InlineData(49920)]     // Too many operations
    public void IsTransientError_KnownTransientSqlError_ShouldReturnTrue(int errorNumber)
    {
        // Arrange
        SqlException sqlException = CreateSqlException(errorNumber);

        // Act
        bool result = RetryPolicyHelper.IsTransientError(sqlException);

        // Assert
        result.Should().BeTrue($"SQL error {errorNumber} should be considered transient");
    }

    [Theory]
    [InlineData(207)]       // Invalid column name
    [InlineData(208)]       // Invalid object name
    [InlineData(515)]       // Cannot insert null
    [InlineData(547)]       // FK constraint
    [InlineData(2714)]      // Object already exists
    public void IsTransientError_NonTransientSqlError_ShouldReturnFalse(int errorNumber)
    {
        // Arrange
        SqlException sqlException = CreateSqlException(errorNumber);

        // Act
        bool result = RetryPolicyHelper.IsTransientError(sqlException);

        // Assert
        result.Should().BeFalse($"SQL error {errorNumber} should NOT be considered transient");
    }

    [Fact]
    public void IsTransientError_TimeoutException_ShouldReturnTrue()
    {
        // Arrange
        TimeoutException exception = new TimeoutException("Operation timed out");

        // Act
        bool result = RetryPolicyHelper.IsTransientError(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_IOException_ShouldReturnTrue()
    {
        // Arrange
        System.IO.IOException exception = new System.IO.IOException("Network error");

        // Act
        bool result = RetryPolicyHelper.IsTransientError(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_InvalidOperationException_ShouldReturnFalse()
    {
        // Arrange
        InvalidOperationException exception = new InvalidOperationException("Invalid operation");

        // Act
        bool result = RetryPolicyHelper.IsTransientError(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTransientError_ArgumentNullException_ShouldReturnFalse()
    {
        // Arrange
        ArgumentNullException exception = new ArgumentNullException("param");

        // Act
        bool result = RetryPolicyHelper.IsTransientError(exception);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Retry Policy Execution Tests

    [Fact]
    public async Task ExecuteWithRetry_SuccessfulOperation_ShouldExecuteOnce()
    {
        // Arrange
        int executionCount = 0;
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(_mockLogger.Object);

        // Act
        string result = await policy.ExecuteAsync(async (ct) =>
        {
            executionCount++;
            await Task.Delay(10, ct);
            return "Success";
        });

        // Assert
        result.Should().Be("Success");
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetry_TransientErrorThenSuccess_ShouldRetryAndSucceed()
    {
        // Arrange
        int executionCount = 0;
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(_mockLogger.Object);

        // Act
        string result = await policy.ExecuteAsync(async (ct) =>
        {
            executionCount++;
            await Task.Delay(10, ct);
            
            if (executionCount < 3)
            {
                throw CreateSqlException(1204); // Deadlock - transient
            }
            
            return "Success after retries";
        });

        // Assert
        result.Should().Be("Success after retries");
        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteWithRetry_PersistentTransientError_ShouldExhaustRetriesAndThrow()
    {
        // Arrange
        int executionCount = 0;
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(_mockLogger.Object, maxRetryAttempts: 3);

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(async (ct) =>
        {
            executionCount++;
            await Task.Delay(10, ct);
            throw CreateSqlException(1204); // Always throw transient error
        });

        // Assert
        await act.Should().ThrowAsync<SqlException>();
        executionCount.Should().Be(4); // Initial attempt + 3 retries
    }

    [Fact]
    public async Task ExecuteWithRetry_NonTransientError_ShouldNotRetry()
    {
        // Arrange
        int executionCount = 0;
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(_mockLogger.Object);

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(async (ct) =>
        {
            executionCount++;
            await Task.Delay(10, ct);
            throw CreateSqlException(208); // Invalid object - NOT transient
        });

        // Assert
        await act.Should().ThrowAsync<SqlException>();
        executionCount.Should().Be(1, "should not retry non-transient errors");
    }

    [Fact]
    public async Task ExecuteWithRetry_TimeoutError_ShouldRetry()
    {
        // Arrange
        int executionCount = 0;
        ResiliencePipeline<int> policy = RetryPolicyHelper.CreateRetryPolicy<int>(_mockLogger.Object);

        // Act
        int result = await policy.ExecuteAsync(async (ct) =>
        {
            executionCount++;
            await Task.Delay(10, ct);
            
            if (executionCount < 2)
            {
                throw new TimeoutException("Database timeout");
            }
            
            return 42;
        });

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2);
    }

    #endregion

    #region Backoff Strategy Tests

    [Fact]
    public async Task ExecuteWithRetry_ExponentialBackoff_ShouldIncreaseDelayBetweenRetries()
    {
        // Arrange
        List<DateTime> attemptTimes = new List<DateTime>();
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(
            _mockLogger.Object,
            maxRetryAttempts: 3,
            initialDelayMilliseconds: 100);

        // Act
        try
        {
            await policy.ExecuteAsync(async (ct) =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                await Task.Delay(10, ct);
                throw CreateSqlException(-2); // Timeout
            });
        }
        catch (SqlException)
        {
            // Expected
        }

        // Assert
        attemptTimes.Should().HaveCount(4); // Initial + 3 retries
        
        // Verify delays are increasing (exponential backoff with jitter)
        TimeSpan delay1 = attemptTimes[1] - attemptTimes[0];
        TimeSpan delay2 = attemptTimes[2] - attemptTimes[1];
        TimeSpan delay3 = attemptTimes[3] - attemptTimes[2];

        // Each delay should be greater than the previous (accounting for jitter)
        delay1.TotalMilliseconds.Should().BeGreaterThan(50); // At least some delay
        delay2.TotalMilliseconds.Should().BeGreaterThan(delay1.TotalMilliseconds * 0.8); // Allow for jitter
        delay3.TotalMilliseconds.Should().BeGreaterThan(delay2.TotalMilliseconds * 0.8);
    }

    [Fact]
    public async Task ExecuteWithRetry_CustomDelaySettings_ShouldRespectConfiguration()
    {
        // Arrange
        int executionCount = 0;
        DateTime startTime = DateTime.UtcNow;
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(
            _mockLogger.Object,
            maxRetryAttempts: 2,
            initialDelayMilliseconds: 500,
            maxDelayMilliseconds: 1000);

        // Act
        try
        {
            await policy.ExecuteAsync(async (ct) =>
            {
                executionCount++;
                await Task.Delay(10, ct);
                throw CreateSqlException(1204); // Deadlock
            });
        }
        catch (SqlException)
        {
            // Expected
        }

        TimeSpan totalTime = DateTime.UtcNow - startTime;

        // Assert
        executionCount.Should().Be(3); // Initial + 2 retries
        totalTime.TotalMilliseconds.Should().BeGreaterThan(500); // At least initial delay
    }

    #endregion

    #region Concurrent Execution Tests

    [Fact]
    public async Task ExecuteWithRetry_ConcurrentOperations_ShouldAllRetryIndependently()
    {
        // Arrange
        int[] executionCounts = new int[10];
        ResiliencePipeline<int> policy = RetryPolicyHelper.CreateRetryPolicy<int>(_mockLogger.Object);

        // Act
        List<Task<int>> tasks = new List<Task<int>>();
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks.Add(policy.ExecuteAsync(async (ct) =>
            {
                executionCounts[index]++;
                await Task.Delay(10, ct);
                
                if (executionCounts[index] < 2)
                {
                    throw CreateSqlException(40501); // Service busy
                }
                
                return index;
            }));
        }

        int[] results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        for (int i = 0; i < 10; i++)
        {
            results[i].Should().Be(i);
            executionCounts[i].Should().Be(2); // Each should have retried once
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExecuteWithRetry_NullLogger_ShouldNotThrow()
    {
        // Arrange
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(null!);

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(async (ct) =>
        {
            await Task.Delay(10, ct);
            return "Success";
        });

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteWithRetry_ZeroRetries_ShouldExecuteOnceAndThrow()
    {
        // Arrange
        int executionCount = 0;
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(
            _mockLogger.Object,
            maxRetryAttempts: 0);

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(async (ct) =>
        {
            executionCount++;
            await Task.Delay(10, ct);
            throw CreateSqlException(1204);
        });

        // Assert
        await act.Should().ThrowAsync<SqlException>();
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetry_MixedErrors_ShouldOnlyRetryTransient()
    {
        // Arrange
        int executionCount = 0;
        ResiliencePipeline<string> policy = RetryPolicyHelper.CreateRetryPolicy<string>(_mockLogger.Object);

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(async (ct) =>
        {
            executionCount++;
            await Task.Delay(10, ct);
            
            // First attempt: transient error (will retry)
            if (executionCount == 1)
            {
                throw CreateSqlException(1204); // Deadlock
            }
            
            // Second attempt: non-transient error (will NOT retry)
            throw CreateSqlException(208); // Invalid object
        });

        // Assert
        await act.Should().ThrowAsync<SqlException>();
        executionCount.Should().Be(2, "should retry once for transient, then fail on non-transient");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a SqlException with the specified error number using reflection
    /// </summary>
    private static SqlException CreateSqlException(int errorNumber)
    {
        // SqlException cannot be directly instantiated, so we use reflection
        SqlErrorCollection collection = CreateSqlErrorCollection();
        SqlError error = CreateSqlError(errorNumber);
        
        // Add error to collection using reflection
        System.Reflection.MethodInfo? addMethod = collection.GetType()
            .GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        addMethod?.Invoke(collection, new object[] { error });

        // Create SqlException using reflection
        System.Reflection.ConstructorInfo? ctor = typeof(SqlException)
            .GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid) },
                null);

        SqlException exception = (SqlException)ctor!.Invoke(new object?[] 
        { 
            $"SQL Error {errorNumber}", 
            collection, 
            null, 
            Guid.NewGuid() 
        });

        return exception;
    }

    private static SqlErrorCollection CreateSqlErrorCollection()
    {
        System.Reflection.ConstructorInfo? ctor = typeof(SqlErrorCollection)
            .GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

        return (SqlErrorCollection)ctor!.Invoke(null);
    }

    private static SqlError CreateSqlError(int errorNumber)
    {
        System.Reflection.ConstructorInfo? ctor = typeof(SqlError)
            .GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] 
                { 
                    typeof(int), typeof(byte), typeof(byte), typeof(string),
                    typeof(string), typeof(string), typeof(int), typeof(uint), typeof(Exception)
                },
                null);

        return (SqlError)ctor!.Invoke(new object?[] 
        { 
            errorNumber,    // number
            (byte)0,        // state
            (byte)0,        // @class
            "server",       // server
            "error message",// message
            "procedure",    // procedure
            0,              // line number
            (uint)0,        // win32 error code
            null            // exception
        });
    }

    #endregion
}

/// <summary>
/// Helper class for retry policy operations
/// </summary>
public static class RetryPolicyHelper
{
    /// <summary>
    /// Determines if an exception represents a transient error
    /// </summary>
    public static bool IsTransientError(Exception exception)
    {
        if (exception is SqlException sqlException)
        {
            foreach (SqlError error in sqlException.Errors)
            {
                if (IsTransientSqlError(error.Number))
                {
                    return true;
                }
            }
            return false;
        }

        return exception is TimeoutException || exception is System.IO.IOException;
    }

    /// <summary>
    /// Checks if a SQL error number represents a transient error
    /// </summary>
    private static bool IsTransientSqlError(int errorNumber)
    {
        // Comprehensive list of transient SQL Server error codes
        int[] transientErrors = new[]
        {
            -2,     // Timeout
            -1,     // Connection broken
            1204,   // Deadlock victim
            1205,   // Deadlock
            1222,   // Lock timeout
            2601,   // Duplicate key
            2627,   // Constraint violation
            8645,   // Timeout waiting for memory
            8651,   // Low memory
            40197,  // Network error
            40501,  // Service busy
            40540,  // Connection lost
            40613,  // Database unavailable
            49918,  // Cannot process request
            49919,  // Too many create/update requests
            49920   // Too many operations
        };

        return transientErrors.Contains(errorNumber);
    }

    /// <summary>
    /// Creates a retry policy for database operations
    /// </summary>
    public static ResiliencePipeline<T> CreateRetryPolicy<T>(
        ILogger? logger,
        int maxRetryAttempts = 3,
        int initialDelayMilliseconds = 100,
        int maxDelayMilliseconds = 5000)
    {
        return new ResiliencePipelineBuilder<T>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<T>
            {
                MaxRetryAttempts = maxRetryAttempts,
                Delay = TimeSpan.FromMilliseconds(initialDelayMilliseconds),
                BackoffType = Polly.DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = TimeSpan.FromMilliseconds(maxDelayMilliseconds),
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<SqlException>(ex => IsTransientError(ex))
                    .Handle<TimeoutException>()
                    .Handle<System.IO.IOException>(),
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        "Retry attempt {AttemptNumber} after {Delay}ms due to: {Exception}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
