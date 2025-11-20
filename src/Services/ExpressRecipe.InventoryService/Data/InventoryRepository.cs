using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.InventoryService.Data;

public class InventoryRepository : IInventoryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(string connectionString, ILogger<InventoryRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> AddInventoryItemAsync(Guid userId, Guid? productId, string? customName,
        Guid storageLocationId, decimal quantity, string? unit, DateTime? expirationDate,
        string? barcode, decimal? price, string? store)
    {
        const string sql = @"
            INSERT INTO InventoryItem
            (UserId, ProductId, CustomName, StorageLocationId, Quantity, Unit, ExpirationDate, Barcode, Price, Store, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @ProductId, @CustomName, @StorageLocationId, @Quantity, @Unit, @ExpirationDate, @Barcode, @Price, @Store, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StorageLocationId", storageLocationId);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.Parameters.AddWithValue("@Unit", unit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ExpirationDate", expirationDate.HasValue ? expirationDate.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Barcode", barcode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Price", price.HasValue ? price.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Store", store ?? (object)DBNull.Value);

        var itemId = (Guid)await command.ExecuteScalarAsync()!;

        // Create history entry
        await CreateHistoryEntryAsync(itemId, userId, "Added", quantity, 0, quantity, null);

        _logger.LogInformation("Added inventory item {ItemId} for user {UserId}", itemId, userId);
        return itemId;
    }

    public async Task<List<InventoryItemDto>> GetUserInventoryAsync(Guid userId)
    {
        const string sql = @"
            SELECT
                i.Id, i.UserId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.IsOpened, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            WHERE i.UserId = @UserId AND i.IsDeleted = 0
            ORDER BY i.ExpirationDate ASC NULLS LAST, i.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var items = new List<InventoryItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(MapInventoryItem(reader));
        }

        return items;
    }

    public async Task<InventoryItemDto?> GetInventoryItemAsync(Guid itemId, Guid userId)
    {
        const string sql = @"
            SELECT
                i.Id, i.UserId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.IsOpened, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            WHERE i.Id = @ItemId AND i.UserId = @UserId AND i.IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapInventoryItem(reader);
        }

        return null;
    }

    public async Task UpdateInventoryQuantityAsync(Guid itemId, decimal newQuantity, string actionType, string? reason)
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
            await CreateHistoryEntryAsync(itemId, userId, actionType, quantityChange, oldQuantity, newQuantity, reason, transaction);

            await transaction.CommitAsync();
            _logger.LogInformation("Updated inventory item {ItemId} quantity from {Old} to {New}", itemId, oldQuantity, newQuantity);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteInventoryItemAsync(Guid itemId, Guid userId)
    {
        const string sql = @"
            UPDATE InventoryItem
            SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @ItemId AND UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);
        command.Parameters.AddWithValue("@UserId", userId);

        var rows = await command.ExecuteNonQueryAsync();
        if (rows == 0)
            throw new InvalidOperationException("Inventory item not found or access denied");

        _logger.LogInformation("Deleted inventory item {ItemId} for user {UserId}", itemId, userId);
    }

    public async Task<List<InventoryItemDto>> GetExpiringItemsAsync(Guid userId, int daysAhead = 7)
    {
        const string sql = @"
            SELECT
                i.Id, i.UserId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.IsOpened, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            WHERE i.UserId = @UserId
              AND i.IsDeleted = 0
              AND i.ExpirationDate IS NOT NULL
              AND i.ExpirationDate <= DATEADD(day, @DaysAhead, GETUTCDATE())
            ORDER BY i.ExpirationDate ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DaysAhead", daysAhead);

        var items = new List<InventoryItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(MapInventoryItem(reader));
        }

        return items;
    }

    public async Task<List<StorageLocationDto>> GetStorageLocationsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, Name, Description, Temperature, IsDefault, CreatedAt
            FROM StorageLocation
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY IsDefault DESC, Name ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var locations = new List<StorageLocationDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            locations.Add(new StorageLocationDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                Temperature = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsDefault = reader.GetBoolean(5),
                CreatedAt = reader.GetDateTime(6)
            });
        }

        return locations;
    }

    public async Task<Guid> CreateStorageLocationAsync(Guid userId, string name, string? description, string? temperature)
    {
        const string sql = @"
            INSERT INTO StorageLocation (UserId, Name, Description, Temperature, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Name, @Description, @Temperature, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Temperature", temperature ?? (object)DBNull.Value);

        var locationId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Created storage location {LocationId} for user {UserId}", locationId, userId);
        return locationId;
    }

    public async Task<List<InventoryHistoryDto>> GetItemHistoryAsync(Guid itemId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                Id, InventoryItemId, UserId, ActionType, QuantityChange,
                QuantityBefore, QuantityAfter, Reason, RecipeId, CreatedAt
            FROM InventoryHistory
            WHERE InventoryItemId = @ItemId
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);
        command.Parameters.AddWithValue("@Limit", limit);

        var history = new List<InventoryHistoryDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            history.Add(new InventoryHistoryDto
            {
                Id = reader.GetGuid(0),
                InventoryItemId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                ActionType = reader.GetString(3),
                QuantityChange = reader.GetDecimal(4),
                QuantityBefore = reader.GetDecimal(5),
                QuantityAfter = reader.GetDecimal(6),
                Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                RecipeId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                CreatedAt = reader.GetDateTime(9)
            });
        }

        return history;
    }

    public async Task CreateExpirationAlertsAsync(Guid userId)
    {
        const string sql = @"
            INSERT INTO ExpirationAlert (UserId, InventoryItemId, AlertType, DaysUntilExpiration, AlertDate)
            SELECT
                @UserId,
                i.Id,
                CASE
                    WHEN DATEDIFF(day, GETUTCDATE(), i.ExpirationDate) < 0 THEN 'Expired'
                    WHEN DATEDIFF(day, GETUTCDATE(), i.ExpirationDate) <= 2 THEN 'Critical'
                    WHEN DATEDIFF(day, GETUTCDATE(), i.ExpirationDate) <= 7 THEN 'Warning'
                    ELSE 'Info'
                END,
                DATEDIFF(day, GETUTCDATE(), i.ExpirationDate),
                GETUTCDATE()
            FROM InventoryItem i
            WHERE i.UserId = @UserId
              AND i.IsDeleted = 0
              AND i.ExpirationDate IS NOT NULL
              AND i.ExpirationDate <= DATEADD(day, 14, GETUTCDATE())
              AND NOT EXISTS (
                  SELECT 1 FROM ExpirationAlert ea
                  WHERE ea.InventoryItemId = i.Id
                    AND ea.IsDismissed = 0
                    AND CAST(ea.AlertDate AS DATE) = CAST(GETUTCDATE() AS DATE)
              )";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var count = await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Created {Count} expiration alerts for user {UserId}", count, userId);
    }

    public async Task<List<ExpirationAlertDto>> GetExpirationAlertsAsync(Guid userId)
    {
        const string sql = @"
            SELECT
                ea.Id, ea.UserId, ea.InventoryItemId, ea.AlertType, ea.DaysUntilExpiration,
                ea.AlertDate, ea.IsDismissed, ea.DismissedAt,
                i.CustomName, i.ProductId
            FROM ExpirationAlert ea
            INNER JOIN InventoryItem i ON ea.InventoryItemId = i.Id
            WHERE ea.UserId = @UserId AND ea.IsDismissed = 0
            ORDER BY ea.DaysUntilExpiration ASC, ea.AlertDate DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var alerts = new List<ExpirationAlertDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            alerts.Add(new ExpirationAlertDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                InventoryItemId = reader.GetGuid(2),
                AlertType = reader.GetString(3),
                DaysUntilExpiration = reader.GetInt32(4),
                AlertDate = reader.GetDateTime(5),
                IsDismissed = reader.GetBoolean(6),
                DismissedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                ItemName = reader.IsDBNull(8) ? null : reader.GetString(8),
                ProductId = reader.IsDBNull(9) ? null : reader.GetGuid(9)
            });
        }

        return alerts;
    }

    public async Task<List<UsagePredictionDto>> GetUsagePredictionsAsync(Guid userId)
    {
        const string sql = @"
            SELECT
                Id, UserId, ProductId, IngredientId, PredictedUsagePerWeek,
                ConfidenceScore, ReorderThreshold, SuggestedQuantity, CalculatedAt, BasedOnDays
            FROM UsagePrediction
            WHERE UserId = @UserId
            ORDER BY ConfidenceScore DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var predictions = new List<UsagePredictionDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            predictions.Add(new UsagePredictionDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                ProductId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                IngredientId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                PredictedUsagePerWeek = reader.GetDecimal(4),
                ConfidenceScore = reader.GetDecimal(5),
                ReorderThreshold = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                SuggestedQuantity = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                CalculatedAt = reader.GetDateTime(8),
                BasedOnDays = reader.GetInt32(9)
            });
        }

        return predictions;
    }

    private async Task CreateHistoryEntryAsync(Guid inventoryItemId, Guid userId, string actionType,
        decimal quantityChange, decimal quantityBefore, decimal quantityAfter, string? reason,
        SqlTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO InventoryHistory
            (InventoryItemId, UserId, ActionType, QuantityChange, QuantityBefore, QuantityAfter, Reason, CreatedAt)
            VALUES (@InventoryItemId, @UserId, @ActionType, @QuantityChange, @QuantityBefore, @QuantityAfter, @Reason, GETUTCDATE())";

        if (transaction != null)
        {
            await using var command = new SqlCommand(sql, transaction.Connection, transaction);
            command.Parameters.AddWithValue("@InventoryItemId", inventoryItemId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@ActionType", actionType);
            command.Parameters.AddWithValue("@QuantityChange", quantityChange);
            command.Parameters.AddWithValue("@QuantityBefore", quantityBefore);
            command.Parameters.AddWithValue("@QuantityAfter", quantityAfter);
            command.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@InventoryItemId", inventoryItemId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@ActionType", actionType);
            command.Parameters.AddWithValue("@QuantityChange", quantityChange);
            command.Parameters.AddWithValue("@QuantityBefore", quantityBefore);
            command.Parameters.AddWithValue("@QuantityAfter", quantityAfter);
            command.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }
    }

    private InventoryItemDto MapInventoryItem(SqlDataReader reader)
    {
        return new InventoryItemDto
        {
            Id = reader.GetGuid(0),
            UserId = reader.GetGuid(1),
            ProductId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
            CustomName = reader.IsDBNull(3) ? null : reader.GetString(3),
            StorageLocationId = reader.GetGuid(4),
            Quantity = reader.GetDecimal(5),
            Unit = reader.IsDBNull(6) ? null : reader.GetString(6),
            PurchaseDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            ExpirationDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
            OpenedDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
            Barcode = reader.IsDBNull(11) ? null : reader.GetString(11),
            Price = reader.IsDBNull(12) ? null : reader.GetDecimal(12),
            Store = reader.IsDBNull(13) ? null : reader.GetString(13),
            IsOpened = reader.GetBoolean(14),
            CreatedAt = reader.GetDateTime(15),
            UpdatedAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
            StorageLocationName = reader.GetString(17)
        };
    }
}
