using ExpressRecipe.Data.Common;
using ExpressRecipe.SafeForkService.Models;

namespace ExpressRecipe.SafeForkService.Data;

public class AdaptationOverrideRepository : SqlHelper, IAdaptationOverrideRepository
{
    public AdaptationOverrideRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<AdaptationOverrideEntry>> GetAsync(Guid householdId, Guid? recipeInstanceId, Guid? memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, HouseholdId, RecipeInstanceId, MemberId, StrategyCode, CreatedAt, CreatedBy, UpdatedAt
            FROM AdaptationOverride
            WHERE HouseholdId = @HouseholdId
              AND (@RecipeInstanceId IS NULL OR RecipeInstanceId = @RecipeInstanceId)
              AND (@MemberId IS NULL OR MemberId = @MemberId)
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new AdaptationOverrideEntry
            {
                Id = GetGuid(reader, "Id"),
                HouseholdId = GetGuid(reader, "HouseholdId"),
                RecipeInstanceId = GetGuidNullable(reader, "RecipeInstanceId"),
                MemberId = GetGuidNullable(reader, "MemberId"),
                StrategyCode = GetString(reader, "StrategyCode") ?? string.Empty,
                CreatedAt = GetDateTime(reader, "CreatedAt"),
                CreatedBy = GetGuidNullable(reader, "CreatedBy"),
                UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
            },
            ct,
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@RecipeInstanceId", recipeInstanceId),
            CreateParameter("@MemberId", memberId));
    }

    public async Task<Guid> AddAsync(Guid householdId, Guid? recipeInstanceId, Guid? memberId, string strategyCode, Guid? createdBy, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO AdaptationOverride
                (Id, HouseholdId, RecipeInstanceId, MemberId, StrategyCode, CreatedBy, CreatedAt)
            VALUES
                (@Id, @HouseholdId, @RecipeInstanceId, @MemberId, @StrategyCode, @CreatedBy, GETUTCDATE())";

        Guid newId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            ct,
            CreateParameter("@Id", newId),
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@RecipeInstanceId", recipeInstanceId),
            CreateParameter("@MemberId", memberId),
            CreateParameter("@StrategyCode", strategyCode),
            CreateParameter("@CreatedBy", createdBy));

        return newId;
    }
}
