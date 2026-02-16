using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.InventoryService.Data;

// Partial class for Enhanced Inventory Items and Reports
public partial class InventoryRepository
{
    #region Enhanced Inventory Items

    public async Task<Guid> AddInventoryItemAsync(Guid userId, Guid? householdId, Guid? productId, string? customName, 
        Guid storageLocationId, decimal quantity, string? unit, DateTime? expirationDate, string? barcode, 
        decimal? price = null, string? preferredStore = null, string? storeLocation = null)
    {
        const string sql = @"
            INSERT INTO InventoryItem
            (UserId, HouseholdId, ProductId, CustomName, StorageLocationId, Quantity, Unit, ExpirationDate, 
             Barcode, Price, PreferredStore, StoreLocation, AddedBy, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @ProductId, @CustomName, @StorageLocationId, @Quantity, @Unit, @ExpirationDate, 
                    @Barcode, @Price, @PreferredStore, @StoreLocation, @AddedBy, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StorageLocationId", storageLocationId);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.Parameters.AddWithValue("@Unit", unit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ExpirationDate", expirationDate.HasValue ? expirationDate.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Barcode", barcode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Price", price.HasValue ? price.Value : DBNull.Value);
        command.Parameters.AddWithValue("@PreferredStore", preferredStore ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StoreLocation", storeLocation ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AddedBy", userId);

        var itemId = (Guid)await command.ExecuteScalarAsync()!;

        // Create history entry
        await CreateHistoryEntryAsync(itemId, userId, "Added", quantity, 0, quantity, null, userId);

        _logger.LogInformation("Added inventory item {ItemId} for user {UserId}", itemId, userId);
        return itemId;
    }

    public async Task<List<InventoryItemDto>> GetHouseholdInventoryAsync(Guid householdId)
    {
        const string sql = @"
            SELECT
                i.Id, i.UserId, i.HouseholdId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.PreferredStore, i.StoreLocation,
                i.IsOpened, i.AddedBy, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName, s.AddressId,
                a.Name AS AddressName
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE i.HouseholdId = @HouseholdId AND i.IsDeleted = 0
            ORDER BY i.ExpirationDate ASC, i.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

        return await ReadInventoryItemsAsync(command);
    }

    public async Task<List<InventoryItemDto>> GetInventoryByAddressAsync(Guid addressId)
    {
        const string sql = @"
            SELECT
                i.Id, i.UserId, i.HouseholdId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.PreferredStore, i.StoreLocation,
                i.IsOpened, i.AddedBy, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName, s.AddressId,
                a.Name AS AddressName
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE s.AddressId = @AddressId AND i.IsDeleted = 0
            ORDER BY i.ExpirationDate ASC, i.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AddressId", addressId);

        return await ReadInventoryItemsAsync(command);
    }

    public async Task<List<InventoryItemDto>> GetInventoryByStorageLocationAsync(Guid storageLocationId)
    {
        const string sql = @"
            SELECT
                i.Id, i.UserId, i.HouseholdId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.PreferredStore, i.StoreLocation,
                i.IsOpened, i.AddedBy, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName, s.AddressId,
                a.Name AS AddressName
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE i.StorageLocationId = @StorageLocationId AND i.IsDeleted = 0
            ORDER BY i.ExpirationDate ASC, i.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@StorageLocationId", storageLocationId);

        return await ReadInventoryItemsAsync(command);
    }

