using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.InventoryService.Data;

public sealed class EquipmentRepository : IEquipmentRepository
{
    private readonly string _connectionString;

    public EquipmentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<EquipmentTemplateDto>> GetTemplatesAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT t.Id, t.Name, t.Category, t.Description,
                   c.CapabilityName
            FROM EquipmentTemplate t
            LEFT JOIN EquipmentCapabilityDef c ON c.TemplateId = t.Id AND c.IsDefault = 1
            WHERE t.IsActive = 1
            ORDER BY t.Category, t.Name, c.CapabilityName";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);

        Dictionary<Guid, EquipmentTemplateDto> map = new Dictionary<Guid, EquipmentTemplateDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            Guid id = reader.GetGuid(0);
            if (!map.TryGetValue(id, out EquipmentTemplateDto? dto))
            {
                dto = new EquipmentTemplateDto
                {
                    Id = id,
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DefaultCapabilities = new List<string>()
                };
                map[id] = dto;
            }
            if (!reader.IsDBNull(4))
            {
                dto.DefaultCapabilities.Add(reader.GetString(4));
            }
        }
        return new List<EquipmentTemplateDto>(map.Values);
    }

    public async Task<EquipmentTemplateDto?> GetTemplateByIdAsync(Guid templateId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT t.Id, t.Name, t.Category, t.Description, c.CapabilityName
            FROM EquipmentTemplate t
            LEFT JOIN EquipmentCapabilityDef c ON c.TemplateId = t.Id
            WHERE t.Id = @Id AND t.IsActive = 1
            ORDER BY c.CapabilityName";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = templateId });

        EquipmentTemplateDto? dto = null;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (dto == null)
            {
                dto = new EquipmentTemplateDto
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DefaultCapabilities = new List<string>()
                };
            }
            if (!reader.IsDBNull(4))
            {
                dto.DefaultCapabilities.Add(reader.GetString(4));
            }
        }
        return dto;
    }

    public async Task<Guid> CreateInstanceAsync(Guid householdId, Guid? addressId, Guid templateId,
        string? customName, string? brand, string? modelNumber,
        decimal? sizeValue, string? sizeUnit, string? notes,
        IEnumerable<string> capabilities, CancellationToken ct = default)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlTransaction tx = connection.BeginTransaction();

        try
        {
            const string insertSql = @"
                INSERT INTO EquipmentInstance
                    (HouseholdId, AddressId, TemplateId, CustomName, Brand, ModelNumber,
                     SizeValue, SizeUnit, Notes, IsActive, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@HouseholdId, @AddressId, @TemplateId, @CustomName, @Brand, @ModelNumber,
                     @SizeValue, @SizeUnit, @Notes, 1, GETUTCDATE())";

            await using SqlCommand insertCmd = new SqlCommand(insertSql, connection, tx);
            insertCmd.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });
            insertCmd.Parameters.Add(new SqlParameter("@AddressId", SqlDbType.UniqueIdentifier) { Value = addressId.HasValue ? addressId.Value : DBNull.Value });
            insertCmd.Parameters.Add(new SqlParameter("@TemplateId", SqlDbType.UniqueIdentifier) { Value = templateId });
            insertCmd.Parameters.Add(new SqlParameter("@CustomName", SqlDbType.NVarChar, 200) { Value = customName ?? (object)DBNull.Value });
            insertCmd.Parameters.Add(new SqlParameter("@Brand", SqlDbType.NVarChar, 200) { Value = brand ?? (object)DBNull.Value });
            insertCmd.Parameters.Add(new SqlParameter("@ModelNumber", SqlDbType.NVarChar, 100) { Value = modelNumber ?? (object)DBNull.Value });
            insertCmd.Parameters.Add(new SqlParameter("@SizeValue", SqlDbType.Decimal) { Value = sizeValue.HasValue ? sizeValue.Value : DBNull.Value, Precision = 10, Scale = 2 });
            insertCmd.Parameters.Add(new SqlParameter("@SizeUnit", SqlDbType.NVarChar, 50) { Value = sizeUnit ?? (object)DBNull.Value });
            insertCmd.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar) { Value = notes ?? (object)DBNull.Value });

            Guid instanceId = (Guid)(await insertCmd.ExecuteScalarAsync(ct))!;

            const string capSql = @"
                INSERT INTO EquipmentInstanceCapability (InstanceId, CapabilityName)
                VALUES (@InstanceId, @CapabilityName)";

            foreach (string cap in capabilities)
            {
                await using SqlCommand capCmd = new SqlCommand(capSql, connection, tx);
                capCmd.Parameters.Add(new SqlParameter("@InstanceId", SqlDbType.UniqueIdentifier) { Value = instanceId });
                capCmd.Parameters.Add(new SqlParameter("@CapabilityName", SqlDbType.NVarChar, 100) { Value = cap });
                await capCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return instanceId;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<EquipmentInstanceDto>> GetInstancesByHouseholdAsync(Guid householdId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT i.Id, i.HouseholdId, i.AddressId, i.TemplateId, t.Name AS TemplateName,
                   i.CustomName, i.Brand, i.ModelNumber, i.SizeValue, i.SizeUnit,
                   i.Notes, i.IsActive, i.CreatedAt, c.CapabilityName
            FROM EquipmentInstance i
            INNER JOIN EquipmentTemplate t ON t.Id = i.TemplateId
            LEFT JOIN EquipmentInstanceCapability c ON c.InstanceId = i.Id
            WHERE i.HouseholdId = @HouseholdId AND i.IsActive = 1
            ORDER BY i.CreatedAt, c.CapabilityName";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });

        return await ReadInstancesAsync(command, ct);
    }

    public async Task DeactivateInstanceAsync(Guid instanceId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE EquipmentInstance SET IsActive = 0, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = instanceId });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<EquipmentInstanceDto>> ResolveByCapabilityAsync(Guid householdId, string capability, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT i.Id, i.HouseholdId, i.AddressId, i.TemplateId, t.Name AS TemplateName,
                   i.CustomName, i.Brand, i.ModelNumber, i.SizeValue, i.SizeUnit,
                   i.Notes, i.IsActive, i.CreatedAt, c.CapabilityName
            FROM EquipmentInstance i
            INNER JOIN EquipmentTemplate t ON t.Id = i.TemplateId
            INNER JOIN EquipmentInstanceCapability c ON c.InstanceId = i.Id
            WHERE i.HouseholdId = @HouseholdId AND i.IsActive = 1
              AND EXISTS (
                  SELECT 1 FROM EquipmentInstanceCapability ec
                  WHERE ec.InstanceId = i.Id AND ec.CapabilityName = @Capability)
            ORDER BY i.CreatedAt, c.CapabilityName";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });
        command.Parameters.Add(new SqlParameter("@Capability", SqlDbType.NVarChar, 100) { Value = capability });

        return await ReadInstancesAsync(command, ct);
    }

    public async Task<List<EquipmentSubstituteDto>> FindSubstitutesAsync(Guid householdId, string equipmentName, CancellationToken ct = default)
    {
        // Find equipment that shares at least one capability with any instance whose template name matches
        const string sql = @"
            SELECT DISTINCT i.Id, COALESCE(i.CustomName, t.Name) AS InstanceName, c.CapabilityName
            FROM EquipmentInstance i
            INNER JOIN EquipmentTemplate t ON t.Id = i.TemplateId
            INNER JOIN EquipmentInstanceCapability c ON c.InstanceId = i.Id
            WHERE i.HouseholdId = @HouseholdId AND i.IsActive = 1
              AND i.Id NOT IN (
                  SELECT ei.Id FROM EquipmentInstance ei
                  INNER JOIN EquipmentTemplate et ON et.Id = ei.TemplateId
                  WHERE et.Name = @EquipmentName AND ei.HouseholdId = @HouseholdId AND ei.IsActive = 1)
              AND c.CapabilityName IN (
                  SELECT ec.CapabilityName
                  FROM EquipmentInstance src
                  INNER JOIN EquipmentTemplate st ON st.Id = src.TemplateId
                  INNER JOIN EquipmentInstanceCapability ec ON ec.InstanceId = src.Id
                  WHERE st.Name = @EquipmentName AND src.HouseholdId = @HouseholdId AND src.IsActive = 1)";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });
        command.Parameters.Add(new SqlParameter("@EquipmentName", SqlDbType.NVarChar, 200) { Value = equipmentName });

        List<EquipmentSubstituteDto> results = new List<EquipmentSubstituteDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new EquipmentSubstituteDto
            {
                InstanceId = reader.GetGuid(0),
                InstanceName = reader.GetString(1),
                Capability = reader.GetString(2)
            });
        }
        return results;
    }

    public async Task<List<EquipmentInstanceDto>> GetInstancesAsync(
        Guid householdId, Guid? addressId, bool? activeOnly, CancellationToken ct = default)
    {
        string whereExtra = "";
        if (addressId.HasValue)  whereExtra += " AND i.AddressId = @AddressId";
        if (activeOnly == true)  whereExtra += " AND i.IsActive = 1";
        if (activeOnly == false) whereExtra += " AND i.IsActive = 0";

        string sql = $@"
            SELECT i.Id, i.HouseholdId, i.AddressId, i.TemplateId, t.Name AS TemplateName,
                   i.CustomName, i.Brand, i.ModelNumber, i.SizeValue, i.SizeUnit,
                   i.Notes, i.IsActive, i.CreatedAt, c.CapabilityName
            FROM EquipmentInstance i
            INNER JOIN EquipmentTemplate t ON t.Id = i.TemplateId
            LEFT JOIN EquipmentInstanceCapability c ON c.InstanceId = i.Id
            WHERE i.HouseholdId = @HouseholdId{whereExtra}
            ORDER BY i.CreatedAt DESC, c.CapabilityName";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });
        if (addressId.HasValue)
            command.Parameters.Add(new SqlParameter("@AddressId", SqlDbType.UniqueIdentifier) { Value = addressId.Value });
        return await ReadInstancesAsync(command, ct);
    }

    public async Task<EquipmentInstanceDto?> GetInstanceByIdAsync(Guid instanceId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT i.Id, i.HouseholdId, i.AddressId, i.TemplateId, t.Name AS TemplateName,
                   i.CustomName, i.Brand, i.ModelNumber, i.SizeValue, i.SizeUnit,
                   i.Notes, i.IsActive, i.CreatedAt, c.CapabilityName
            FROM EquipmentInstance i
            INNER JOIN EquipmentTemplate t ON t.Id = i.TemplateId
            LEFT JOIN EquipmentInstanceCapability c ON c.InstanceId = i.Id
            WHERE i.Id = @Id";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = instanceId });
        List<EquipmentInstanceDto> results = await ReadInstancesAsync(command, ct);
        return results.FirstOrDefault();
    }

    public async Task<List<EquipmentInstanceDto>> GetInstancesByCapabilityAsync(
        Guid householdId, string capability, CancellationToken ct = default)
        => await ResolveByCapabilityAsync(householdId, capability, ct);

    public async Task<Guid> AddInstanceAsync(
        Guid householdId, Guid? addressId, Guid? templateId,
        string? customName, string? brand, string? modelNumber,
        decimal? sizeValue, string? sizeUnit, string? notes,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO EquipmentInstance
                (HouseholdId, AddressId, TemplateId, CustomName, Brand, ModelNumber,
                 SizeValue, SizeUnit, Notes, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@HouseholdId, @AddressId, @TemplateId, @CustomName, @Brand, @ModelNumber,
                 @SizeValue, @SizeUnit, @Notes, 1, GETUTCDATE())";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@HouseholdId", SqlDbType.UniqueIdentifier) { Value = householdId });
        cmd.Parameters.Add(new SqlParameter("@AddressId", SqlDbType.UniqueIdentifier) { Value = addressId.HasValue ? addressId.Value : DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@TemplateId", SqlDbType.UniqueIdentifier) { Value = templateId.HasValue ? templateId.Value : DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@CustomName", SqlDbType.NVarChar, 200) { Value = customName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Brand", SqlDbType.NVarChar, 200) { Value = brand ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ModelNumber", SqlDbType.NVarChar, 100) { Value = modelNumber ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@SizeValue", SqlDbType.Decimal) { Value = sizeValue.HasValue ? sizeValue.Value : DBNull.Value, Precision = 10, Scale = 2 });
        cmd.Parameters.Add(new SqlParameter("@SizeUnit", SqlDbType.NVarChar, 50) { Value = sizeUnit ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar) { Value = notes ?? (object)DBNull.Value });
        object? result = await cmd.ExecuteScalarAsync(ct);
        return (Guid)result!;
    }

    public async Task SetCapabilitiesAsync(Guid instanceId, IEnumerable<string> capabilities, CancellationToken ct = default)
    {
        const string deleteSql = "DELETE FROM EquipmentInstanceCapability WHERE InstanceId = @InstanceId";
        const string insertSql = "INSERT INTO EquipmentInstanceCapability (InstanceId, CapabilityName) VALUES (@InstanceId, @Cap)";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlTransaction tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using (SqlCommand del = new SqlCommand(deleteSql, connection, tx))
            {
                del.Parameters.Add(new SqlParameter("@InstanceId", SqlDbType.UniqueIdentifier) { Value = instanceId });
                await del.ExecuteNonQueryAsync(ct);
            }
            foreach (string cap in capabilities)
            {
                await using SqlCommand ins = new SqlCommand(insertSql, connection, tx);
                ins.Parameters.Add(new SqlParameter("@InstanceId", SqlDbType.UniqueIdentifier) { Value = instanceId });
                ins.Parameters.Add(new SqlParameter("@Cap", SqlDbType.NVarChar, 100) { Value = cap });
                await ins.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task UpdateInstanceAsync(Guid instanceId, string? customName, string? brand, string? modelNumber,
        decimal? sizeValue, string? sizeUnit, string? notes, bool isActive, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE EquipmentInstance
            SET CustomName = @CustomName, Brand = @Brand, ModelNumber = @ModelNumber,
                SizeValue = @SizeValue, SizeUnit = @SizeUnit, Notes = @Notes,
                IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier) { Value = instanceId });
        cmd.Parameters.Add(new SqlParameter("@CustomName", SqlDbType.NVarChar, 200) { Value = customName ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Brand", SqlDbType.NVarChar, 200) { Value = brand ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ModelNumber", SqlDbType.NVarChar, 100) { Value = modelNumber ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@SizeValue", SqlDbType.Decimal) { Value = sizeValue.HasValue ? sizeValue.Value : DBNull.Value, Precision = 10, Scale = 2 });
        cmd.Parameters.Add(new SqlParameter("@SizeUnit", SqlDbType.NVarChar, 50) { Value = sizeUnit ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar) { Value = notes ?? (object)DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = isActive });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<EquipmentInstanceDto>> ReadInstancesAsync(SqlCommand command, CancellationToken ct)    {
        Dictionary<Guid, EquipmentInstanceDto> map = new Dictionary<Guid, EquipmentInstanceDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            Guid id = reader.GetGuid(0);
            if (!map.TryGetValue(id, out EquipmentInstanceDto? dto))
            {
                dto = new EquipmentInstanceDto
                {
                    Id = id,
                    HouseholdId = reader.GetGuid(1),
                    AddressId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    TemplateId = reader.GetGuid(3),
                    TemplateName = reader.GetString(4),
                    CustomName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Brand = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ModelNumber = reader.IsDBNull(7) ? null : reader.GetString(7),
                    SizeValue = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                    SizeUnit = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
                    IsActive = reader.GetBoolean(11),
                    CreatedAt = reader.GetDateTime(12),
                    Capabilities = new List<string>()
                };
                map[id] = dto;
            }
            if (!reader.IsDBNull(13))
            {
                dto.Capabilities.Add(reader.GetString(13));
            }
        }
        return new List<EquipmentInstanceDto>(map.Values);
    }
}
