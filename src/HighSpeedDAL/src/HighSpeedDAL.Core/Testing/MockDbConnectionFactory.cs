using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Base;
using HighSpeedDAL.Core.Interfaces;

namespace HighSpeedDAL.Core.Testing;

/// <summary>
/// Mock database connection factory for unit testing
/// </summary>
public class MockDbConnectionFactory : IDbConnectionFactory
{
    private readonly InMemoryDataStore _dataStore;

    public MockDbConnectionFactory(InMemoryDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    /// <summary>
    /// Creates a mock database connection
    /// </summary>
    public Task<IDbConnection> CreateConnectionAsync(
        string connectionString,
        DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        var connection = new MockDatabaseConnection(_dataStore);
        connection.Open();
        return Task.FromResult<IDbConnection>(connection);
    }

    /// <summary>
    /// Gets the in-memory data store for test setup and verification
    /// </summary>
    public InMemoryDataStore GetDataStore() => _dataStore;
}
