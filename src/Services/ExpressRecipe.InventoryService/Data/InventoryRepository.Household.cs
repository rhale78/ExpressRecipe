using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.InventoryService.Data;

// Partial class for Household and Address management
public partial class InventoryRepository
{
    #region Household Management

    public async Task<Guid> CreateHouseholdAsync(Guid userId, string name, string? description)
    {
        const string sql = @"
            INSERT INTO Household (Name, Description, CreatedBy, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Description, @CreatedBy, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        try
        {
            Guid householdId;
            await using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CreatedBy", userId);

                householdId = (Guid)await command.ExecuteScalarAsync()!;
            }

            // Add creator as owner
            await AddHouseholdMemberInternalAsync(householdId, userId, "Owner", userId, connection, transaction);

            await transaction.CommitAsync();
            _logger.LogInformation("Created household {HouseholdId} for user {UserId}", householdId, userId);
            return householdId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> IsUserMemberOfHouseholdAsync(Guid householdId, Guid userId)
    {
        const string sql = @"
            SELECT COUNT(1) FROM HouseholdMember
            WHERE HouseholdId = @HouseholdId AND UserId = @UserId AND IsActive = 1";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);
        command.Parameters.AddWithValue("@UserId", userId);

        return (int)await command.ExecuteScalarAsync()! > 0;
    }

    public async Task<List<HouseholdDto>> GetUserHouseholdsAsync(Guid userId)
    {
        const string sql = @"
            SELECT 
                h.Id, h.Name, h.Description, h.CreatedBy, h.CreatedAt,
                (SELECT COUNT(*) FROM HouseholdMember WHERE HouseholdId = h.Id AND IsActive = 1) AS MemberCount,
                (SELECT COUNT(*) FROM Address WHERE HouseholdId = h.Id AND IsDeleted = 0) AS AddressCount
            FROM Household h
            INNER JOIN HouseholdMember hm ON h.Id = hm.HouseholdId
            WHERE hm.UserId = @UserId AND hm.IsActive = 1 AND h.IsDeleted = 0
            ORDER BY h.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var households = new List<HouseholdDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            households.Add(new HouseholdDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedBy = reader.GetGuid(3),
                CreatedAt = reader.GetDateTime(4),
                MemberCount = reader.GetInt32(5),
                AddressCount = reader.GetInt32(6)
            });
        }

        return households;
    }

