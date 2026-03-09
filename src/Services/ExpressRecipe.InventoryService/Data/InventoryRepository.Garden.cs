using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

// Partial class for Garden harvest and long-term storage inventory methods
public partial class InventoryRepository
{
    public async Task<Guid> CreateFromGardenHarvestAsync(Guid userId, Guid householdId, string plantName, decimal quantity,
        string unit, int freshnessDays, CancellationToken ct = default)
    {
        Guid storageLocationId = await GetOrCreateHouseholdDefaultStorageAsync(userId, householdId, ct);

        const string sql = @"
            INSERT INTO InventoryItem
            (UserId, HouseholdId, CustomName, StorageLocationId, Quantity, Unit, ExpirationDate,
             Source, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @CustomName, @StorageLocationId, @Quantity, @Unit,
                    DATEADD(day, @FreshnessDays, CAST(GETUTCDATE() AS DATE)),
                    'Garden', GETUTCDATE(), 0)";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",            userId);
        cmd.Parameters.AddWithValue("@HouseholdId",       householdId);
        cmd.Parameters.AddWithValue("@CustomName",        plantName);
        cmd.Parameters.AddWithValue("@StorageLocationId", storageLocationId);
        cmd.Parameters.AddWithValue("@Quantity",          quantity);
        cmd.Parameters.AddWithValue("@Unit",              unit);
        cmd.Parameters.AddWithValue("@FreshnessDays",     freshnessDays);

        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<Guid> AddItemAsync(AddItemRequest request, CancellationToken ct = default)
    {
        Guid storageLocationId = await GetOrCreateHouseholdDefaultStorageAsync(
            request.UserId, request.HouseholdId ?? Guid.Empty, ct);

        const string sql = @"
            INSERT INTO InventoryItem
            (UserId, HouseholdId, CustomName, StorageLocationId, Quantity, Unit, ExpirationDate,
             StorageMethod, IsLongTermStorage, StorageCapacityUnit, BatchLabel, Source, Temperature,
             CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @CustomName, @StorageLocationId, @Quantity, @Unit, @ExpirationDate,
                    @StorageMethod, @IsLongTermStorage, @StorageCapacityUnit, @BatchLabel, @Source, @Temperature,
                    GETUTCDATE(), 0)";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",              request.UserId);
        cmd.Parameters.AddWithValue("@HouseholdId",         request.HouseholdId.HasValue ? request.HouseholdId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@CustomName",          request.Name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@StorageLocationId",   storageLocationId);
        cmd.Parameters.AddWithValue("@Quantity",            request.Quantity);
        cmd.Parameters.AddWithValue("@Unit",                request.Unit ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ExpirationDate",      request.ExpirationDate.HasValue ? request.ExpirationDate.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@StorageMethod",       request.StorageMethod ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsLongTermStorage",   request.IsLongTermStorage);
        cmd.Parameters.AddWithValue("@StorageCapacityUnit", request.StorageCapacityUnit ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@BatchLabel",          request.BatchLabel ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Source",              request.Source ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Temperature",         request.Temperature ?? (object)DBNull.Value);

        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<List<InventoryItemDto>> GetItemsAsync(Guid householdId, bool isLongTermOnly = false,
        string? storageMethod = null, CancellationToken ct = default)
    {
        string whereClause = "i.HouseholdId = @HouseholdId AND i.IsDeleted = 0";
        if (isLongTermOnly) { whereClause += " AND i.IsLongTermStorage = 1"; }
        if (storageMethod != null) { whereClause += " AND i.StorageMethod = @StorageMethod"; }

        string sql = $@"
            SELECT
                i.Id, i.UserId, i.HouseholdId, i.ProductId, i.CustomName, i.StorageLocationId,
                i.Quantity, i.Unit, i.PurchaseDate, i.ExpirationDate, i.OpenedDate,
                i.Notes, i.Barcode, i.Price, i.Store, i.PreferredStore, i.StoreLocation,
                i.IsOpened, i.AddedBy, i.CreatedAt, i.UpdatedAt,
                s.Name AS StorageLocationName, s.AddressId,
                a.Name AS AddressName,
                i.StorageMethod, i.IsLongTermStorage, i.StorageCapacityUnit, i.BatchLabel, i.Source, i.Temperature
            FROM InventoryItem i
            INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE {whereClause}
            ORDER BY i.ExpirationDate ASC, i.CreatedAt DESC";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        if (storageMethod != null) { cmd.Parameters.AddWithValue("@StorageMethod", storageMethod); }

        List<InventoryItemDto> items = new();
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new InventoryItemDto
            {
                Id                  = reader.GetGuid(0),
                UserId              = reader.IsDBNull(1) ? Guid.Empty : reader.GetGuid(1),
                HouseholdId         = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                ProductId           = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                CustomName          = reader.IsDBNull(4) ? null : reader.GetString(4),
                StorageLocationId   = reader.GetGuid(5),
                Quantity            = reader.GetDecimal(6),
                Unit                = reader.IsDBNull(7) ? null : reader.GetString(7),
                PurchaseDate        = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                ExpirationDate      = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                OpenedDate          = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                Notes               = reader.IsDBNull(11) ? null : reader.GetString(11),
                Barcode             = reader.IsDBNull(12) ? null : reader.GetString(12),
                Price               = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                Store               = reader.IsDBNull(14) ? null : reader.GetString(14),
                PreferredStore      = reader.IsDBNull(15) ? null : reader.GetString(15),
                StoreLocation       = reader.IsDBNull(16) ? null : reader.GetString(16),
                IsOpened            = reader.GetBoolean(17),
                AddedBy             = reader.IsDBNull(18) ? null : reader.GetGuid(18),
                CreatedAt           = reader.GetDateTime(19),
                UpdatedAt           = reader.IsDBNull(20) ? null : reader.GetDateTime(20),
                StorageLocationName = reader.GetString(21),
                AddressId           = reader.IsDBNull(22) ? null : reader.GetGuid(22),
                AddressName         = reader.IsDBNull(23) ? null : reader.GetString(23),
                StorageMethod       = reader.IsDBNull(24) ? null : reader.GetString(24),
                IsLongTermStorage   = !reader.IsDBNull(25) && reader.GetBoolean(25),
                StorageCapacityUnit = reader.IsDBNull(26) ? null : reader.GetString(26),
                BatchLabel          = reader.IsDBNull(27) ? null : reader.GetString(27),
                Source              = reader.IsDBNull(28) ? null : reader.GetString(28),
                Temperature         = reader.IsDBNull(29) ? null : reader.GetString(29)
            });
        }
        return items;
    }

    private async Task<Guid> GetOrCreateHouseholdDefaultStorageAsync(Guid userId, Guid householdId, CancellationToken ct)
    {
        // Try to get the first non-deleted storage location for the household
        const string selectSql = @"
            SELECT TOP 1 Id FROM StorageLocation
            WHERE HouseholdId = @HouseholdId AND IsDeleted = 0
            ORDER BY IsDefault DESC, CreatedAt ASC";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand selectCmd = new(selectSql, conn);
        selectCmd.Parameters.AddWithValue("@HouseholdId", householdId);
        object? result = await selectCmd.ExecuteScalarAsync(ct);
        if (result is Guid existingId) { return existingId; }

        // Create a default "Pantry" storage location for the household
        const string insertSql = @"
            INSERT INTO StorageLocation (UserId, HouseholdId, Name, IsDefault, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, 'Pantry', 1, GETUTCDATE(), 0)";

        await using SqlCommand insertCmd = new(insertSql, conn);
        insertCmd.Parameters.AddWithValue("@UserId",      userId);
        insertCmd.Parameters.AddWithValue("@HouseholdId", householdId);
        return (Guid)(await insertCmd.ExecuteScalarAsync(ct))!;
    }
}
