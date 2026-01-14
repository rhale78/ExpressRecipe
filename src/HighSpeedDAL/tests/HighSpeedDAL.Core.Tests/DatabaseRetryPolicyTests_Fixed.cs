using Microsoft.Data.SqlClient;
using FluentAssertions;
using HighSpeedDAL.Core.Resilience;
using HighSpeedDAL.Core.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.Core.Tests.Resilience;

/// <summary>
/// Unit tests for DatabaseRetryPolicy - Tests retry logic without requiring a real database
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
                throw SqlExceptionHelper.CreateSqlException(1205, "Deadlock victim");
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
    public async Task ExecuteAsync_TimeoutError_RetriesAndSucceeds()
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
            return 42;
        }

        // Act
        int result = await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2, "should retry once on timeout");
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionError_RetriesAndSucceeds()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw SqlExceptionHelper.CreateSqlException(40197, "Service error processing request");
            }
            return 42;
        }

        // Act
        int result = await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2, "should retry once on connection error");
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsRetryAttempts_ThrowsException()
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            await Task.CompletedTask;
            throw SqlExceptionHelper.CreateSqlException(1205, "Deadlock victim");
        }

        // Act
        Func<Task> act = async () => await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        await act.Should().ThrowAsync<SqlException>();
        executionCount.Should().Be(4, "should attempt 1 initial + 3 retries");
    }

    [Theory]
    [InlineData(1205)] // Deadlock victim
    [InlineData(1222)] // Lock request timeout
    [InlineData(-2)]   // Timeout
    [InlineData(40197)] // Service error
    [InlineData(40501)] // Service busy
    public async Task ExecuteAsync_TransientErrors_AllRetried(int errorNumber)
    {
        // Arrange
        int executionCount = 0;
        async Task<int> Operation()
        {
            executionCount++;
            if (executionCount < 2)
            {
                throw SqlExceptionHelper.CreateSqlException(errorNumber, "Transient error");
            }
            return 42;
        }

        // Act
        int result = await _retryPolicy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(2, "should retry on transient error");
    }
}
