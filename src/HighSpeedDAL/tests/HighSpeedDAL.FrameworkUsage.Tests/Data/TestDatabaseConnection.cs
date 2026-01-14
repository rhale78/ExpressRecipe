using HighSpeedDAL.Core.Base;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace HighSpeedDAL.FrameworkUsage.Tests.Data;

/// <summary>
/// Test database connection class for framework usage tests.
/// Uses SQLite in-memory database for fast, isolated testing.
/// </summary>
public class TestDatabaseConnection : DatabaseConnectionBase
{
    /// <summary>
    /// Constructor for testing with direct SQLite connection.
    /// Creates minimal configuration with the connection string.
    /// </summary>
    public TestDatabaseConnection(SqliteConnection connection)
        : base(CreateConfiguration(connection?.ConnectionString ?? throw new ArgumentNullException(nameof(connection))), 
               CreateNullLogger())
    {
    }

    public override DatabaseProvider Provider => DatabaseProvider.Sqlite;

    protected override string GetConnectionStringKey()
    {
        return "TestDatabase";
    }

    private static IConfiguration CreateConfiguration(string connectionString)
    {
        var configData = new Dictionary<string, string?>
        {
            { "ConnectionStrings:TestDatabase", connectionString }
        };

        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(configData);

        return builder.Build();
    }

    private static ILogger<TestDatabaseConnection> CreateNullLogger()
    {
        using var loggerFactory = LoggerFactory.Create(builder => { });
        return loggerFactory.CreateLogger<TestDatabaseConnection>();
    }
}
