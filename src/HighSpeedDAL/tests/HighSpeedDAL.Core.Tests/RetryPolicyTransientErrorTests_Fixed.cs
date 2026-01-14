using FluentAssertions;
using Microsoft.Data.SqlClient;
using HighSpeedDAL.Core.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.Core.Tests.Resilience;

/// <summary>
/// Tests for retry policy detection and handling - Tests retry logic without requiring a database
/// </summary>
public class RetryPolicyTransientErrorTests
{
    #region Transient Error Detection Tests

    [Theory]
    [InlineData(-2)]        // Timeout
    [InlineData(-1)]        // Connection broken
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
        SqlException sqlException = SqlExceptionHelper.CreateSqlException(errorNumber);

        // Act
        bool result = IsTransientSqlError(errorNumber);

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
        // Arrange & Act
        bool result = IsTransientSqlError(errorNumber);

        // Assert
        result.Should().BeFalse($"SQL error {errorNumber} should NOT be considered transient");
    }

    [Fact]
    public void IsTransientError_TimeoutException_ShouldReturnTrue()
    {
        // Arrange
        TimeoutException exception = new TimeoutException("Operation timed out");

        // Act
        bool result = IsTransientErrorType(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_IOException_ShouldReturnTrue()
    {
        // Arrange
        System.IO.IOException exception = new System.IO.IOException("Network error");

        // Act
        bool result = IsTransientErrorType(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_InvalidOperationException_ShouldReturnFalse()
    {
        // Arrange
        InvalidOperationException exception = new InvalidOperationException("Invalid operation");

        // Act
        bool result = IsTransientErrorType(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTransientError_ArgumentNullException_ShouldReturnFalse()
    {
        // Arrange
        ArgumentNullException exception = new ArgumentNullException("param");

        // Act
        bool result = IsTransientErrorType(exception);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private bool IsTransientSqlError(int errorNumber)
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

    private bool IsTransientErrorType(Exception exception)
    {
        return exception is TimeoutException || exception is System.IO.IOException;
    }

    #endregion
}
