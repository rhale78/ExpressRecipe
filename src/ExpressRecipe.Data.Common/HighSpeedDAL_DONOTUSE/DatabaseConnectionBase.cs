//using Microsoft.Data.SqlClient;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using System.Data;

//namespace ExpressRecipe.Data.Common.HighSpeedDAL;

///// <summary>
///// Base class for database connections following HighSpeedDAL pattern.
///// Provides connection management and logging.
///// </summary>
//public abstract class DatabaseConnectionBase
//{
//    protected readonly IConfiguration Configuration;
//    protected readonly ILogger Logger;
//    private readonly string _connectionStringKey;

//    protected DatabaseConnectionBase(IConfiguration configuration, ILogger logger)
//    {
//        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
//        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        _connectionStringKey = GetConnectionStringKey();
//    }

//    /// <summary>
//    /// Gets the connection string from configuration.
//    /// </summary>
//    public string ConnectionString
//    {
//        get
//        {
//            var connectionString = Configuration.GetConnectionString(_connectionStringKey);
//            if (string.IsNullOrWhiteSpace(connectionString))
//            {
//                throw new InvalidOperationException(
//                    $"Connection string '{_connectionStringKey}' not found in configuration.");
//            }
//            return connectionString;
//        }
//    }

//    /// <summary>
//    /// Creates a new database connection.
//    /// </summary>
//    public virtual async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
//    {
//        var connection = new SqlConnection(ConnectionString);
//        await connection.OpenAsync(cancellationToken);
//        Logger.LogDebug("Database connection opened for {ConnectionKey}", _connectionStringKey);
//        return connection;
//    }

//    /// <summary>
//    /// Gets the connection string key. Override to customize.
//    /// Default removes "Connection" suffix from class name.
//    /// </summary>
//    protected virtual string GetConnectionStringKey()
//    {
//        var typeName = GetType().Name;
//        if (typeName.EndsWith("Connection", StringComparison.OrdinalIgnoreCase))
//        {
//            return typeName.Substring(0, typeName.Length - "Connection".Length);
//        }
//        return typeName;
//    }

//    /// <summary>
//    /// Executes an action within a transaction.
//    /// </summary>
//    public async Task<T> ExecuteInTransactionAsync<T>(
//        Func<IDbConnection, IDbTransaction, Task<T>> action,
//        CancellationToken cancellationToken = default)
//    {
//        var connection = await CreateConnectionAsync(cancellationToken);
//        IDbTransaction? transaction = null;

//        try
//        {
//            transaction = connection.BeginTransaction();
//            var result = await action(connection, transaction);
            
//            if (transaction is SqlTransaction sqlTransaction)
//            {
//                await sqlTransaction.CommitAsync(cancellationToken);
//            }
//            else
//            {
//                transaction.Commit();
//            }
            
//            Logger.LogDebug("Transaction committed successfully");
//            return result;
//        }
//        catch (Exception ex)
//        {
//            Logger.LogError(ex, "Transaction failed, rolling back");
            
//            if (transaction != null)
//            {
//                if (transaction is SqlTransaction sqlTransaction)
//                {
//                    await sqlTransaction.RollbackAsync(cancellationToken);
//                }
//                else
//                {
//                    transaction.Rollback();
//                }
//            }
//            throw;
//        }
//        finally
//        {
//            transaction?.Dispose();
//            connection?.Dispose();
//        }
//    }
//}

///// <summary>
///// Product database connection for ExpressRecipe.
///// </summary>
//public class ProductConnection : DatabaseConnectionBase
//{
//    public ProductConnection(IConfiguration configuration, ILogger<ProductConnection> logger)
//        : base(configuration, logger)
//    {
//    }

//    protected override string GetConnectionStringKey()
//    {
//        return "ProductDb"; // Matches appsettings.json ConnectionStrings:ProductDb
//    }
//}
