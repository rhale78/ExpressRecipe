using HighSpeedDAL.Core.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.Example.Database;

/// <summary>
/// Database connection configuration for the example application.
/// The framework automatically pulls the connection string from configuration
/// using the class name (minus "Database" suffix) as the key.
/// 
/// In this case, it will look for "ConnectionStrings:Store" in appsettings.json
/// </summary>
public class StoreDatabase : DatabaseConnectionBase
{
    public StoreDatabase(IConfiguration configuration, ILogger<StoreDatabase> logger)
        : base(configuration, logger)
    {
    }

    /// <summary>
    /// Specifies that this connection uses SQL Server
    /// </summary>
    public override DatabaseProvider Provider => DatabaseProvider.SqlServer;

    // That's it! The framework handles everything else:
    // - Connection string retrieval from config
    // - Connection pooling
    // - Error handling
    // - Logging
}
