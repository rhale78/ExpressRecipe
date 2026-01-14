using Microsoft.Data.SqlClient;
using FluentAssertions;
using HighSpeedDAL.Core.Resilience;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.Core.Tests.Resilience;

/// <summary>
/// Unit tests for DatabaseRetryPolicy
/// </summary>
public class DatabaseRetryPolicyTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly DatabaseRetryPolicy _retryPolicy;

    public DatabaseRetryPolicyTests()
    {
        _mockLogger = new Mock<ILogger>();
        _retryPolicy = new DatabaseRetryPolicy(
            _mockLogger.Object,
            maxRetryAttempts: 3,
            delayMilliseconds: 10); // Fast retry for testing
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_ExecutesOnce()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            await Task.CompletedTask;
            return 42;
        }

        // Act
        int result = await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(1, "operation should execute only once when successful");
    }

    [Fact]
    public async Task ExecuteAsync_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            if (executionCount < 3)
            {
                // Simulate transient error (deadlock)
                throw new SqlException();
            }
            await Task.CompletedTask;
            return 42;
        }

        // Act
        int result = await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(3, "should retry twice then succeed");
    }

    [Fact]
    public async Task ExecuteAsync_PermanentError_FailsImmediately()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            await Task.CompletedTask;
            throw new InvalidOperationException("Permanent error");
        }

        // Act
        Func<Task> act = async () => await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        executionCount.Should().Be(1, "should not retry permanent errors");
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutError_RetríesAndSucceeds()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new TimeoutException("Connection timeout");
            }
            await Task.CompletedTask;
            return 42;
        }

        // Act
        int result = await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2, "should retry timeout error once");
    }

    [Fact]
    public async Task ExecuteAsync_AllRetriesFail_ThrowsLastException()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            await Task.CompletedTask;
            throw new TimeoutException($"Timeout attempt {executionCount}");
        }

        // Act
        Func<Task> act = async () => await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*attempt 4*"); // 1 initial + 3 retries = 4 total
        executionCount.Should().Be(4, "should execute initial attempt plus 3 retries");
    }

    [Fact]
    public async Task ExecuteAsync_SqlConnectionError_Retries()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            if (executionCount < 2)
            {
                // Simulate connection initialization error
                throw new InvalidOperationException("connection initialization error occurred");
            }
            await Task.CompletedTask;
            return 42;
        }

        // Act
        int result = await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2, "should retry connection error once");
    }

    [Fact]
    public async Task ExecuteAsync_SqliteLockError_Retries()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            if (executionCount < 2)
            {
                throw new Exception("database is locked");
            }
            await Task.CompletedTask;
            return 42;
        }

        // Act
        int result = await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2, "should retry SQLite lock error once");
    }

    [Fact]
    public async Task ExecuteAsync_ExponentialBackoff_IncreasesDelay()
    {
        // Arrange
        DatabaseRetryPolicy slowRetryPolicy = new DatabaseRetryPolicy(
            _mockLogger.Object,
            maxRetryAttempts: 3,
            delayMilliseconds: 100);

        int executionCount = 0;
        List<DateTime> executionTimes = new List<DateTime>();

        async Task<int> Operation()
        {
            executionTimes.Add(DateTime.UtcNow);
            executionCount++;
            await Task.CompletedTask;
            throw new TimeoutException("Timeout");
        }

        // Act
        try
        {
            int result = await slowRetryPolicy.ExecuteAsync(Operation);
        }
        catch (TimeoutException)
        {
            // Expected
        }

        // Assert
        executionCount.Should().Be(4); // 1 initial + 3 retries

        // Check exponential backoff (100ms, 200ms, 400ms)
        TimeSpan firstDelay = executionTimes[1] - executionTimes[0];
        TimeSpan secondDelay = executionTimes[2] - executionTimes[1];
        TimeSpan thirdDelay = executionTimes[3] - executionTimes[2];

        firstDelay.TotalMilliseconds.Should().BeGreaterOrEqualTo(100);
        secondDelay.TotalMilliseconds.Should().BeGreaterOrEqualTo(200);
        thirdDelay.TotalMilliseconds.Should().BeGreaterOrEqualTo(400);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        async Task<int> Operation()
        {
            await Task.CompletedTask;
            return 42;
        }

        // Act
        Func<Task> act = async () => await _retryPolicy.ExecuteAsync(Operation, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

/// <summary>
/// Unit tests for RetryPolicyFactory
/// </summary>
public class RetryPolicyFactoryTests
{
    [Fact]
    public void CreatePolicy_ValidParameters_ReturnsPolicy()
    {
        // Arrange
        Mock<ILogger> mockLogger = new Mock<ILogger>();
        RetryPolicyFactory factory = new RetryPolicyFactory(
            mockLogger.Object,
            maxRetryAttempts: 5,
            delayMilliseconds: 50);

        // Act
        DatabaseRetryPolicy policy = factory.CreatePolicy();

        // Assert
        policy.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new RetryPolicyFactory(null!, maxRetryAttempts: 3, delayMilliseconds: 100);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
