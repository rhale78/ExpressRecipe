using HighSpeedDAL.Core.Base;
using HighSpeedDAL.Core.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data
{
    /// <summary>
    /// Database connection for ProductService
    /// Connection string loaded from appsettings.json key "ProductDb"
    /// </summary>
    public class ProductDatabaseConnection : DatabaseConnectionBase
    {
        public ProductDatabaseConnection(IConfiguration configuration, ILogger<ProductDatabaseConnection> logger)
            : base(configuration, logger)
        {
        }

        public override DatabaseProvider Provider => DatabaseProvider.SqlServer;

        protected override string GetConnectionStringKey()
        {
            return "ProductDb";
        }
    }
}