    public async Task UpdateInventoryQuantityAsync(Guid itemId, decimal newQuantity, string actionType, Guid changedBy, 
        string? reason = null, string? disposalReason = null, string? allergenDetected = null)
    {
        const string getQuantitySql = "SELECT Quantity, UserId FROM InventoryItem WHERE Id = @ItemId AND IsDeleted = 0";
        const string updateSql = "UPDATE InventoryItem SET Quantity = @Quantity, UpdatedAt = GETUTCDATE() WHERE Id = @ItemId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        try
        {
            // Get current quantity
            decimal oldQuantity;
            Guid userId;
            await using (var command = new SqlCommand(getQuantitySql, connection, transaction))
            {
                command.Parameters.AddWithValue("@ItemId", itemId);
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("Inventory item not found");

                oldQuantity = reader.GetDecimal(0);
                userId = reader.GetGuid(1);
            }

            // Update quantity
            await using (var command = new SqlCommand(updateSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@ItemId", itemId);
                command.Parameters.AddWithValue("@Quantity", newQuantity);
                await command.ExecuteNonQueryAsync();
            }

            // Create history entry
            var quantityChange = newQuantity - oldQuantity;
            await CreateHistoryEntryAsync(itemId, userId, actionType, quantityChange, oldQuantity, newQuantity, 
                reason, changedBy, disposalReason, allergenDetected, transaction);

            await transaction.CommitAsync();
            _logger.LogInformation("Updated inventory item {ItemId} quantity from {Old} to {New}", itemId, oldQuantity, newQuantity);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<InventoryItemDto>> GetLowStockItemsAsync(Guid userId, decimal threshold = 2.0m)
    {
        const string sql = @"
            SELECT
                i.Id, i.UserId, i.HouseholdId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.PreferredStore, i.StoreLocation,
                i.IsOpened, i.AddedBy, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName, s.AddressId,
                a.Name AS AddressName
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE i.UserId = @UserId 
              AND i.IsDeleted = 0 
              AND i.Quantity <= @Threshold
            ORDER BY i.Quantity ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Threshold", threshold);

        return await ReadInventoryItemsAsync(command);
    }

    public async Task<List<InventoryItemDto>> GetItemsRunningOutAsync(Guid userId, int withinDays = 7)
    {
        // Items that will run out within specified days based on usage prediction
        const string sql = @"
            SELECT DISTINCT
                i.Id, i.UserId, i.HouseholdId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.PreferredStore, i.StoreLocation,
                i.IsOpened, i.AddedBy, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName, s.AddressId,
                a.Name AS AddressName
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            LEFT JOIN Address a ON s.AddressId = a.Id
            INNER JOIN UsagePrediction up ON i.ProductId = up.ProductId
            WHERE i.UserId = @UserId 
              AND i.IsDeleted = 0
              AND up.PredictedUsagePerWeek > 0
              AND (i.Quantity / (up.PredictedUsagePerWeek / 7.0)) <= @WithinDays
            ORDER BY (i.Quantity / (up.PredictedUsagePerWeek / 7.0)) ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@WithinDays", withinDays);

        return await ReadInventoryItemsAsync(command);
    }

    public async Task<List<InventoryItemDto>> GetItemsAboutToExpireAsync(Guid userId, int daysAhead = 3)
    {
        return await GetExpiringItemsAsync(userId, daysAhead);
    }

    private async Task<List<InventoryItemDto>> ReadInventoryItemsAsync(SqlCommand command)
    {
        var items = new List<InventoryItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new InventoryItemDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                ProductId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                CustomName = reader.IsDBNull(4) ? null : reader.GetString(4),
                StorageLocationId = reader.GetGuid(5),
                Quantity = reader.GetDecimal(6),
                Unit = reader.IsDBNull(7) ? null : reader.GetString(7),
                PurchaseDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                ExpirationDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                OpenedDate = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                Notes = reader.IsDBNull(11) ? null : reader.GetString(11),
                Barcode = reader.IsDBNull(12) ? null : reader.GetString(12),
                Price = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                Store = reader.IsDBNull(14) ? null : reader.GetString(14),
                PreferredStore = reader.IsDBNull(15) ? null : reader.GetString(15),
                StoreLocation = reader.IsDBNull(16) ? null : reader.GetString(16),
                IsOpened = reader.GetBoolean(17),
                AddedBy = reader.IsDBNull(18) ? null : reader.GetGuid(18),
                CreatedAt = reader.GetDateTime(19),
                UpdatedAt = reader.IsDBNull(20) ? null : reader.GetDateTime(20),
                StorageLocationName = reader.GetString(21),
                AddressId = reader.IsDBNull(22) ? null : reader.GetGuid(22),
                AddressName = reader.IsDBNull(23) ? null : reader.GetString(23)
            });
        }

        return items;
    }

    #endregion

    #region Reports

