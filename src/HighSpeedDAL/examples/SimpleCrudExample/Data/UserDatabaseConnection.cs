using HighSpeedDAL.Core.Base;
using HighSpeedDAL.Core.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.SimpleCrudExample.Data;

/// <summary>
/// Database connection for the SimpleCrudExample application
/// Connection string loaded from appsettings.json key "UserDatabase"
/// </summary>
public class UserDatabaseConnection : DatabaseConnectionBase
{
    public UserDatabaseConnection(IConfiguration configuration, ILogger<UserDatabaseConnection> logger)
        : base(configuration, logger)
    {
    }

    public override DatabaseProvider Provider => DatabaseProvider.SqlServer;

    protected override string GetConnectionStringKey()
    {
        return "UserDatabase";
    }
}
