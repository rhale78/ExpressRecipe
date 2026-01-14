using HighSpeedDAL.Core.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ShowcaseExample.Data;

/// <summary>
/// SQLite database connection for showcase examples.
/// Demonstrates connection factory pattern for DAL operations.
/// </summary>
public class ShowcaseConnection : DatabaseConnectionBase
{
    public ShowcaseConnection(IConfiguration configuration, ILogger<ShowcaseConnection> logger)
        : base(configuration, logger)
    {
    }

    public override DatabaseProvider Provider => DatabaseProvider.Sqlite;

    protected override string GetConnectionStringKey()
    {
        return "ShowcaseDatabase";
    }
}
