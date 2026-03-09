using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.InventoryService.Data;

public sealed class InventoryStorageReminderQuery : IInventoryStorageReminderQuery
{
    private readonly string _connectionString;

    public InventoryStorageReminderQuery(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<FreezerBurnRiskItem>> GetFreezerBurnRiskItemsAsync(CancellationToken ct = default)
    {
        // Returns items in Freezer storage that exceed food-type thresholds
        const string sql = @"
            SELECT ii.HouseholdId,
                   COALESCE(ii.CustomName, '') AS ItemName,
                   sl.Name AS LocationName,
                   DATEDIFF(day, ii.PurchaseDate, GETUTCDATE()) AS DaysInFreezer
            FROM InventoryItem ii
            JOIN StorageLocation sl ON sl.Id = ii.StorageLocationId
            WHERE sl.StorageType = 'Freezer'
              AND sl.OutageActive = 0
              AND ii.IsDeleted = 0
              AND ii.PurchaseDate IS NOT NULL
              AND ii.HouseholdId IS NOT NULL
              AND (
                (ii.CustomName LIKE '%ground%'
                    AND DATEDIFF(day, ii.PurchaseDate, GETUTCDATE()) > 90)
                OR
                ((ii.CustomName LIKE '%chicken%' OR ii.CustomName LIKE '%turkey%')
                    AND DATEDIFF(day, ii.PurchaseDate, GETUTCDATE()) > 180)
                OR
                DATEDIFF(day, ii.PurchaseDate, GETUTCDATE()) > 270
              )";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);

        List<FreezerBurnRiskItem> results = new List<FreezerBurnRiskItem>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new FreezerBurnRiskItem
            {
                HouseholdId = reader.GetGuid(reader.GetOrdinal("HouseholdId")),
                ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                LocationName = reader.GetString(reader.GetOrdinal("LocationName")),
                DaysInFreezer = reader.GetInt32(reader.GetOrdinal("DaysInFreezer"))
            });
        }
        return results;
    }

    public async Task<List<OutageStorageLocation>> GetActiveOutagesAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT sl.Id AS LocationId,
                   sl.HouseholdId,
                   sl.Name AS LocationName,
                   COALESCE(sl.StorageType, '') AS StorageType,
                   COALESCE(sl.OutageType, '') AS OutageType,
                   sl.OutageStartedAt,
                   sl.OutageSafetyWarningSent AS WarningSent
            FROM StorageLocation sl
            WHERE sl.OutageActive = 1
              AND sl.IsDeleted = 0
              AND sl.HouseholdId IS NOT NULL
              AND sl.OutageStartedAt IS NOT NULL";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);

        List<OutageStorageLocation> results = new List<OutageStorageLocation>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new OutageStorageLocation
            {
                LocationId = reader.GetGuid(reader.GetOrdinal("LocationId")),
                HouseholdId = reader.GetGuid(reader.GetOrdinal("HouseholdId")),
                LocationName = reader.GetString(reader.GetOrdinal("LocationName")),
                StorageType = reader.GetString(reader.GetOrdinal("StorageType")),
                OutageType = reader.GetString(reader.GetOrdinal("OutageType")),
                OutageStartedAt = reader.GetDateTime(reader.GetOrdinal("OutageStartedAt")),
                WarningSent = reader.GetBoolean(reader.GetOrdinal("WarningSent"))
            });
        }
        return results;
    }

    public async Task<int> GetItemCountInStorageAsync(Guid locationId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM InventoryItem
            WHERE StorageLocationId = @LocationId AND IsDeleted = 0";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.UniqueIdentifier) { Value = locationId });

        object? result = await command.ExecuteScalarAsync(ct);
        return result is int count ? count : 0;
    }

    public async Task MarkOutageWarningSentAsync(Guid locationId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE StorageLocation
            SET OutageSafetyWarningSent = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = locationId });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<PerishableInventoryItem>> GetPerishableItemsForRecipeAsync(
        Guid householdId, Guid recipeId, CancellationToken ct = default)
    {
        // NOTE: Full recipe-to-inventory linking would require calling RecipeService.
        // Returns all non-deleted perishable items for the household that are in Refrigerator/Counter.
        // This is consistent with the caller's filtering (StorageType is "Refrigerator" or "Counter").
        const string sql = @"
            SELECT ii.Id,
                   COALESCE(ii.CustomName, '') AS ItemName,
                   sl.StorageType,
                   NULL AS FoodCategory
            FROM InventoryItem ii
            JOIN StorageLocation sl ON sl.Id = ii.StorageLocationId
            WHERE ii.HouseholdId = @HouseholdId
              AND ii.IsDeleted = 0
              AND sl.StorageType IN ('Refrigerator', 'Counter')";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });

        List<PerishableInventoryItem> results = new List<PerishableInventoryItem>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new PerishableInventoryItem
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                StorageType = reader.IsDBNull(reader.GetOrdinal("StorageType")) ? null : reader.GetString(reader.GetOrdinal("StorageType")),
                FoodCategory = null
            });
        }
        return results;
    }
}
