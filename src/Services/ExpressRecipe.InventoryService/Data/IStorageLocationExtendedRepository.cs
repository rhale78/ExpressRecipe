using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

public sealed record StorageLocationExtendedDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public Guid? AddressId { get; init; }
    public string AddressName { get; init; } = string.Empty;  // joined from Address.Name
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Temperature { get; init; }           // Room|Cold|Frozen (existing)
    public string? StorageType { get; init; }           // Pantry|Freezer|Refrigerator|RootCellar|...
    public Guid? EquipmentInstanceId { get; init; }
    public string? EquipmentDisplayName { get; init; }  // joined from EquipmentInstance
    public bool IsDefault { get; init; }
    public bool OutageActive { get; init; }
    public DateTime? OutageStartedAt { get; init; }
    public string? OutageType { get; init; }
    public string? OutageNotes { get; init; }
    public List<string> FoodCategories { get; init; } = new();
}

public sealed record StorageLocationSuggestionDto
{
    public Guid StorageLocationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? StorageType { get; init; }
    public bool OutageActive { get; init; }
    public int MatchScore { get; init; }  // higher = better match for the food category
}

public interface IStorageLocationExtendedRepository
{
    Task<List<StorageLocationExtendedDto>> GetLocationsAsync(Guid householdId, Guid? addressId = null,
        CancellationToken ct = default);
    Task<StorageLocationExtendedDto?> GetLocationByIdAsync(Guid locationId, CancellationToken ct = default);
    Task UpdateStorageTypeAsync(Guid locationId, string? storageType,
        Guid? equipmentInstanceId, CancellationToken ct = default);
    Task SetFoodCategoriesAsync(Guid locationId, IEnumerable<string> categories,
        CancellationToken ct = default);
    Task<List<StorageLocationSuggestionDto>> SuggestLocationsAsync(Guid householdId,
        string foodCategory, CancellationToken ct = default);
    Task SetOutageAsync(Guid locationId, string outageType, string? notes, CancellationToken ct = default);
    Task ClearOutageAsync(Guid locationId, CancellationToken ct = default);
    Task<List<StorageLocationExtendedDto>> GetLocationsWithOutageAsync(CancellationToken ct = default);
}

public sealed class StorageLocationExtendedRepository : IStorageLocationExtendedRepository
{
    private readonly string _connectionString;

    public StorageLocationExtendedRepository(string connectionString)
    { _connectionString = connectionString; }

    public async Task<List<StorageLocationExtendedDto>> GetLocationsAsync(Guid householdId,
        Guid? addressId = null, CancellationToken ct = default)
    {
        string sql = @"SELECT sl.Id,sl.HouseholdId,sl.AddressId,a.Name AS AddrName,
                   sl.Name,sl.Description,sl.Temperature,sl.StorageType,
                   sl.EquipmentInstanceId,
                   COALESCE(ei.CustomName, t.Name) AS EqName,
                   sl.IsDefault,sl.OutageActive,sl.OutageStartedAt,sl.OutageType,sl.OutageNotes,
                   fc.FoodCategory
            FROM StorageLocation sl
            LEFT JOIN Address a ON a.Id=sl.AddressId
            LEFT JOIN EquipmentInstance ei ON ei.Id=sl.EquipmentInstanceId
            LEFT JOIN EquipmentTemplate t ON t.Id=ei.TemplateId
            LEFT JOIN StorageLocationFoodCategory fc ON fc.StorageLocationId=sl.Id
            WHERE sl.HouseholdId=@HouseholdId AND sl.IsDeleted=0"
            + (addressId.HasValue ? " AND sl.AddressId=@AddressId" : "")
            + " ORDER BY a.Name,sl.Name";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        if (addressId.HasValue) { cmd.Parameters.AddWithValue("@AddressId", addressId.Value); }
        return await ReadStorageLocationsAsync(cmd, ct);
    }

