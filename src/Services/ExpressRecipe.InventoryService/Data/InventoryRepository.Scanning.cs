using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.InventoryService.Data;

// Partial class for Storage Locations and Scanning
public partial class InventoryRepository
{
    #region Storage Locations

    public async Task<Guid> CreateStorageLocationAsync(Guid userId, Guid? householdId, Guid? addressId, string name, string? description, string? temperature)
    {
        const string sql = @"
            INSERT INTO StorageLocation 
            (UserId, HouseholdId, AddressId, Name, Description, Temperature, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @AddressId, @Name, @Description, @Temperature, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@AddressId", addressId.HasValue ? addressId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Temperature", temperature ?? (object)DBNull.Value);

        var locationId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Created storage location {LocationId} for user {UserId}", locationId, userId);
        return locationId;
    }

    public async Task<List<StorageLocationDto>> GetStorageLocationsByAddressAsync(Guid addressId)
    {
        const string sql = @"
            SELECT 
                s.Id, s.UserId, s.HouseholdId, s.AddressId, s.Name, s.Description, s.Temperature, s.IsDefault, s.CreatedAt,
                a.Name AS AddressName,
                (SELECT COUNT(*) FROM InventoryItem WHERE StorageLocationId = s.Id AND IsDeleted = 0) AS ItemCount
            FROM StorageLocation s
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE s.AddressId = @AddressId AND s.IsDeleted = 0
            ORDER BY s.Name";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AddressId", addressId);