    public async Task<InventoryReportDto> GetInventoryReportAsync(Guid userId, Guid? householdId)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS TotalItems,
                SUM(CASE WHEN ExpirationDate IS NOT NULL AND ExpirationDate <= DATEADD(day, 7, GETUTCDATE()) AND ExpirationDate > GETUTCDATE() THEN 1 ELSE 0 END) AS ExpiringSoonItems,
                SUM(CASE WHEN ExpirationDate IS NOT NULL AND ExpirationDate <= GETUTCDATE() THEN 1 ELSE 0 END) AS ExpiredItems,
                SUM(CASE WHEN Quantity <= 2.0 THEN 1 ELSE 0 END) AS LowStockItems,
                SUM(CASE WHEN Price IS NOT NULL THEN i.Quantity * i.Price ELSE 0 END) AS TotalEstimatedValue
            FROM InventoryItem i
            WHERE (@HouseholdId IS NULL AND i.UserId = @UserId OR i.HouseholdId = @HouseholdId)
              AND i.IsDeleted = 0";

        const string locationSql = @"
            SELECT s.Name, COUNT(*) AS ItemCount
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            WHERE (@HouseholdId IS NULL AND i.UserId = @UserId OR i.HouseholdId = @HouseholdId)
              AND i.IsDeleted = 0
            GROUP BY s.Name
            ORDER BY ItemCount DESC";

        const string addressSql = @"
            SELECT ISNULL(a.Name, 'Unassigned') AS AddressName, COUNT(*) AS ItemCount
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE (@HouseholdId IS NULL AND i.UserId = @UserId OR i.HouseholdId = @HouseholdId)
              AND i.IsDeleted = 0
            GROUP BY a.Name
            ORDER BY ItemCount DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var report = new InventoryReportDto();

        // Get totals
        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                report.TotalItems = reader.GetInt32(0);
                report.ExpiringSoonItems = reader.GetInt32(1);
                report.ExpiredItems = reader.GetInt32(2);
                report.LowStockItems = reader.GetInt32(3);
                report.TotalEstimatedValue = reader.GetDecimal(4);
            }
        }

        // Get items by location
        await using (var command = new SqlCommand(locationSql, connection))
        {
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                report.ItemsByLocation[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        // Get items by address
        await using (var command = new SqlCommand(addressSql, connection))
        {
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                report.ItemsByAddress[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        // Calculate running out items based on predictions
        var runningOutItems = await GetItemsRunningOutAsync(userId, 7);
        report.RunningOutItems = runningOutItems.Count;

        return report;
    }

    #endregion

    #region Helper Methods

    private async Task CreateHistoryEntryAsync(Guid itemId, Guid userId, string actionType, 
        decimal quantityChange, decimal quantityBefore, decimal quantityAfter, string? reason, 
        Guid? changedBy = null, string? disposalReason = null, string? allergenDetected = null, 
        SqlTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO InventoryHistory
            (InventoryItemId, UserId, ChangedBy, ActionType, QuantityChange, QuantityBefore, QuantityAfter, Reason, DisposalReason, AllergenDetected, CreatedAt)
            VALUES (@InventoryItemId, @UserId, @ChangedBy, @ActionType, @QuantityChange, @QuantityBefore, @QuantityAfter, @Reason, @DisposalReason, @AllergenDetected, GETUTCDATE())";

        SqlConnection connection = transaction?.Connection ?? new SqlConnection(_connectionString);
        bool shouldDisposeConnection = transaction == null;

        try
        {
            if (shouldDisposeConnection)
                await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@InventoryItemId", itemId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@ChangedBy", changedBy.HasValue ? changedBy.Value : DBNull.Value);
            command.Parameters.AddWithValue("@ActionType", actionType);
            command.Parameters.AddWithValue("@QuantityChange", quantityChange);
            command.Parameters.AddWithValue("@QuantityBefore", quantityBefore);
            command.Parameters.AddWithValue("@QuantityAfter", quantityAfter);
            command.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DisposalReason", disposalReason ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@AllergenDetected", allergenDetected ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldDisposeConnection)
                await connection.DisposeAsync();
        }
    }

    #endregion
}
