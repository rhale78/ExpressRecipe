using System;

namespace HighSpeedDAL.Core.Testing;

/// <summary>
/// Exception thrown when an assertion fails during unit testing
/// </summary>
public class AssertionException : Exception
{
    /// <summary>
    /// Creates a new assertion exception
    /// </summary>
    public AssertionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new assertion exception with an inner exception
    /// </summary>
    public AssertionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Base class for unit tests that test the framework without a database
/// </summary>
public abstract class UnitTestBase : IDisposable
{
    protected InMemoryDataStore DataStore { get; }
    protected MockDbConnectionFactory ConnectionFactory { get; }
    protected MockDatabaseConnection Connection { get; }

    protected UnitTestBase()
    {
        DataStore = new InMemoryDataStore();
        ConnectionFactory = new MockDbConnectionFactory(DataStore);
        Connection = new MockDatabaseConnection(DataStore);
    }

    /// <summary>
    /// Asserts that a SQL query was executed containing the specified text
    /// </summary>
    protected void AssertQueryExecuted(string sqlPattern)
    {
        var executed = Connection.GetExecutedQueries();
        var found = executed.Any(q => q.sql.Contains(sqlPattern, StringComparison.OrdinalIgnoreCase));
        if (!found)
        {
            throw new AssertionException(
                $"Expected query containing '{sqlPattern}' to be executed. " +
                $"Executed: {string.Join(", ", executed.Select(q => q.sql))}");
        }
    }

    /// <summary>
    /// Asserts that the number of executed queries matches the expected count
    /// </summary>
    protected void AssertQueryCount(int expectedCount)
    {
        var executed = Connection.GetExecutedQueries();
        if (executed.Count != expectedCount)
        {
            throw new AssertionException(
                $"Expected {expectedCount} queries to be executed but got {executed.Count}");
        }
    }

    /// <summary>
    /// Clears all executed queries from the log
    /// </summary>
    protected void ClearQueryLog()
    {
        Connection.ClearExecutedQueries();
    }

    /// <summary>
    /// Gets all executed queries for inspection
    /// </summary>
    protected System.Collections.Generic.IReadOnlyList<(string sql, System.Collections.Generic.Dictionary<string, object?> parameters)> GetExecutedQueries()
    {
        return Connection.GetExecutedQueries();
    }

    /// <summary>
    /// Asserts that a table exists in the data store
    /// </summary>
    protected void AssertTableExists(string tableName)
    {
        var tables = DataStore.GetTableNames();
        if (!tables.Contains(tableName))
        {
            throw new AssertionException(
                $"Table '{tableName}' was not found. Available tables: {string.Join(", ", tables)}");
        }
    }

    /// <summary>
    /// Asserts that a table has a specific number of rows
    /// </summary>
    protected void AssertTableRowCount(string tableName, int expectedCount)
    {
        int actualCount = DataStore.Count(tableName);
        if (actualCount != expectedCount)
        {
            throw new AssertionException(
                $"Expected {expectedCount} rows in table '{tableName}' but got {actualCount}");
        }
    }

    /// <summary>
    /// Asserts that a row exists in a table matching the predicate
    /// </summary>
    protected void AssertRowExists(string tableName, Func<System.Collections.Generic.Dictionary<string, object?>, bool> predicate)
    {
        if (!DataStore.Exists(tableName, predicate))
        {
            throw new AssertionException(
                $"Expected a row in table '{tableName}' matching the predicate but none was found");
        }
    }

    public virtual void Dispose()
    {
        DataStore.Clear();
        Connection?.Dispose();
    }
}
