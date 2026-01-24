using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Partial extension to ProductStagingEntityDal with optimized bulk operations
/// </summary>
public sealed partial class ProductStagingEntityDal
{
    private readonly ILogger? _partialLogger;

    /// <summary>
    /// Bulk updates processing status for multiple staging products
    /// OPTIMIZED: Parallel updates in larger batches (200) to reduce contention
    /// </summary>
    public async Task<int> BulkUpdateProcessingStatusAsync(IEnumerable<Guid> ids, string status, string? error, CancellationToken cancellationToken = default)
    {
        var idList = new List<Guid>(ids);
        if (idList.Count == 0) return 0;

        Logger.LogInformation("Bulk updating {Count} product staging statuses to '{Status}' (optimized)", idList.Count, status);

        // Fetch all entities first
        var entities = await GetByIdsAsync(idList);

        // Update fields in memory
        var now = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.ProcessingStatus = status;
            entity.ProcessingAttempts = entity.ProcessingAttempts + 1;
            entity.ProcessingError = error;
            if (status == "Completed")
            {
                entity.ProcessedAt = now;
            }
        }

        // OPTIMIZATION: Update in larger parallel batches (200 instead of 50)
        // to reduce database connection pool pressure and improve throughput
        const int BATCH_SIZE = 200;
        int totalUpdated = 0;

        for (int i = 0; i < entities.Count; i += BATCH_SIZE)
        {
            var batch = entities.Skip(i).Take(BATCH_SIZE).ToList();
            var updateTasks = batch.Select(e => UpdateAsync(e, "System", cancellationToken)).ToList();
            await Task.WhenAll(updateTasks);
            totalUpdated += batch.Count;
        }

        Logger.LogInformation("Successfully bulk updated {Count} staging products to status '{Status}'", totalUpdated, status);
        return totalUpdated;
    }
}
