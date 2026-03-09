using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

public interface IGardenRepository
{
    Task<bool> HasGardenAsync(Guid householdId, CancellationToken ct = default);
    Task SetHasGardenAsync(Guid householdId, bool hasGarden, string? notes, CancellationToken ct = default);
    Task<List<GardenPlantingDto>> GetPlantingsAsync(Guid householdId, CancellationToken ct = default);
    Task<Guid> AddPlantingAsync(Guid householdId, string plantName, string? varietyNotes, string plantType,
        DateOnly plantedDate, DateOnly? expectedRipeDate, int quantityPlanted, CancellationToken ct = default);
    Task UpdatePlantingAsync(Guid plantingId, DateOnly? expectedRipeDate, bool isActive, bool reminderEnabled, CancellationToken ct = default);
    Task<Guid> RecordHarvestAsync(Guid plantingId, decimal quantity, string unit, string? notes, CancellationToken ct = default);
    Task LinkHarvestToInventoryAsync(Guid harvestId, Guid inventoryItemId, CancellationToken ct = default);
    Task<List<GardenHarvestDto>> GetHarvestsAsync(Guid plantingId, CancellationToken ct = default);
    Task<List<GardenPlantingDto>> GetRipeCheckDuePlantingsAsync(int daysAhead, CancellationToken ct = default);
}

public sealed record GardenPlantingDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public string PlantName { get; init; } = string.Empty;
    public string? VarietyNotes { get; init; }
    public string PlantType { get; init; } = string.Empty;
    public DateOnly PlantedDate { get; init; }
    public DateOnly? ExpectedRipeDate { get; init; }
    public int QuantityPlanted { get; init; }
    public bool IsActive { get; init; }
    public bool RipeCheckReminderEnabled { get; init; }
    public string RipeStatus { get; init; } = string.Empty;  // Ready | Soon (≤3d) | Growing — computed
}

public sealed record GardenHarvestDto
{
    public Guid Id { get; init; }
    public DateOnly HarvestDate { get; init; }
    public decimal QuantityHarvested { get; init; }
    public string Unit { get; init; } = string.Empty;
    public bool AddedToInventory { get; init; }
}

public sealed class GardenRepository : IGardenRepository
{
    private readonly string _connectionString;

    public GardenRepository(string connectionString) { _connectionString = connectionString; }

    public async Task<bool> HasGardenAsync(Guid householdId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new("SELECT TOP 1 HasGarden FROM GardenProfile WHERE HouseholdId=@Id", conn);
        cmd.Parameters.AddWithValue("@Id", householdId);
        object? result = await cmd.ExecuteScalarAsync(ct);
        return result is not null && (bool)result;
    }

