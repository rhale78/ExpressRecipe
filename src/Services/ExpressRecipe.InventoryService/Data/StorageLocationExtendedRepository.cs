using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.InventoryService.Data;

public sealed class StorageLocationExtendedRepository : IStorageLocationExtendedRepository
{
    private readonly string _connectionString;

    public StorageLocationExtendedRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<StorageLocationExtendedDto>> GetLocationsAsync(
        Guid householdId, Guid? addressId, CancellationToken ct = default)
    {
        string sql = @"
            SELECT s.Id, s.HouseholdId, s.AddressId, s.Name, s.StorageType, s.OutageActive
            FROM StorageLocation s
            WHERE s.HouseholdId = @HouseholdId
              AND s.IsDeleted = 0"
            + (addressId.HasValue ? " AND s.AddressId = @AddressId" : "")
            + " ORDER BY s.Name";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });
        if (addressId.HasValue)
            command.Parameters.Add(new SqlParameter("@AddressId", SqlDbType.UniqueIdentifier) { Value = addressId.Value });

        List<StorageLocationExtendedDto> results = new List<StorageLocationExtendedDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            Guid locationId = reader.GetGuid(reader.GetOrdinal("Id"));
            results.Add(new StorageLocationExtendedDto
            {
                Id          = locationId,
                HouseholdId = reader.GetGuid(reader.GetOrdinal("HouseholdId")),
                AddressId   = reader.IsDBNull(reader.GetOrdinal("AddressId")) ? null : reader.GetGuid(reader.GetOrdinal("AddressId")),
                Name        = reader.GetString(reader.GetOrdinal("Name")),
                StorageType = reader.IsDBNull(reader.GetOrdinal("StorageType")) ? null : reader.GetString(reader.GetOrdinal("StorageType")),
                OutageActive = reader.GetBoolean(reader.GetOrdinal("OutageActive")),
                FoodCategories = new List<string>()
            });
        }
        return results;
    }

    public async Task<StorageLocationExtendedDto?> GetLocationByIdAsync(
        Guid locationId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT s.Id, s.HouseholdId, s.AddressId, s.Name, s.StorageType, s.OutageActive
            FROM StorageLocation s
            WHERE s.Id = @Id AND s.IsDeleted = 0";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = locationId });

        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new StorageLocationExtendedDto
        {
            Id          = reader.GetGuid(reader.GetOrdinal("Id")),
            HouseholdId = reader.GetGuid(reader.GetOrdinal("HouseholdId")),
            AddressId   = reader.IsDBNull(reader.GetOrdinal("AddressId")) ? null : reader.GetGuid(reader.GetOrdinal("AddressId")),
            Name        = reader.GetString(reader.GetOrdinal("Name")),
            StorageType = reader.IsDBNull(reader.GetOrdinal("StorageType")) ? null : reader.GetString(reader.GetOrdinal("StorageType")),
            OutageActive = reader.GetBoolean(reader.GetOrdinal("OutageActive")),
            FoodCategories = new List<string>()
        };
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
        command.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });
        command.Parameters.Add(new SqlParameter("@StorageType", SqlDbType.NVarChar, 50) { Value = storageType });

        List<StorageLocationSuggestionDto> results = new List<StorageLocationSuggestionDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            int itemCount = reader.GetInt32(reader.GetOrdinal("ItemCount"));
            results.Add(new StorageLocationSuggestionDto
            {
                StorageLocationId = reader.GetGuid(reader.GetOrdinal("Id")),
                Name        = reader.GetString(reader.GetOrdinal("Name")),
                StorageType = reader.GetString(reader.GetOrdinal("StorageType")),
                OutageActive = reader.GetBoolean(reader.GetOrdinal("OutageActive")),
                ItemCount   = itemCount,
                MatchScore  = 100 - Math.Min(itemCount, 99)
            });
        }

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
        command.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });

        List<StorageLocationDto> results = new List<StorageLocationDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new StorageLocationDto
            {
                Id          = reader.GetGuid(reader.GetOrdinal("Id")),
                UserId      = reader.GetGuid(reader.GetOrdinal("UserId")),
                HouseholdId = reader.IsDBNull(reader.GetOrdinal("HouseholdId")) ? null : reader.GetGuid(reader.GetOrdinal("HouseholdId")),
                AddressId   = reader.IsDBNull(reader.GetOrdinal("AddressId")) ? null : reader.GetGuid(reader.GetOrdinal("AddressId")),
                Name        = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                Temperature = reader.IsDBNull(reader.GetOrdinal("Temperature")) ? null : reader.GetString(reader.GetOrdinal("Temperature")),
                IsDefault   = reader.GetBoolean(reader.GetOrdinal("IsDefault")),
                CreatedAt   = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                AddressName = reader.IsDBNull(reader.GetOrdinal("AddressName")) ? null : reader.GetString(reader.GetOrdinal("AddressName")),
                ItemCount   = reader.GetInt32(reader.GetOrdinal("ItemCount"))
            });
        }
        return results;
    }

    public async Task SetOutageAsync(Guid locationId, string outageType, string? notes, CancellationToken ct = default)
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
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = locationId });
        command.Parameters.Add(new SqlParameter("@OutageType", SqlDbType.NVarChar, 50) { Value = outageType });
        command.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar) { Value = notes ?? (object)DBNull.Value });
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
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = locationId });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateStorageTypeAsync(Guid locationId, string storageType, Guid? equipmentInstanceId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE StorageLocation
            SET StorageType = @StorageType,
                EquipmentInstanceId = @EquipmentInstanceId,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = locationId });
        command.Parameters.Add(new SqlParameter("@StorageType", SqlDbType.NVarChar, 50) { Value = storageType });
        command.Parameters.Add(new SqlParameter("@EquipmentInstanceId", SqlDbType.UniqueIdentifier)
            { Value = equipmentInstanceId.HasValue ? equipmentInstanceId.Value : DBNull.Value });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SetFoodCategoriesAsync(Guid locationId, List<string> categories, CancellationToken ct = default)
    {
        // Replace the categories list: delete all existing and insert new ones atomically.
        const string deleteSql = "DELETE FROM StorageLocationFoodCategory WHERE StorageLocationId = @LocationId";
        const string insertSql = "INSERT INTO StorageLocationFoodCategory (StorageLocationId, FoodCategory) VALUES (@LocationId, @Category)";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using (SqlCommand deleteCmd = new SqlCommand(deleteSql, connection, transaction))
            {
                deleteCmd.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.UniqueIdentifier) { Value = locationId });
                await deleteCmd.ExecuteNonQueryAsync(ct);
            }

            foreach (string category in categories)
            {
                await using SqlCommand insertCmd = new SqlCommand(insertSql, connection, transaction);
                insertCmd.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.UniqueIdentifier) { Value = locationId });
                insertCmd.Parameters.Add(new SqlParameter("@Category", SqlDbType.NVarChar, 100) { Value = category });
                await insertCmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
