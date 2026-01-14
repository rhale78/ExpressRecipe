using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Core.Base;

/// <summary>
/// Base class for database connection configuration.
/// Inherit from this to create connection contexts for different databases.
/// </summary>
public abstract class DatabaseConnectionBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private string? _connectionString;

    /// <summary>
    /// Gets the connection string for this database context
    /// </summary>
    public string ConnectionString
    {
        get
        {
            if (_connectionString == null)
            {
                string connectionStringKey = GetConnectionStringKey();
                _connectionString = _configuration.GetConnectionString(connectionStringKey);

                if (string.IsNullOrWhiteSpace(_connectionString))
                {
                    string errorMessage = $"Connection string '{connectionStringKey}' not found in configuration";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                _logger.LogDebug("Loaded connection string for key: {ConnectionStringKey}", connectionStringKey);
            }

            return _connectionString;
        }
    }

    /// <summary>
    /// Gets whether tables should be dropped and recreated on startup.
    /// Checked in order:
    /// 1. HighSpeedDAL:Connections:{ConnectionKey}:DropTablesOnStartup
    /// 2. HighSpeedDAL:GlobalDropTablesOnStartup
    /// </summary>
    public bool ShouldDropTablesOnStartup
    {
        get
        {
            String connectionStringKey = GetConnectionStringKey();

            // Check specific connection config
            bool? specific = _configuration.GetValue<bool?>($"HighSpeedDAL:Connections:{connectionStringKey}:DropTablesOnStartup");
            if (specific.HasValue)
            {
                return specific.Value;
            }

            // Check global config
            return _configuration.GetValue<bool>("HighSpeedDAL:GlobalDropTablesOnStartup", false);
        }
    }

    /// <summary>
    /// Gets the database provider type
    /// </summary>
    public abstract DatabaseProvider Provider { get; }

    protected DatabaseConnectionBase(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Override to specify the configuration key for the connection string.
    /// Default implementation uses the class name without "Connection" or "Database" suffix.
    /// </summary>
    protected virtual string GetConnectionStringKey()
    {
        string typeName = GetType().Name;

        // Remove common suffixes
        if (typeName.EndsWith("Connection", StringComparison.OrdinalIgnoreCase))
        {
            typeName = typeName.Substring(0, typeName.Length - "Connection".Length);
        }
        else if (typeName.EndsWith("Database", StringComparison.OrdinalIgnoreCase))
        {
            typeName = typeName.Substring(0, typeName.Length - "Database".Length);
        }
        else if (typeName.EndsWith("Db", StringComparison.OrdinalIgnoreCase))
        {
            typeName = typeName.Substring(0, typeName.Length - "Db".Length);
        }

        return typeName;
    }
}

/// <summary>
/// Supported database providers
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    Sqlite
}