    public async Task<HouseholdDto?> GetHouseholdByIdAsync(Guid householdId)
    {
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}household:{householdId}",
                async (ct) => await GetHouseholdByIdFromDbAsync(householdId),
                expiration: TimeSpan.FromMinutes(30));
        }

        return await GetHouseholdByIdFromDbAsync(householdId);
    }

    private async Task<HouseholdDto?> GetHouseholdByIdFromDbAsync(Guid householdId)
    {
        const string sql = @"
            SELECT 
                h.Id, h.Name, h.Description, h.CreatedBy, h.CreatedAt,
                (SELECT COUNT(*) FROM HouseholdMember WHERE HouseholdId = h.Id AND IsActive = 1) AS MemberCount,
                (SELECT COUNT(*) FROM Address WHERE HouseholdId = h.Id AND IsDeleted = 0) AS AddressCount
            FROM Household h
            WHERE h.Id = @HouseholdId AND h.IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new HouseholdDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedBy = reader.GetGuid(3),
                CreatedAt = reader.GetDateTime(4),
                MemberCount = reader.GetInt32(5),
                AddressCount = reader.GetInt32(6)
            };
        }

        return null;
    }

    public async Task<Guid> AddHouseholdMemberAsync(Guid householdId, Guid userId, string role, Guid invitedBy)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var newMemberId = await AddHouseholdMemberInternalAsync(householdId, userId, role, invitedBy, connection, null);

        // Evict the household cache — MemberCount is embedded in the cached HouseholdDto
        if (_cache != null)
            await _cache.RemoveAsync($"{CachePrefix}household:{householdId}");

        return newMemberId;
    }

    private async Task<Guid> AddHouseholdMemberInternalAsync(Guid householdId, Guid userId, string role, Guid invitedBy, SqlConnection connection, SqlTransaction? transaction)
    {
        const string sql = @"
            INSERT INTO HouseholdMember 
            (HouseholdId, UserId, Role, InvitedBy, JoinedAt, CanManageInventory, CanManageShopping, CanManageMembers)
            OUTPUT INSERTED.Id
            VALUES (@HouseholdId, @UserId, @Role, @InvitedBy, GETUTCDATE(), @CanManageInventory, @CanManageShopping, @CanManageMembers)";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@HouseholdId", householdId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Role", role);
        command.Parameters.AddWithValue("@InvitedBy", invitedBy);
        command.Parameters.AddWithValue("@CanManageInventory", role is "Owner" or "Admin" || role == "Member");
        command.Parameters.AddWithValue("@CanManageShopping", role is "Owner" or "Admin" || role == "Member");
        command.Parameters.AddWithValue("@CanManageMembers", role is "Owner" or "Admin");

        var memberId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Added user {UserId} to household {HouseholdId} as {Role}", userId, householdId, role);
        return memberId;
    }

    public async Task<List<HouseholdMemberDto>> GetHouseholdMembersAsync(Guid householdId)
    {
        const string sql = @"
            SELECT 
                hm.Id, hm.HouseholdId, hm.UserId, hm.Role, 
                hm.CanManageInventory, hm.CanManageShopping, hm.CanManageMembers,
                hm.JoinedAt, hm.IsActive,
                ISNULL(u.Email, 'Unknown') AS UserEmail
            FROM HouseholdMember hm
            LEFT JOIN ExpressRecipe.Auth.[User] u ON hm.UserId = u.Id
            WHERE hm.HouseholdId = @HouseholdId AND hm.IsActive = 1
            ORDER BY 
                CASE hm.Role 
                    WHEN 'Owner' THEN 1
                    WHEN 'Admin' THEN 2
                    WHEN 'Member' THEN 3
                    ELSE 4
                END, hm.JoinedAt";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

        var members = new List<HouseholdMemberDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            members.Add(new HouseholdMemberDto
            {
                Id = reader.GetGuid(0),
                HouseholdId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                Role = reader.GetString(3),
                CanManageInventory = reader.GetBoolean(4),
                CanManageShopping = reader.GetBoolean(5),
                CanManageMembers = reader.GetBoolean(6),
                JoinedAt = reader.GetDateTime(7),
                IsActive = reader.GetBoolean(8),
                UserEmail = reader.GetString(9)
            });
        }

        return members;
    }

    public async Task UpdateMemberPermissionsAsync(Guid memberId, bool canManageInventory, bool canManageShopping, bool canManageMembers)
    {
        const string sql = @"
            UPDATE HouseholdMember
            SET CanManageInventory = @CanManageInventory,
                CanManageShopping = @CanManageShopping,
                CanManageMembers = @CanManageMembers
            WHERE Id = @MemberId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MemberId", memberId);
        command.Parameters.AddWithValue("@CanManageInventory", canManageInventory);
        command.Parameters.AddWithValue("@CanManageShopping", canManageShopping);
        command.Parameters.AddWithValue("@CanManageMembers", canManageMembers);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Updated permissions for household member {MemberId}", memberId);
    }

    public async Task RemoveHouseholdMemberAsync(Guid memberId)
    {
        // Look up the household id before deactivating so we can evict the cache
        Guid? householdId = null;
        if (_cache != null)
        {
            const string selectSql = "SELECT HouseholdId FROM HouseholdMember WHERE Id = @MemberId";
            await using var selectConn = new SqlConnection(_connectionString);
            await selectConn.OpenAsync();
            await using var selectCmd = new SqlCommand(selectSql, selectConn);
            selectCmd.Parameters.AddWithValue("@MemberId", memberId);
            var result = await selectCmd.ExecuteScalarAsync();
            if (result is Guid hid) householdId = hid;
        }

        const string sql = "UPDATE HouseholdMember SET IsActive = 0 WHERE Id = @MemberId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MemberId", memberId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Removed household member {MemberId}", memberId);

        if (_cache != null && householdId.HasValue)
            await _cache.RemoveAsync($"{CachePrefix}household:{householdId.Value}");
    }

    #endregion

    #region Address Management

    public async Task<Guid> CreateAddressAsync(Guid householdId, string name, string street, string city, 
        string state, string zipCode, string country, decimal? latitude, decimal? longitude, bool isPrimary)
    {
        const string sql = @"
            INSERT INTO Address 
            (HouseholdId, Name, Street, City, State, ZipCode, Country, Latitude, Longitude, IsPrimary, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@HouseholdId, @Name, @Street, @City, @State, @ZipCode, @Country, @Latitude, @Longitude, @IsPrimary, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        try
        {
            // If this is primary, unset other primary addresses
            if (isPrimary)
            {
                const string unsetSql = "UPDATE Address SET IsPrimary = 0 WHERE HouseholdId = @HouseholdId";
                await using var unsetCommand = new SqlCommand(unsetSql, connection, transaction);
                unsetCommand.Parameters.AddWithValue("@HouseholdId", householdId);
                await unsetCommand.ExecuteNonQueryAsync();
            }

            Guid addressId;
            await using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@HouseholdId", householdId);
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@Street", street);
                command.Parameters.AddWithValue("@City", city);
                command.Parameters.AddWithValue("@State", state);
                command.Parameters.AddWithValue("@ZipCode", zipCode);
                command.Parameters.AddWithValue("@Country", country);
                command.Parameters.AddWithValue("@Latitude", latitude.HasValue ? latitude.Value : DBNull.Value);
                command.Parameters.AddWithValue("@Longitude", longitude.HasValue ? longitude.Value : DBNull.Value);
                command.Parameters.AddWithValue("@IsPrimary", isPrimary);

                addressId = (Guid)await command.ExecuteScalarAsync()!;
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Created address {AddressId} for household {HouseholdId}", addressId, householdId);
            return addressId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<AddressDto>> GetHouseholdAddressesAsync(Guid householdId)
    {
        const string sql = @"
            SELECT 
                a.Id, a.HouseholdId, a.Name, a.Street, a.City, a.State, a.ZipCode, a.Country,
                a.Latitude, a.Longitude, a.IsPrimary, a.CreatedAt,
                (SELECT COUNT(*) FROM StorageLocation WHERE AddressId = a.Id AND IsDeleted = 0) AS StorageLocationCount,
                (SELECT COUNT(*) FROM InventoryItem i 
                 INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id 
                 WHERE s.AddressId = a.Id AND i.IsDeleted = 0) AS ItemCount
            FROM Address a
            WHERE a.HouseholdId = @HouseholdId AND a.IsDeleted = 0
            ORDER BY a.IsPrimary DESC, a.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

        var addresses = new List<AddressDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            addresses.Add(new AddressDto
            {
                Id = reader.GetGuid(0),
                HouseholdId = reader.GetGuid(1),
                Name = reader.GetString(2),
                Street = reader.GetString(3),
                City = reader.GetString(4),
                State = reader.GetString(5),
                ZipCode = reader.GetString(6),
                Country = reader.GetString(7),
                Latitude = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                Longitude = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                IsPrimary = reader.GetBoolean(10),
                CreatedAt = reader.GetDateTime(11),
                StorageLocationCount = reader.GetInt32(12),
                ItemCount = reader.GetInt32(13)
            });
        }

        return addresses;
    }

    public async Task<AddressDto?> GetAddressByIdAsync(Guid addressId)
    {
        const string sql = @"
            SELECT 
                a.Id, a.HouseholdId, a.Name, a.Street, a.City, a.State, a.ZipCode, a.Country,
                a.Latitude, a.Longitude, a.IsPrimary, a.CreatedAt,
                (SELECT COUNT(*) FROM StorageLocation WHERE AddressId = a.Id AND IsDeleted = 0) AS StorageLocationCount,
                (SELECT COUNT(*) FROM InventoryItem i 
                 INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id 
                 WHERE s.AddressId = a.Id AND i.IsDeleted = 0) AS ItemCount
            FROM Address a
            WHERE a.Id = @AddressId AND a.IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AddressId", addressId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AddressDto
            {
                Id = reader.GetGuid(0),
                HouseholdId = reader.GetGuid(1),
                Name = reader.GetString(2),
                Street = reader.GetString(3),
                City = reader.GetString(4),
                State = reader.GetString(5),
                ZipCode = reader.GetString(6),
                Country = reader.GetString(7),
                Latitude = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                Longitude = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                IsPrimary = reader.GetBoolean(10),
                CreatedAt = reader.GetDateTime(11),
                StorageLocationCount = reader.GetInt32(12),
                ItemCount = reader.GetInt32(13)
            };
        }

        return null;
    }

    public async Task<AddressDto?> DetectNearestAddressAsync(Guid householdId, decimal latitude, decimal longitude, double maxDistanceKm = 1.0)
    {
        // Haversine formula for distance calculation
        const string sql = @"
            SELECT TOP 1
                a.Id, a.HouseholdId, a.Name, a.Street, a.City, a.State, a.ZipCode, a.Country,
                a.Latitude, a.Longitude, a.IsPrimary, a.CreatedAt,
                (SELECT COUNT(*) FROM StorageLocation WHERE AddressId = a.Id AND IsDeleted = 0) AS StorageLocationCount,
                (SELECT COUNT(*) FROM InventoryItem i 
                 INNER JOIN StorageLocation s ON i.StorageLocationId = s.Id 
                 WHERE s.AddressId = a.Id AND i.IsDeleted = 0) AS ItemCount,
                (6371 * ACOS(
                    COS(RADIANS(@Latitude)) * COS(RADIANS(a.Latitude)) * 
                    COS(RADIANS(a.Longitude) - RADIANS(@Longitude)) + 
                    SIN(RADIANS(@Latitude)) * SIN(RADIANS(a.Latitude))
                )) AS DistanceKm
            FROM Address a
            WHERE a.HouseholdId = @HouseholdId 
              AND a.IsDeleted = 0
              AND a.Latitude IS NOT NULL 
              AND a.Longitude IS NOT NULL
            HAVING (6371 * ACOS(
                    COS(RADIANS(@Latitude)) * COS(RADIANS(a.Latitude)) * 
                    COS(RADIANS(a.Longitude) - RADIANS(@Longitude)) + 
                    SIN(RADIANS(@Latitude)) * SIN(RADIANS(a.Latitude))
                )) <= @MaxDistanceKm
            ORDER BY DistanceKm";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);
        command.Parameters.AddWithValue("@Latitude", latitude);
        command.Parameters.AddWithValue("@Longitude", longitude);
        command.Parameters.AddWithValue("@MaxDistanceKm", maxDistanceKm);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AddressDto
            {
                Id = reader.GetGuid(0),
                HouseholdId = reader.GetGuid(1),
                Name = reader.GetString(2),
                Street = reader.GetString(3),
                City = reader.GetString(4),
                State = reader.GetString(5),
                ZipCode = reader.GetString(6),
                Country = reader.GetString(7),
                Latitude = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                Longitude = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                IsPrimary = reader.GetBoolean(10),
                CreatedAt = reader.GetDateTime(11),
                StorageLocationCount = reader.GetInt32(12),
                ItemCount = reader.GetInt32(13),
                DistanceKm = reader.GetDouble(14)
            };
        }

        return null;
    }

    public async Task UpdateAddressCoordinatesAsync(Guid addressId, decimal latitude, decimal longitude)
    {
        const string sql = @"
            UPDATE Address
            SET Latitude = @Latitude, Longitude = @Longitude, UpdatedAt = GETUTCDATE()
            WHERE Id = @AddressId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AddressId", addressId);
        command.Parameters.AddWithValue("@Latitude", latitude);
        command.Parameters.AddWithValue("@Longitude", longitude);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Updated coordinates for address {AddressId}", addressId);
    }

    public async Task SetPrimaryAddressAsync(Guid householdId, Guid addressId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        try
        {
            // Unset all primary addresses for household
            const string unsetSql = "UPDATE Address SET IsPrimary = 0 WHERE HouseholdId = @HouseholdId";
            await using (var command = new SqlCommand(unsetSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@HouseholdId", householdId);
                await command.ExecuteNonQueryAsync();
            }

            // Set new primary
            const string setSql = "UPDATE Address SET IsPrimary = 1, UpdatedAt = GETUTCDATE() WHERE Id = @AddressId AND HouseholdId = @HouseholdId";
            await using (var command = new SqlCommand(setSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@AddressId", addressId);
                command.Parameters.AddWithValue("@HouseholdId", householdId);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Set primary address {AddressId} for household {HouseholdId}", addressId, householdId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAddressAsync(Guid addressId)
    {
        const string sql = "UPDATE Address SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @AddressId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AddressId", addressId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Deleted address {AddressId}", addressId);
    }

    #endregion
}
