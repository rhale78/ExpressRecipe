using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ExpressRecipe.Data.Common.HighSpeedDAL;

/// <summary>
/// Base class for database connections following HighSpeedDAL pattern.
/// Provides connection management and logging.
/// </summary>
public abstract class DatabaseConnectionBase
{
    protected readonly IConfiguration Configuration;
    protected readonly ILogger Logger;
    private readonly string _connectionStringKey;

    protected DatabaseConnectionBase(IConfiguration configuration, ILogger logger)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionStringKey = GetConnectionStringKey();
    }

    /// <summary>
    /// Gets the connection string from configuration.
    /// </summary>
    public string ConnectionString
    {
        get
        {
            var connectionString = Configuration.GetConnectionString(_connectionStringKey);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{_connectionStringKey}' not found in configuration.");
            }
            return connectionString;
        }
    }

    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    public virtual async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        Logger.LogDebug("Database connection opened for {ConnectionKey}", _connectionStringKey);
        return connection;
    }

    /// <summary>
    /// Gets the connection string key. Override to customize.
    /// Default removes "Connection" suffix from class name.
    /// </summary>
    protected virtual string GetConnectionStringKey()
    {
        var typeName = GetType().Name;
        if (typeName.EndsWith("Connection", StringComparison.OrdinalIgnoreCase))
        {
            return typeName.Substring(0, typeName.Length - "Connection".Length);
        }
        return typeName;
    }

    /// <summary>
    /// Executes an action within a transaction.
    /// </summary>
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<IDbConnection, IDbTransaction, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            var result = await action(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
            Logger.LogDebug("Transaction committed successfully");
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Transaction failed, rolling back");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

/// <summary>
/// Product database connection for ExpressRecipe.
/// </summary>
public class ProductConnection : DatabaseConnectionBase
{
    public ProductConnection(IConfiguration configuration, ILogger<ProductConnection> logger)
        : base(configuration, logger)
    {
    }

    protected override string GetConnectionStringKey()
    {
        return "ProductDb"; // Matches appsettings.json ConnectionStrings:ProductDb
    }
}