    public async Task<StorageLocationExtendedDto?> GetLocationByIdAsync(Guid locationId,
        CancellationToken ct = default)
    {
        const string sql = @"SELECT sl.Id,sl.HouseholdId,sl.AddressId,a.Name AS AddrName,
                   sl.Name,sl.Description,sl.Temperature,sl.StorageType,
                   sl.EquipmentInstanceId,
                   COALESCE(ei.CustomName, t.Name) AS EqName,
                   sl.IsDefault,sl.OutageActive,sl.OutageStartedAt,sl.OutageType,sl.OutageNotes,
                   fc.FoodCategory
            FROM StorageLocation sl
            LEFT JOIN Address a ON a.Id=sl.AddressId
            LEFT JOIN EquipmentInstance ei ON ei.Id=sl.EquipmentInstanceId
            LEFT JOIN EquipmentTemplate t ON t.Id=ei.TemplateId
            LEFT JOIN StorageLocationFoodCategory fc ON fc.StorageLocationId=sl.Id
            WHERE sl.Id=@Id AND sl.IsDeleted=0";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", locationId);
        List<StorageLocationExtendedDto> results = await ReadStorageLocationsAsync(cmd, ct);
        return results.FirstOrDefault();
    }

    public async Task<List<StorageLocationSuggestionDto>> SuggestLocationsAsync(
        Guid householdId, string foodCategory, CancellationToken ct = default)
    {
        // Returns storage locations ordered by: exact food category match first,
        // then by StorageType temperature alignment, then all others.
        const string sql = @"SELECT sl.Id, sl.Name, sl.StorageType, sl.OutageActive,
                   CASE WHEN fc.FoodCategory IS NOT NULL THEN 2
                        WHEN sl.StorageType='Freezer' AND @Cat IN ('Meat','Poultry','Seafood','Frozen') THEN 1
                        WHEN sl.StorageType='Refrigerator' AND @Cat IN ('Dairy','Produce','Eggs') THEN 1
                        WHEN sl.StorageType='Pantry' AND @Cat IN ('Canned','DryGoods','Condiments','Spices') THEN 1
                        ELSE 0 END AS MatchScore
            FROM StorageLocation sl
            LEFT JOIN StorageLocationFoodCategory fc ON fc.StorageLocationId=sl.Id AND fc.FoodCategory=@Cat
            WHERE sl.HouseholdId=@HouseholdId AND sl.IsDeleted=0
            ORDER BY MatchScore DESC, sl.Name";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@Cat", foodCategory);
        List<StorageLocationSuggestionDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new StorageLocationSuggestionDto
            {
                StorageLocationId = r.GetGuid(0),
                Name              = r.GetString(1),
                StorageType       = r.IsDBNull(2) ? null : r.GetString(2),
                OutageActive      = r.GetBoolean(3),
                MatchScore        = r.GetInt32(4)
            });
        }
        return results;
    }

    public async Task SetFoodCategoriesAsync(Guid locationId, IEnumerable<string> categories,
        CancellationToken ct = default)
    {
        // Deduplicate preserving order, then replace atomically inside a transaction
        List<string> deduped = categories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlTransaction tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            await using SqlCommand del = new(
                "DELETE FROM StorageLocationFoodCategory WHERE StorageLocationId=@Id", conn, tx);
            del.Parameters.AddWithValue("@Id", locationId);
            await del.ExecuteNonQueryAsync(ct);
            if (deduped.Count > 0)
            {
                await using SqlCommand ins = new(
                    "INSERT INTO StorageLocationFoodCategory (Id,StorageLocationId,FoodCategory) VALUES (NEWID(),@Id,@Cat)",
                    conn, tx);
                ins.Parameters.AddWithValue("@Id", locationId);
                ins.Parameters.Add("@Cat", System.Data.SqlDbType.NVarChar, 50);
                foreach (string cat in deduped)
                {
                    ins.Parameters["@Cat"].Value = cat;
                    await ins.ExecuteNonQueryAsync(ct);
                }
            }
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UpdateStorageTypeAsync(Guid locationId, string? storageType,
        Guid? equipmentInstanceId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(@"UPDATE StorageLocation
            SET StorageType=@Type, EquipmentInstanceId=@EqId, UpdatedAt=GETUTCDATE()
            WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Type", storageType ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@EqId", equipmentInstanceId.HasValue ? equipmentInstanceId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Id", locationId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetOutageAsync(Guid locationId, string outageType, string? notes,
        CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(@"UPDATE StorageLocation
            SET OutageActive=1, OutageStartedAt=GETUTCDATE(), OutageType=@Type, OutageNotes=@Notes, UpdatedAt=GETUTCDATE()
            WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Type", outageType);
        cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Id", locationId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ClearOutageAsync(Guid locationId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(@"UPDATE StorageLocation
            SET OutageActive=0, OutageStartedAt=NULL, OutageType=NULL, OutageNotes=NULL, UpdatedAt=GETUTCDATE()
            WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Id", locationId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<StorageLocationExtendedDto>> GetLocationsWithOutageAsync(CancellationToken ct = default)
    {
        const string sql = @"SELECT sl.Id,sl.HouseholdId,sl.AddressId,a.Name,
                   sl.Name,sl.Description,sl.Temperature,sl.StorageType,
                   sl.EquipmentInstanceId, COALESCE(ei.CustomName,t.Name),
                   sl.IsDefault,sl.OutageActive,sl.OutageStartedAt,sl.OutageType,sl.OutageNotes,
                   fc.FoodCategory
            FROM StorageLocation sl
            LEFT JOIN Address a ON a.Id=sl.AddressId
            LEFT JOIN EquipmentInstance ei ON ei.Id=sl.EquipmentInstanceId
            LEFT JOIN EquipmentTemplate t ON t.Id=ei.TemplateId
            LEFT JOIN StorageLocationFoodCategory fc ON fc.StorageLocationId=sl.Id
            WHERE sl.OutageActive=1 AND sl.IsDeleted=0";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        return await ReadStorageLocationsAsync(cmd, ct);
    }

    private static async Task<List<StorageLocationExtendedDto>> ReadStorageLocationsAsync(
        SqlCommand cmd, CancellationToken ct)
    {
        Dictionary<Guid, StorageLocationExtendedDto> map = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            Guid id = r.GetGuid(0);
            if (!map.TryGetValue(id, out StorageLocationExtendedDto? dto))
            {
                dto = new StorageLocationExtendedDto
                {
                    Id = id, HouseholdId = r.GetGuid(1), AddressId = r.IsDBNull(2) ? null : r.GetGuid(2),
                    AddressName = r.IsDBNull(3) ? "" : r.GetString(3), Name = r.GetString(4),
                    Description = r.IsDBNull(5) ? null : r.GetString(5),
                    Temperature = r.IsDBNull(6) ? null : r.GetString(6),
                    StorageType = r.IsDBNull(7) ? null : r.GetString(7),
                    EquipmentInstanceId = r.IsDBNull(8) ? null : r.GetGuid(8),
                    EquipmentDisplayName = r.IsDBNull(9) ? null : r.GetString(9),
                    IsDefault = r.GetBoolean(10), OutageActive = r.GetBoolean(11),
                    OutageStartedAt = r.IsDBNull(12) ? null : r.GetDateTime(12),
                    OutageType = r.IsDBNull(13) ? null : r.GetString(13),
                    OutageNotes = r.IsDBNull(14) ? null : r.GetString(14)
                };
                map[id] = dto;
            }
            if (!r.IsDBNull(15)) { dto.FoodCategories.Add(r.GetString(15)); }
        }
        return map.Values.ToList();
    }
}
