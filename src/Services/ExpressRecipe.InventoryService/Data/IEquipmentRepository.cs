using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

public interface IEquipmentRepository
{
    Task<List<EquipmentTemplateDto>> GetTemplatesAsync(CancellationToken ct = default);
    Task<List<EquipmentInstanceDto>> GetInstancesAsync(Guid householdId, Guid? addressId = null,
        bool activeOnly = true, CancellationToken ct = default);
    Task<EquipmentInstanceDto?> GetInstanceByIdAsync(Guid instanceId, CancellationToken ct = default);
    Task<Guid> AddInstanceAsync(Guid householdId, Guid? addressId, Guid? templateId,
        string? customName, string? brand, string? modelNumber, decimal? sizeValue,
        string? sizeUnit, string? notes, CancellationToken ct = default);
    Task UpdateInstanceAsync(Guid instanceId, string? customName, string? brand,
        string? modelNumber, decimal? sizeValue, string? sizeUnit,
        string? notes, bool isActive, CancellationToken ct = default);
    Task SetCapabilitiesAsync(Guid instanceId, IEnumerable<string> capabilities,
        CancellationToken ct = default);
    Task<List<string>> GetCapabilitiesAsync(Guid instanceId, CancellationToken ct = default);
    Task<List<EquipmentInstanceDto>> GetInstancesByCapabilityAsync(Guid householdId,
        string capability, CancellationToken ct = default);
}

public sealed record EquipmentTemplateDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsBuiltIn { get; init; }
    public List<string> DefaultCapabilities { get; init; } = new();
}

public sealed record EquipmentInstanceDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public Guid? AddressId { get; init; }
    public Guid? TemplateId { get; init; }
    public string? TemplateName { get; init; }
    public string? CustomName { get; init; }
    public string DisplayName => CustomName ?? TemplateName ?? "Unknown Equipment";
    public string? Brand { get; init; }
    public string? ModelNumber { get; init; }
    public decimal? SizeValue { get; init; }
    public string? SizeUnit { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public List<string> Capabilities { get; init; } = new();
}

public sealed class EquipmentRepository : IEquipmentRepository
{
    private readonly string _connectionString;

    public EquipmentRepository(string connectionString) { _connectionString = connectionString; }