    public async Task SetHasGardenAsync(Guid householdId, bool hasGarden, string? notes, CancellationToken ct = default)
    {
        const string sql = @"MERGE GardenProfile AS t USING (SELECT @HouseholdId) AS s(HouseholdId)
            ON t.HouseholdId=s.HouseholdId
            WHEN MATCHED THEN UPDATE SET HasGarden=@HasGarden,GardenNotes=@Notes,UpdatedAt=GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (Id,HouseholdId,HasGarden,GardenNotes,CreatedAt)
                VALUES (NEWID(),@HouseholdId,@HasGarden,@Notes,GETUTCDATE());";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@HasGarden",   hasGarden);
        cmd.Parameters.AddWithValue("@Notes",       notes ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> AddPlantingAsync(Guid householdId, string plantName, string? varietyNotes,
        string plantType, DateOnly plantedDate, DateOnly? expectedRipeDate, int quantityPlanted, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO GardenPlanting
            (Id,HouseholdId,PlantName,VarietyNotes,PlantType,PlantedDate,ExpectedRipeDate,QuantityPlanted,IsActive,RipeCheckReminderEnabled,CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (NEWID(),@HouseholdId,@PlantName,@VarietyNotes,@PlantType,@PlantedDate,@ExpectedRipeDate,@QuantityPlanted,1,1,GETUTCDATE())";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId",      householdId);
        cmd.Parameters.AddWithValue("@PlantName",        plantName);
        cmd.Parameters.AddWithValue("@VarietyNotes",     varietyNotes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PlantType",        plantType);
        cmd.Parameters.AddWithValue("@PlantedDate",      plantedDate.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@ExpectedRipeDate", expectedRipeDate.HasValue ? expectedRipeDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@QuantityPlanted",  quantityPlanted);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdatePlantingAsync(Guid plantingId, DateOnly? expectedRipeDate, bool isActive, bool reminderEnabled, CancellationToken ct = default)
    {
        const string sql = @"UPDATE GardenPlanting SET ExpectedRipeDate=@ExpectedRipeDate,IsActive=@IsActive,
            RipeCheckReminderEnabled=@ReminderEnabled,UpdatedAt=GETUTCDATE() WHERE Id=@Id";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id",              plantingId);
        cmd.Parameters.AddWithValue("@ExpectedRipeDate", expectedRipeDate.HasValue ? expectedRipeDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsActive",         isActive);
        cmd.Parameters.AddWithValue("@ReminderEnabled",  reminderEnabled);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> RecordHarvestAsync(Guid plantingId, decimal quantity, string unit, string? notes, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO GardenHarvest (Id,PlantingId,HarvestDate,QuantityHarvested,Unit,Notes,CreatedAt)
            OUTPUT INSERTED.Id VALUES (NEWID(),@PlantingId,CAST(GETUTCDATE() AS DATE),@Qty,@Unit,@Notes,GETUTCDATE())";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlantingId", plantingId);
        cmd.Parameters.AddWithValue("@Qty",        quantity);
        cmd.Parameters.AddWithValue("@Unit",       unit);
        cmd.Parameters.AddWithValue("@Notes",      notes ?? (object)DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task LinkHarvestToInventoryAsync(Guid harvestId, Guid inventoryItemId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new("UPDATE GardenHarvest SET AddedToInventory=1,InventoryItemId=@InvId WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@InvId", inventoryItemId);
        cmd.Parameters.AddWithValue("@Id",    harvestId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<GardenHarvestDto>> GetHarvestsAsync(Guid plantingId, CancellationToken ct = default)
    {
        const string sql = @"SELECT Id,HarvestDate,QuantityHarvested,Unit,AddedToInventory
            FROM GardenHarvest WHERE PlantingId=@PlantingId ORDER BY HarvestDate DESC";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlantingId", plantingId);
        List<GardenHarvestDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new GardenHarvestDto
            {
                Id                = r.GetGuid(0),
                HarvestDate       = DateOnly.FromDateTime(r.GetDateTime(1)),
                QuantityHarvested = r.GetDecimal(2),
                Unit              = r.GetString(3),
                AddedToInventory  = r.GetBoolean(4)
            });
        }
        return results;
    }

    public async Task<List<GardenPlantingDto>> GetRipeCheckDuePlantingsAsync(int daysAhead, CancellationToken ct = default)
    {
        const string sql = @"SELECT Id,HouseholdId,PlantName,PlantType,PlantedDate,ExpectedRipeDate,QuantityPlanted
            FROM GardenPlanting WHERE IsActive=1 AND RipeCheckReminderEnabled=1
              AND ExpectedRipeDate BETWEEN CAST(GETUTCDATE() AS DATE) AND DATEADD(day,@DaysAhead,CAST(GETUTCDATE() AS DATE))";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@DaysAhead", daysAhead);
        List<GardenPlantingDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new GardenPlantingDto
            {
                Id              = r.GetGuid(0),
                HouseholdId     = r.GetGuid(1),
                PlantName       = r.GetString(2),
                PlantType       = r.GetString(3),
                PlantedDate     = DateOnly.FromDateTime(r.GetDateTime(4)),
                ExpectedRipeDate = r.IsDBNull(5) ? null : DateOnly.FromDateTime(r.GetDateTime(5)),
                QuantityPlanted = r.GetInt32(6)
            });
        }
        return results;
    }

    // GetPlantingsAsync — RipeStatus computed:
    //   ExpectedRipeDate <= today     → "Ready"
    //   ExpectedRipeDate <= today+3   → "Soon"
    //   else                          → "Growing"
    public async Task<List<GardenPlantingDto>> GetPlantingsAsync(Guid householdId, CancellationToken ct = default)
    {
        const string sql = @"SELECT Id,HouseholdId,PlantName,VarietyNotes,PlantType,PlantedDate,
                   ExpectedRipeDate,QuantityPlanted,IsActive,RipeCheckReminderEnabled
            FROM GardenPlanting WHERE HouseholdId=@HouseholdId AND IsActive=1 ORDER BY ExpectedRipeDate";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        List<GardenPlantingDto> results = new();
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            DateOnly? ripeDate = r.IsDBNull(6) ? null : DateOnly.FromDateTime(r.GetDateTime(6));
            results.Add(new GardenPlantingDto
            {
                Id                       = r.GetGuid(0),
                HouseholdId              = r.GetGuid(1),
                PlantName                = r.GetString(2),
                VarietyNotes             = r.IsDBNull(3) ? null : r.GetString(3),
                PlantType                = r.GetString(4),
                PlantedDate              = DateOnly.FromDateTime(r.GetDateTime(5)),
                ExpectedRipeDate         = ripeDate,
                QuantityPlanted          = r.GetInt32(7),
                IsActive                 = r.GetBoolean(8),
                RipeCheckReminderEnabled = r.GetBoolean(9),
                RipeStatus               = ripeDate is null ? "Growing" : ripeDate <= today ? "Ready" : ripeDate <= today.AddDays(3) ? "Soon" : "Growing"
            });
        }
        return results;
    }
}
