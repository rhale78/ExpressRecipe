using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

public sealed class StorageLocationExtendedRepository : IStorageLocationExtendedRepository
{
    private readonly string _connectionString;

    public StorageLocationExtendedRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<StorageLocationSuggestionDto>> SuggestLocationsAsync(
        Guid householdId, string storageType, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT s.Id, s.Name, s.StorageType, s.OutageActive,
                   (SELECT COUNT(*) FROM InventoryItem WHERE StorageLocationId = s.Id AND IsDeleted = 0) AS ItemCount
            FROM StorageLocation s
            WHERE s.HouseholdId = @HouseholdId
              AND s.IsDeleted = 0
              AND s.StorageType = @StorageType
              AND s.OutageActive = 0
            ORDER BY ItemCount ASC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@HouseholdId", System.Data.SqlDbType.UniqueIdentifier) { Value = householdId });
        command.Parameters.Add(new SqlParameter("@StorageType", System.Data.SqlDbType.NVarChar, 50) { Value = storageType });

        List<StorageLocationSuggestionDto> results = new List<StorageLocationSuggestionDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            int itemCount = reader.GetInt32(reader.GetOrdinal("ItemCount"));
            results.Add(new StorageLocationSuggestionDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                StorageType = reader.GetString(reader.GetOrdinal("StorageType")),
                OutageActive = reader.GetBoolean(reader.GetOrdinal("OutageActive")),
                ItemCount = itemCount,
                Score = 100 - Math.Min(itemCount, 99)
            });
        }

        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        return results;
    }

    public async Task<List<StorageLocationDto>> GetLocationsWithOutageAsync(
        Guid householdId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT s.Id, s.UserId, s.HouseholdId, s.AddressId, s.Name, s.Description,
                   s.Temperature, s.IsDefault, s.CreatedAt,
                   a.Name AS AddressName,
                   (SELECT COUNT(*) FROM InventoryItem WHERE StorageLocationId = s.Id AND IsDeleted = 0) AS ItemCount
            FROM StorageLocation s
            LEFT JOIN Address a ON s.AddressId = a.Id
            WHERE s.HouseholdId = @HouseholdId
              AND s.IsDeleted = 0
              AND s.OutageActive = 1";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@HouseholdId", System.Data.SqlDbType.UniqueIdentifier) { Value = householdId });

        List<StorageLocationDto> results = new List<StorageLocationDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new StorageLocationDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                HouseholdId = reader.IsDBNull(reader.GetOrdinal("HouseholdId")) ? null : reader.GetGuid(reader.GetOrdinal("HouseholdId")),
                AddressId = reader.IsDBNull(reader.GetOrdinal("AddressId")) ? null : reader.GetGuid(reader.GetOrdinal("AddressId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                Temperature = reader.IsDBNull(reader.GetOrdinal("Temperature")) ? null : reader.GetString(reader.GetOrdinal("Temperature")),
                IsDefault = reader.GetBoolean(reader.GetOrdinal("IsDefault")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                AddressName = reader.IsDBNull(reader.GetOrdinal("AddressName")) ? null : reader.GetString(reader.GetOrdinal("AddressName")),
                ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount"))
            });
        }
        return results;
    }

    public async Task MarkOutageAsync(Guid locationId, string outageType, string? notes, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE StorageLocation
            SET OutageActive = 1,
                OutageType = @OutageType,
                OutageStartedAt = GETUTCDATE(),
                OutageNotes = @Notes,
                OutageSafetyWarningSent = 0,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", System.Data.SqlDbType.UniqueIdentifier) { Value = locationId });
        command.Parameters.Add(new SqlParameter("@OutageType", System.Data.SqlDbType.NVarChar, 50) { Value = outageType });
        command.Parameters.Add(new SqlParameter("@Notes", System.Data.SqlDbType.NVarChar) { Value = notes ?? (object)DBNull.Value });

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task ClearOutageAsync(Guid locationId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE StorageLocation
            SET OutageActive = 0,
                OutageType = NULL,
                OutageStartedAt = NULL,
                OutageNotes = NULL,
                OutageSafetyWarningSent = 0,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", System.Data.SqlDbType.UniqueIdentifier) { Value = locationId });

        await command.ExecuteNonQueryAsync(ct);
    }
}