    public async Task<List<EquipmentTemplateDto>> GetTemplatesAsync(CancellationToken ct = default)
    {
        const string sql = @"SELECT t.Id,t.Name,t.Category,t.IsBuiltIn,c.Capability
            FROM EquipmentTemplate t
            LEFT JOIN EquipmentTemplateCapability c ON c.TemplateId=t.Id
            WHERE t.IsActive=1 ORDER BY t.Category,t.Name";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        Dictionary<Guid, EquipmentTemplateDto> map = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            Guid id = r.GetGuid(0);
            if (!map.TryGetValue(id, out EquipmentTemplateDto? dto))
            {
                dto = new EquipmentTemplateDto
                { Id = id, Name = r.GetString(1), Category = r.GetString(2), IsBuiltIn = r.GetBoolean(3) };
                map[id] = dto;
            }
            if (!r.IsDBNull(4)) { dto.DefaultCapabilities.Add(r.GetString(4)); }
        }
        return map.Values.ToList();
    }

    public async Task<List<EquipmentInstanceDto>> GetInstancesAsync(Guid householdId,
        Guid? addressId = null, bool activeOnly = true, CancellationToken ct = default)
    {
        string sql = @"SELECT ei.Id,ei.HouseholdId,ei.AddressId,ei.TemplateId,t.Name AS TemplateName,
                   ei.CustomName,ei.Brand,ei.ModelNumber,ei.SizeValue,ei.SizeUnit,ei.Notes,ei.IsActive,
                   c.Capability
            FROM EquipmentInstance ei
            LEFT JOIN EquipmentTemplate t ON t.Id=ei.TemplateId
            LEFT JOIN EquipmentInstanceCapability c ON c.InstanceId=ei.Id
            WHERE ei.HouseholdId=@HouseholdId"
            + (activeOnly ? " AND ei.IsActive=1" : "")
            + (addressId.HasValue ? " AND ei.AddressId=@AddressId" : "")
            + " ORDER BY t.Name,ei.CustomName";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        if (addressId.HasValue) { cmd.Parameters.AddWithValue("@AddressId", addressId.Value); }
        return await ReadInstancesAsync(cmd, ct);
    }

    public async Task<EquipmentInstanceDto?> GetInstanceByIdAsync(Guid instanceId, CancellationToken ct = default)
    {
        const string sql = @"SELECT ei.Id,ei.HouseholdId,ei.AddressId,ei.TemplateId,t.Name AS TemplateName,
                   ei.CustomName,ei.Brand,ei.ModelNumber,ei.SizeValue,ei.SizeUnit,ei.Notes,ei.IsActive,
                   c.Capability
            FROM EquipmentInstance ei
            LEFT JOIN EquipmentTemplate t ON t.Id=ei.TemplateId
            LEFT JOIN EquipmentInstanceCapability c ON c.InstanceId=ei.Id
            WHERE ei.Id=@InstanceId";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@InstanceId", instanceId);
        List<EquipmentInstanceDto> results = await ReadInstancesAsync(cmd, ct);
        return results.FirstOrDefault();
    }

    public async Task<List<EquipmentInstanceDto>> GetInstancesByCapabilityAsync(
        Guid householdId, string capability, CancellationToken ct = default)
    {
        const string sql = @"SELECT ei.Id,ei.HouseholdId,ei.AddressId,ei.TemplateId,t.Name AS TemplateName,
                   ei.CustomName,ei.Brand,ei.ModelNumber,ei.SizeValue,ei.SizeUnit,ei.Notes,ei.IsActive,
                   cap.Capability
            FROM EquipmentInstance ei
            JOIN EquipmentInstanceCapability cap ON cap.InstanceId=ei.Id AND cap.Capability=@Cap
            LEFT JOIN EquipmentTemplate t ON t.Id=ei.TemplateId
            LEFT JOIN EquipmentInstanceCapability c ON c.InstanceId=ei.Id
            WHERE ei.HouseholdId=@HouseholdId AND ei.IsActive=1";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@Cap", capability);
        return await ReadInstancesAsync(cmd, ct);
    }

    private static async Task<List<EquipmentInstanceDto>> ReadInstancesAsync(
        SqlCommand cmd, CancellationToken ct)
    {
        Dictionary<Guid, EquipmentInstanceDto> map = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            Guid id = r.GetGuid(0);
            if (!map.TryGetValue(id, out EquipmentInstanceDto? dto))
            {
                dto = new EquipmentInstanceDto
                {
                    Id = id, HouseholdId = r.GetGuid(1), AddressId = r.IsDBNull(2) ? null : r.GetGuid(2),
                    TemplateId = r.IsDBNull(3) ? null : r.GetGuid(3),
                    TemplateName = r.IsDBNull(4) ? null : r.GetString(4),
                    CustomName = r.IsDBNull(5) ? null : r.GetString(5),
                    Brand = r.IsDBNull(6) ? null : r.GetString(6),
                    ModelNumber = r.IsDBNull(7) ? null : r.GetString(7),
                    SizeValue = r.IsDBNull(8) ? null : r.GetDecimal(8),
                    SizeUnit = r.IsDBNull(9) ? null : r.GetString(9),
                    Notes = r.IsDBNull(10) ? null : r.GetString(10),
                    IsActive = r.GetBoolean(11)
                };
                map[id] = dto;
            }
            if (!r.IsDBNull(12)) { dto.Capabilities.Add(r.GetString(12)); }
        }
        return map.Values.ToList();
    }

    public async Task<Guid> AddInstanceAsync(Guid householdId, Guid? addressId, Guid? templateId,
        string? customName, string? brand, string? modelNumber, decimal? sizeValue,
        string? sizeUnit, string? notes, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO EquipmentInstance
            (Id,HouseholdId,AddressId,TemplateId,CustomName,Brand,ModelNumber,SizeValue,SizeUnit,Notes,CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (NEWID(),@HouseholdId,@AddressId,@TemplateId,@CustomName,@Brand,@ModelNumber,@SizeValue,@SizeUnit,@Notes,GETUTCDATE())";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@AddressId", addressId.HasValue ? addressId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@TemplateId", templateId.HasValue ? templateId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Brand", brand ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ModelNumber", modelNumber ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@SizeValue", sizeValue.HasValue ? sizeValue.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@SizeUnit", sizeUnit ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateInstanceAsync(Guid instanceId, string? customName, string? brand,
        string? modelNumber, decimal? sizeValue, string? sizeUnit,
        string? notes, bool isActive, CancellationToken ct = default)
    {
        const string sql = @"UPDATE EquipmentInstance SET
            CustomName=@CustomName, Brand=@Brand, ModelNumber=@ModelNumber,
            SizeValue=@SizeValue, SizeUnit=@SizeUnit, Notes=@Notes,
            IsActive=@IsActive, UpdatedAt=GETUTCDATE()
            WHERE Id=@Id";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", instanceId);
        cmd.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Brand", brand ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ModelNumber", modelNumber ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@SizeValue", sizeValue.HasValue ? sizeValue.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@SizeUnit", sizeUnit ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsActive", isActive);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // SetCapabilitiesAsync — full replace within a transaction: DELETE all then INSERT selected
    public async Task SetCapabilitiesAsync(Guid instanceId, IEnumerable<string> capabilities,
        CancellationToken ct = default)
    {
        List<string> capList = capabilities.ToList();
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlTransaction tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            await using SqlCommand del = new("DELETE FROM EquipmentInstanceCapability WHERE InstanceId=@Id", conn, tx);
            del.Parameters.AddWithValue("@Id", instanceId);
            await del.ExecuteNonQueryAsync(ct);
            if (capList.Count > 0)
            {
                await using SqlCommand ins = new(
                    "INSERT INTO EquipmentInstanceCapability (Id,InstanceId,Capability) VALUES (NEWID(),@Id,@Cap)",
                    conn, tx);
                ins.Parameters.AddWithValue("@Id", instanceId);
                ins.Parameters.Add("@Cap", System.Data.SqlDbType.NVarChar, 100);
                foreach (string cap in capList)
                {
                    ins.Parameters["@Cap"].Value = cap;
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

    public async Task<List<string>> GetCapabilitiesAsync(Guid instanceId, CancellationToken ct = default)
    {
        const string sql = "SELECT Capability FROM EquipmentInstanceCapability WHERE InstanceId=@Id ORDER BY Capability";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id", instanceId);
        List<string> caps = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            caps.Add(r.GetString(0));
        }
        return caps;
    }
}
