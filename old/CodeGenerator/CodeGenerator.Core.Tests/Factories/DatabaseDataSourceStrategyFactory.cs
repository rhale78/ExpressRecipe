using CodeGenerator.Core.SourceDataStrategies;
using System;
using Microsoft.Pex.Framework;

namespace CodeGenerator.Core.SourceDataStrategies
{
    /// <summary>A factory for CodeGenerator.Core.SourceDataStrategies.DatabaseDataSourceStrategy instances</summary>
    public static partial class DatabaseDataSourceStrategyFactory
    {
        /// <summary>A factory for CodeGenerator.Core.SourceDataStrategies.DatabaseDataSourceStrategy instances</summary>
        [PexFactoryMethod(typeof(DatabaseDataSourceStrategy))]
        public static DatabaseDataSourceStrategy Create()
        {
            DatabaseDataSourceStrategy databaseDataSourceStrategy
            = new DatabaseDataSourceStrategy();
            ((DataSourceStrategyBase)databaseDataSourceStrategy).Settings = "Server=RHALE78-5-1102;Database=ExpressRecipe.Logging;Trusted_Connection=True;";
            return databaseDataSourceStrategy;

            // TODO: Edit factory method of DatabaseDataSourceStrategy
            // This method should be able to configure the object in all possible ways.
            // Add as many parameters as needed,
            // and assign their values to each field by using the API.
        }
    }
}
