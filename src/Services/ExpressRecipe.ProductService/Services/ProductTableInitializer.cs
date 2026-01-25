using System.Data;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Entities;
using HighSpeedDAL.Core.InMemoryTable;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ProductService.Services
{
    /// <summary>
    /// Handles the intelligent hydration of the Product InMemoryTable on startup.
    /// Implements the logic: "Pull from primary unless staging table itself is newer".
    /// </summary>
    public class ProductTableInitializer : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProductTableInitializer> _logger;

        public ProductTableInitializer(IServiceProvider serviceProvider, ILogger<ProductTableInitializer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing Product In-Memory Table...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var tableManager = scope.ServiceProvider.GetRequiredService<InMemoryTableManager>();
                var connection = scope.ServiceProvider.GetRequiredService<ProductDatabaseConnection>();
                
                // Get the table wrapper
                var table = tableManager.GetTable<ProductEntity>("Product");
                if (table == null)
                {
                    _logger.LogWarning("Product table not found in InMemoryTableManager. Skipping initialization.");
                    return;
                }

                // If already loaded (e.g. from Memory Mapped File), check if we need to sync diffs or just proceed
                if (table.RowCount > 0)
                {
                    _logger.LogInformation("Product table already loaded with {Count} rows (likely from Memory Mapped File).", table.RowCount);
                    // In a perfect world, we'd check for diffs here. For now, assume MMF is up to date or will catch up.
                    return;
                }

                try
                {
                    await HydrateTableSmartAsync(table, connection, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to hydrate Product table from database.");
                    // Non-fatal, app can start empty or retry
                }
            }
        }

        private async Task HydrateTableSmartAsync(InMemoryTable<ProductEntity> table, ProductDatabaseConnection dbConnection, CancellationToken ct)
        {
            // Logic: Compare Max(ModifiedDate) in Primary vs Max(StagedAt) in Staging
            // If Staging is newer => Load from Staging (it has the latest unmerged changes)
            // Else => Load from Primary
            
            using var conn = new SqlConnection(dbConnection.ConnectionString);
            await conn.OpenAsync(ct);

            DateTime? primaryLastModified = await GetMaxDateAsync(conn, "Product", "UpdatedAt", ct);
            DateTime? stagingLastModified = await GetMaxDateAsync(conn, "Product_Staging", "StagedAt", ct);

            _logger.LogInformation("Hydration Logic: Primary LastMod={Primary}, Staging LastMod={Staging}", 
                primaryLastModified, stagingLastModified);

            bool preferStaging = false;

            if (stagingLastModified.HasValue)
            {
                if (!primaryLastModified.HasValue)
                {
                    // Primary empty, Staging has data
                    preferStaging = true;
                }
                else if (stagingLastModified.Value > primaryLastModified.Value)
                {
                    // Staging is newer
                    preferStaging = true;
                }
            }

            if (preferStaging)
            {
                _logger.LogInformation("Staging table appears newer. Hydrating from 'Product_Staging'...");
                // Note: LoadFromStagingAsync automatically handles the "_Staging" suffix logic
                int count = await table.LoadFromStagingAsync(conn, null, ct);
                _logger.LogInformation("Hydrated {Count} rows from Staging.", count);
            }
            else
            {
                _logger.LogInformation("Primary table appears newer (or Staging empty). Hydrating from 'Product'...");
                int count = await table.LoadFromDatabaseAsync(conn, null, ct);
                _logger.LogInformation("Hydrated {Count} rows from Primary.", count);
            }
        }

        private async Task<DateTime?> GetMaxDateAsync(SqlConnection conn, string tableName, string colName, CancellationToken ct)
        {
            try
            {
                // Quick check if table exists first to avoid exception
                var checkCmd = new SqlCommand($"SELECT 1 FROM sys.tables WHERE name = '{tableName}'", conn);
                if (await checkCmd.ExecuteScalarAsync(ct) == null) return null;

                var cmd = new SqlCommand($"SELECT MAX({colName}) FROM {tableName}", conn);
                var result = await cmd.ExecuteScalarAsync(ct);
                if (result != null && result != DBNull.Value)
                {
                    return (DateTime)result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to check max date for {Table}: {Message}", tableName, ex.Message);
            }
            return null;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
