using ExpressRecipe.Data.Common;
using ExpressRecipe.ProfileService.Contracts.Requests;
using ExpressRecipe.ProfileService.Contracts.Responses;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.ProfileService.Data;

public class HouseholdMemberRepository : SqlHelper, IHouseholdMemberRepository
{
    private readonly HybridCacheService? _cache;
    private const string CachePrefix = "member:";

    public HouseholdMemberRepository(string connectionString, HybridCacheService? cache = null)
        : base(connectionString)
    {
        _cache = cache;
    }

    public async Task<List<HouseholdMemberDto>> GetByHouseholdIdAsync(Guid householdId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, HouseholdId, MemberType, DisplayName, BirthYear,
                   LinkedUserId, HasUserAccount, IsGuest, GuestSubtype,
                   GuestExpiresAt, SourceHouseholdId, CreatedAt
            FROM HouseholdMember
            WHERE HouseholdId = @HouseholdId AND IsDeleted = 0
            ORDER BY CreatedAt";

        return await ExecuteReaderAsync(
            sql,
            MapDto,
            CreateParameter("@HouseholdId", householdId));
    }

    public async Task<HouseholdMemberDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}id:{id}",
                async (_ct) => await GetByIdFromDbAsync(id),
                expiration: TimeSpan.FromMinutes(30),
                cancellationToken: ct);
        }

        return await GetByIdFromDbAsync(id);
    }

    private async Task<HouseholdMemberDto?> GetByIdFromDbAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, HouseholdId, MemberType, DisplayName, BirthYear,
                   LinkedUserId, HasUserAccount, IsGuest, GuestSubtype,
                   GuestExpiresAt, SourceHouseholdId, CreatedAt
            FROM HouseholdMember
            WHERE Id = @Id AND IsDeleted = 0";

        List<HouseholdMemberDto> results = await ExecuteReaderAsync(
            sql,
            MapDto,
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddMemberAsync(Guid householdId, AddMemberRequest request, Guid? createdBy = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO HouseholdMember (
                Id, HouseholdId, MemberType, DisplayName, BirthYear,
                LinkedUserId, HasUserAccount, IsGuest, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @HouseholdId, @MemberType, @DisplayName, @BirthYear,
                @LinkedUserId, @HasUserAccount, 0, @CreatedBy, GETUTCDATE()
            )";

        Guid memberId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", memberId),
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@MemberType", request.MemberType),
            CreateParameter("@DisplayName", request.DisplayName),
            CreateParameter("@BirthYear", request.BirthYear),
            CreateParameter("@LinkedUserId", request.LinkedUserId),
            CreateParameter("@HasUserAccount", false),
            CreateParameter("@CreatedBy", createdBy));

        return memberId;
    }

    public async Task<bool> UpdateMemberAsync(Guid memberId, UpdateMemberRequest request, Guid? updatedBy = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE HouseholdMember
            SET DisplayName    = COALESCE(@DisplayName, DisplayName),
                BirthYear      = CASE WHEN @BirthYear IS NOT NULL THEN @BirthYear ELSE BirthYear END,
                HasUserAccount = CASE WHEN @HasUserAccount IS NOT NULL THEN @HasUserAccount ELSE HasUserAccount END,
                UpdatedAt      = GETUTCDATE(),
                UpdatedBy      = @UpdatedBy
            WHERE Id = @Id AND IsDeleted = 0";

        int rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", memberId),
            CreateParameter("@DisplayName", string.IsNullOrWhiteSpace(request.DisplayName) ? null : (object)request.DisplayName),
            CreateParameter("@BirthYear", request.BirthYear),
            CreateParameter("@HasUserAccount", request.HasUserAccount),
            CreateParameter("@UpdatedBy", updatedBy));

        if (rowsAffected > 0 && _cache != null)
            await _cache.RemoveAsync($"{CachePrefix}id:{memberId}");

        return rowsAffected > 0;
    }

    public async Task<bool> SoftDeleteMemberAsync(Guid memberId, Guid? deletedBy = null, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE HouseholdMember
            SET IsDeleted = 1,
                UpdatedAt = GETUTCDATE(),
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id AND IsDeleted = 0";

        int rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", memberId),
            CreateParameter("@UpdatedBy", deletedBy));

        if (rowsAffected > 0 && _cache != null)
            await _cache.RemoveAsync($"{CachePrefix}id:{memberId}");

        return rowsAffected > 0;
    }

    public async Task<Guid> AddTemporaryVisitorAsync(Guid householdId, AddTemporaryVisitorRequest request, Guid? createdBy = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO HouseholdMember (
                Id, HouseholdId, MemberType, DisplayName, HasUserAccount,
                IsGuest, GuestSubtype, GuestExpiresAt, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @HouseholdId, 'TemporaryVisitor', @DisplayName, 0,
                1, 'Temporary', @GuestExpiresAt, @CreatedBy, GETUTCDATE()
            )";

        Guid memberId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", memberId),
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@DisplayName", request.DisplayName),
            CreateParameter("@GuestExpiresAt", request.GuestExpiresAt),
            CreateParameter("@CreatedBy", createdBy));

        return memberId;
    }

    public async Task<Guid> AddCrossHouseholdGuestAsync(Guid householdId, AddCrossHouseholdGuestRequest request, Guid? createdBy = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO HouseholdMember (
                Id, HouseholdId, MemberType, DisplayName, HasUserAccount,
                IsGuest, GuestSubtype, SourceHouseholdId, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @HouseholdId, 'CrossHouseholdGuest', @DisplayName, 0,
                1, 'CrossHousehold', @SourceHouseholdId, @CreatedBy, GETUTCDATE()
            )";

        Guid memberId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", memberId),
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@DisplayName", request.DisplayName),
            CreateParameter("@SourceHouseholdId", request.SourceHouseholdId),
            CreateParameter("@CreatedBy", createdBy));

        return memberId;
    }

    public async Task<List<HouseholdMemberDto>> GetExpiredTemporaryVisitorsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, HouseholdId, MemberType, DisplayName, BirthYear,
                   LinkedUserId, HasUserAccount, IsGuest, GuestSubtype,
                   GuestExpiresAt, SourceHouseholdId, CreatedAt
            FROM HouseholdMember
            WHERE MemberType = 'TemporaryVisitor'
              AND GuestExpiresAt < GETUTCDATE()
              AND IsDeleted = 0";

        return await ExecuteReaderAsync(sql, MapDto);
    }

    public async Task PurgeExpiredTemporaryVisitorsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE HouseholdMember
            SET IsDeleted = 1,
                UpdatedAt = GETUTCDATE()
            WHERE MemberType = 'TemporaryVisitor'
              AND GuestExpiresAt < GETUTCDATE()
              AND IsDeleted = 0";

        await ExecuteNonQueryAsync(sql);
    }

    public async Task<bool> UpdateMemberTypeAsync(Guid memberId, string memberType, Guid? sourceHouseholdId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE HouseholdMember
            SET MemberType        = @MemberType,
                SourceHouseholdId = @SourceHouseholdId,
                UpdatedAt         = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        int rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", memberId),
            CreateParameter("@MemberType", memberType),
            CreateParameter("@SourceHouseholdId", sourceHouseholdId));

        return rowsAffected > 0;
    }

    private static HouseholdMemberDto MapDto(Microsoft.Data.SqlClient.SqlDataReader reader)
    {
        int? birthYearRaw = GetIntNullable(reader, "BirthYear");

        return new HouseholdMemberDto
        {
            Id                = GetGuid(reader, "Id"),
            HouseholdId       = GetGuid(reader, "HouseholdId"),
            MemberType        = GetString(reader, "MemberType") ?? string.Empty,
            DisplayName       = GetString(reader, "DisplayName") ?? string.Empty,
            BirthYear         = birthYearRaw.HasValue ? (short?)birthYearRaw.Value : null,
            LinkedUserId      = GetGuidNullable(reader, "LinkedUserId"),
            HasUserAccount    = GetBoolean(reader, "HasUserAccount"),
            IsGuest           = GetBoolean(reader, "IsGuest"),
            GuestSubtype      = GetString(reader, "GuestSubtype"),
            GuestExpiresAt    = GetNullableDateTime(reader, "GuestExpiresAt"),
            SourceHouseholdId = GetGuidNullable(reader, "SourceHouseholdId"),
            CreatedAt         = GetDateTime(reader, "CreatedAt")
        };
    }

    public async Task<IReadOnlyList<Guid>> DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        // Find all member IDs linked to this user before updating
        const string selectSql = @"
SELECT Id FROM HouseholdMember
WHERE LinkedUserId = @UserId AND IsDeleted = 0;";

        List<Guid> memberIds = await ExecuteReaderAsync<Guid>(
            selectSql,
            reader => GetGuid(reader, "Id"),
            CreateParameter("@UserId", userId));

        if (memberIds.Count > 0)
        {
            // Soft-delete the member record so household data (name, type, etc.) is preserved
            // for the household audit trail while the user's personal identifiers are cleared.
            // LinkedUserId = NULL removes the PII link (GDPR Article 17 erasure of personal data);
            // HasUserAccount = 0 prevents re-use of the slot; IsDeleted = 1 hides it from queries.
            const string deleteSql = @"
UPDATE HouseholdMember
SET IsDeleted      = 1,
    LinkedUserId   = NULL,
    HasUserAccount = 0
WHERE LinkedUserId = @UserId;";

            await ExecuteNonQueryAsync(deleteSql, CreateParameter("@UserId", userId));
        }

        return memberIds;
    }
}
