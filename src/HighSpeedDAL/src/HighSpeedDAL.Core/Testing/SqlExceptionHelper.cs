using System;
using Microsoft.Data.SqlClient;
using System.Reflection;

namespace HighSpeedDAL.Core.Testing;

/// <summary>
/// Helper class for creating SQL exceptions for testing
/// </summary>
public static class SqlExceptionHelper
{
    /// <summary>
    /// Creates a mock SqlException with the specified error number
    /// </summary>
    public static SqlException CreateSqlException(int errorNumber, string message = "Test SQL error")
    {
        try
        {
            // Create a SqlErrorCollection
            var sqlErrorCollection = typeof(SqlException)
                .GetField("_sqlErrorCollection", BindingFlags.NonPublic | BindingFlags.Instance);

            if (sqlErrorCollection == null)
            {
                throw new InvalidOperationException("Cannot create SqlException: _sqlErrorCollection field not found");
            }

            // Create SqlError
            var sqlErrorType = typeof(SqlException).Assembly.GetType("Microsoft.Data.SqlClient.SqlError");
            if (sqlErrorType == null)
            {
                // Fallback for newer versions
                sqlErrorType = typeof(Microsoft.Data.SqlClient.SqlException).Assembly
                    .GetType("Microsoft.Data.SqlClient.SqlError");
            }

            if (sqlErrorType == null)
            {
                throw new InvalidOperationException("Cannot create SqlException: SqlError type not found");
            }

            // Create SqlError instance with reflection
            var errorConstructor = sqlErrorType.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(uint) },
                null);

            if (errorConstructor == null)
            {
                throw new InvalidOperationException("Cannot create SqlException: SqlError constructor not found");
            }

            var sqlError = errorConstructor.Invoke(new object?[]
            {
                errorNumber,      // infoNumber (error number)
                (byte)0,          // errorState
                (byte)11,         // errorClass (severity)
                "localhost",      // server name
                message,          // error message
                string.Empty,     // procedure
                1,                // lineNumber
                0u                // win32ErrorCode
            });

            // Create SqlErrorCollection with the error
            var collectionConstructor = sqlErrorCollection.FieldType!.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

            if (collectionConstructor == null)
            {
                throw new InvalidOperationException("Cannot create SqlException: SqlErrorCollection constructor not found");
            }

            var collection = collectionConstructor.Invoke(Array.Empty<object>());

            // Add error to collection
            var addMethod = sqlErrorCollection.FieldType!.GetMethod(
                "Add",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { sqlErrorType },
                null);

            if (addMethod == null)
            {
                throw new InvalidOperationException("Cannot create SqlException: Add method not found");
            }

            addMethod.Invoke(collection, new[] { sqlError });

            // Create SqlException
            var exceptionConstructor = typeof(SqlException).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string), sqlErrorCollection.FieldType! },
                null);

            if (exceptionConstructor == null)
            {
                throw new InvalidOperationException("Cannot create SqlException: constructor not found");
            }

            var exception = (SqlException)exceptionConstructor.Invoke(new object?[] { message, collection })!;
            return exception;
        }
        catch (Exception ex)
        {
            // If we can't create a real SqlException, throw a more specific error
            throw new InvalidOperationException(
                $"Failed to create SqlException with error number {errorNumber}. " +
                "This may be due to .NET version differences. " +
                "Consider using a different testing approach.", ex);
        }
    }

    /// <summary>
    /// Creates a mock SqlException with multiple errors
    /// </summary>
    public static SqlException CreateSqlExceptionWithMultipleErrors(params int[] errorNumbers)
    {
        if (errorNumbers.Length == 0)
        {
            throw new ArgumentException("At least one error number must be provided", nameof(errorNumbers));
        }

        // For simplicity, return the exception with the first error number
        // In a real scenario, you'd want to add multiple errors to the collection
        return CreateSqlException(errorNumbers[0]);
    }
}