        return await ReadStorageLocationsAsync(command);
    }

    public async Task<List<StorageLocationDto>> GetStorageLocationsByHouseholdAsync(Guid householdId)
    {
        const string sql = @"
            SELECT 
                s.Id, s.UserId, s.HouseholdId, s.AddressId, s.Name, s.Description, s.Temperature, s.IsDefault, s.CreatedAt,
                a.Name AS AddressName,
                (SELECT COUNT(*) FROM InventoryItem WHERE StorageLocationId = s.Id AND IsDeleted = 0) AS ItemCount
            FROM StorageLocation s
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE s.HouseholdId = @HouseholdId AND s.IsDeleted = 0
            ORDER BY a.IsPrimary DESC, a.Name, s.Name";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

        return await ReadStorageLocationsAsync(command);
    }

    public async Task UpdateStorageLocationAsync(Guid locationId, string name, string? description, string? temperature, Guid? addressId)
    {
        const string sql = @"
            UPDATE StorageLocation
            SET Name = @Name, 
                Description = @Description, 
                Temperature = @Temperature,
                AddressId = @AddressId,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @LocationId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LocationId", locationId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Temperature", temperature ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AddressId", addressId.HasValue ? addressId.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Updated storage location {LocationId}", locationId);
    }

    public async Task DeleteStorageLocationAsync(Guid locationId)
    {
        const string sql = "UPDATE StorageLocation SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @LocationId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LocationId", locationId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Deleted storage location {LocationId}", locationId);
    }

    private async Task<List<StorageLocationDto>> ReadStorageLocationsAsync(SqlCommand command)
    {
        var locations = new List<StorageLocationDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            locations.Add(new StorageLocationDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                AddressId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                Name = reader.GetString(4),
                Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                Temperature = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsDefault = reader.GetBoolean(7),
                CreatedAt = reader.GetDateTime(8),
                AddressName = reader.IsDBNull(9) ? null : reader.GetString(9),
                ItemCount = reader.GetInt32(10)
            });
        }

        return locations;
    }

    #endregion

    #region Scanning Sessions

    public async Task<Guid> StartScanSessionAsync(Guid userId, Guid? householdId, string sessionType, Guid? storageLocationId)
    {
        const string sql = @"
            INSERT INTO InventoryScanSession 
            (UserId, HouseholdId, SessionType, StorageLocationId, StartedAt, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @SessionType, @StorageLocationId, GETUTCDATE(), 1)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@SessionType", sessionType);
        command.Parameters.AddWithValue("@StorageLocationId", storageLocationId.HasValue ? storageLocationId.Value : DBNull.Value);

        var sessionId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Started scan session {SessionId} type {SessionType} for user {UserId}", sessionId, sessionType, userId);
        return sessionId;
    }

    public async Task<ScanSessionDto?> GetActiveScanSessionAsync(Guid userId)
    {
        const string sql = @"
            SELECT 
                s.Id, s.UserId, s.HouseholdId, s.SessionType, s.StorageLocationId, 
                s.StartedAt, s.EndedAt, s.ItemsScanned, s.IsActive,
                sl.Name AS StorageLocationName
            FROM InventoryScanSession s
            LEFT JOIN StorageLocation sl ON s.StorageLocationId = sl.Id
            WHERE s.UserId = @UserId AND s.IsActive = 1
            ORDER BY s.StartedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapScanSession(reader);
        }

        return null;
    }

    public async Task<ScanSessionDto?> GetScanSessionByIdAsync(Guid sessionId)
    {
        const string sql = @"
            SELECT 
                s.Id, s.UserId, s.HouseholdId, s.SessionType, s.StorageLocationId, 
                s.StartedAt, s.EndedAt, s.ItemsScanned, s.IsActive,
                sl.Name AS StorageLocationName
            FROM InventoryScanSession s
            LEFT JOIN StorageLocation sl ON s.StorageLocationId = sl.Id
            WHERE s.Id = @SessionId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapScanSession(reader);
        }

        return null;
    }

    public async Task UpdateScanSessionItemCountAsync(Guid sessionId, int itemsScanned)
    {
        const string sql = "UPDATE InventoryScanSession SET ItemsScanned = @ItemsScanned WHERE Id = @SessionId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        command.Parameters.AddWithValue("@ItemsScanned", itemsScanned);

        await command.ExecuteNonQueryAsync();
    }

    public async Task EndScanSessionAsync(Guid sessionId)
    {
        const string sql = "UPDATE InventoryScanSession SET EndedAt = GETUTCDATE(), IsActive = 0 WHERE Id = @SessionId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Ended scan session {SessionId}", sessionId);
    }

    public async Task<Guid> ScanAddItemAsync(Guid sessionId, string barcode, decimal quantity, Guid storageLocationId)
    {
        // Get session details
        var session = await GetScanSessionByIdAsync(sessionId);
        if (session == null || !session.IsActive)
            throw new InvalidOperationException("Scan session not found or not active");

        // Barcode lookup not yet implemented in InventoryRepository.
        // Design: call ProductService GET /api/products/barcode/{barcode} via service-to-service HTTP,
        // or join against a local products cache populated by the ScannerService.
        var itemId = await AddInventoryItemAsync(
            session.UserId, 
            session.HouseholdId,
            null, // productId - would be looked up
            null, // customName
            storageLocationId,
            quantity,
            null, // unit
            null, // expirationDate
            barcode,
            null, // price
            null, // preferredStore
            null  // storeLocation
        );

        // Update session item count
        await UpdateScanSessionItemCountAsync(sessionId, session.ItemsScanned + 1);

        return itemId;
    }

    public async Task<Guid> ScanUseItemAsync(Guid sessionId, string barcode, decimal quantity)
    {
        // Get session details
        var session = await GetScanSessionByIdAsync(sessionId);
        if (session == null || !session.IsActive)
            throw new InvalidOperationException("Scan session not found or not active");

        // Find item by barcode
        const string findSql = @"
            SELECT TOP 1 Id, Quantity 
            FROM InventoryItem 
            WHERE UserId = @UserId AND Barcode = @Barcode AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        Guid itemId;
        decimal currentQuantity;

        await using (var command = new SqlCommand(findSql, connection))
        {
            command.Parameters.AddWithValue("@UserId", session.UserId);
            command.Parameters.AddWithValue("@Barcode", barcode);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException($"No inventory item found with barcode {barcode}");

            itemId = reader.GetGuid(0);
            currentQuantity = reader.GetDecimal(1);
        }

        // Update quantity
        var newQuantity = Math.Max(0, currentQuantity - quantity);
        await UpdateInventoryQuantityAsync(itemId, newQuantity, "Used", session.UserId, $"Scanned usage in session {sessionId}");

        // Update session item count
        await UpdateScanSessionItemCountAsync(sessionId, session.ItemsScanned + 1);

        return itemId;
    }

    public async Task<Guid> ScanDisposeItemAsync(Guid sessionId, string barcode, string disposalReason, string? allergenDetected)
    {
        // Get session details
        var session = await GetScanSessionByIdAsync(sessionId);
        if (session == null || !session.IsActive)
            throw new InvalidOperationException("Scan session not found or not active");

        // Find item by barcode
        const string findSql = @"
            SELECT TOP 1 Id, ProductId
            FROM InventoryItem 
            WHERE UserId = @UserId AND Barcode = @Barcode AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        Guid itemId;
        Guid? productId;

        await using (var command = new SqlCommand(findSql, connection))
        {
            command.Parameters.AddWithValue("@UserId", session.UserId);
            command.Parameters.AddWithValue("@Barcode", barcode);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException($"No inventory item found with barcode {barcode}");

            itemId = reader.GetGuid(0);
            productId = reader.IsDBNull(1) ? null : reader.GetGuid(1);
        }

        // Update quantity to 0 and mark as disposed
        await UpdateInventoryQuantityAsync(itemId, 0, "Disposed", session.UserId, 
            $"Disposed: {disposalReason}", disposalReason, allergenDetected);

        // If allergen detected, create allergen discovery
        if (!string.IsNullOrEmpty(allergenDetected) && disposalReason == "CausedAllergy")
        {
            // Get the history ID we just created
            const string getHistorySql = @"
                SELECT TOP 1 Id 
                FROM InventoryHistory 
                WHERE InventoryItemId = @ItemId 
                ORDER BY CreatedAt DESC";

            Guid historyId;
            await using (var command = new SqlCommand(getHistorySql, connection))
            {
                command.Parameters.AddWithValue("@ItemId", itemId);
                historyId = (Guid)await command.ExecuteScalarAsync()!;
            }

            // Create allergen discovery for each detected allergen
            var allergens = allergenDetected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var allergen in allergens)
            {
                await CreateAllergenDiscoveryAsync(session.UserId, session.HouseholdId, historyId, productId, 
                    allergen, "Unknown", $"Discovered from disposed item in scan session {sessionId}");
            }
        }

        // Update session item count
        await UpdateScanSessionItemCountAsync(sessionId, session.ItemsScanned + 1);

        return itemId;
    }

    private ScanSessionDto MapScanSession(SqlDataReader reader)
    {
        return new ScanSessionDto
        {
            Id = reader.GetGuid(0),
            UserId = reader.GetGuid(1),
            HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
            SessionType = reader.GetString(3),
            StorageLocationId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
            StartedAt = reader.GetDateTime(5),
            EndedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            ItemsScanned = reader.GetInt32(7),
            IsActive = reader.GetBoolean(8),
            StorageLocationName = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }

    #endregion

    #region Allergen Discovery

    public async Task<Guid> CreateAllergenDiscoveryAsync(Guid userId, Guid? householdId, Guid inventoryHistoryId, 
        Guid? productId, string allergenName, string severity, string? notes)
    {
        const string sql = @"
            INSERT INTO AllergenDiscovery 
            (UserId, HouseholdId, InventoryHistoryId, ProductId, AllergenName, Severity, Notes, DiscoveredAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @InventoryHistoryId, @ProductId, @AllergenName, @Severity, @Notes, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@InventoryHistoryId", inventoryHistoryId);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@AllergenName", allergenName);
        command.Parameters.AddWithValue("@Severity", severity);
        command.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

        var discoveryId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Created allergen discovery {DiscoveryId} for allergen {AllergenName}", discoveryId, allergenName);
        return discoveryId;
    }

    public async Task<List<AllergenDiscoveryDto>> GetAllergenDiscoveriesAsync(Guid userId)
    {
        const string sql = @"
            SELECT 
                ad.Id, ad.UserId, ad.HouseholdId, ad.InventoryHistoryId, ad.ProductId,
                ad.AllergenName, ad.Severity, ad.AddedToProfile, ad.AddedToProfileAt, ad.Notes, ad.DiscoveredAt,
                ISNULL(p.Name, 'Unknown Product') AS ProductName
            FROM AllergenDiscovery ad
            LEFT JOIN ExpressRecipe.Products.Product p ON ad.ProductId = p.Id
            WHERE ad.UserId = @UserId
            ORDER BY ad.DiscoveredAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var discoveries = new List<AllergenDiscoveryDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            discoveries.Add(new AllergenDiscoveryDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                InventoryHistoryId = reader.GetGuid(3),
                ProductId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                AllergenName = reader.GetString(5),
                Severity = reader.GetString(6),
                AddedToProfile = reader.GetBoolean(7),
                AddedToProfileAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                DiscoveredAt = reader.GetDateTime(10),
                ProductName = reader.GetString(11)
            });
        }

        return discoveries;
    }

    public async Task MarkAllergenAddedToProfileAsync(Guid discoveryId)
    {
        const string sql = @"
            UPDATE AllergenDiscovery
            SET AddedToProfile = 1, AddedToProfileAt = GETUTCDATE()
            WHERE Id = @DiscoveryId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DiscoveryId", discoveryId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Marked allergen discovery {DiscoveryId} as added to profile", discoveryId);
    }

    #endregion
}
