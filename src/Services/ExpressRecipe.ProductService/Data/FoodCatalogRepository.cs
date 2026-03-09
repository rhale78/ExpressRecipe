using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ProductService.Data;

public interface IFoodCatalogRepository
{
    // Food groups
    Task<Guid> CreateFoodGroupAsync(FoodGroupRecord group, CancellationToken ct = default);
    Task<List<FoodGroupDto>> GetFoodGroupsAsync(string? search, string? functionalRole, CancellationToken ct = default);
    Task<FoodGroupDto?> GetFoodGroupByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> AddFoodGroupMemberAsync(FoodGroupMemberRecord member, CancellationToken ct = default);
    Task<List<FoodGroupMemberDto>> GetFoodGroupMembersAsync(Guid foodGroupId, CancellationToken ct = default);
    Task<List<FoodGroupMemberDto>> GetSubstitutesForIngredientAsync(Guid ingredientId, CancellationToken ct = default);
    Task<int> GetFoodGroupCountAsync(CancellationToken ct = default);

    // Substitution history
    Task<Guid> RecordSubstitutionAsync(SubstitutionHistoryRecord record, CancellationToken ct = default);
    Task<List<SubstitutionHistoryDto>> GetUserSubstitutionHistoryAsync(Guid userId, Guid ingredientId, CancellationToken ct = default);
}

public class FoodCatalogRepository : SqlHelper, IFoodCatalogRepository
{
    public FoodCatalogRepository(string connectionString) : base(connectionString)
    {
    }

    // -----------------------------------------------------------------------
    // Food Groups
    // -----------------------------------------------------------------------

    public async Task<Guid> CreateFoodGroupAsync(FoodGroupRecord group, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO FoodGroup (Name, Description, FunctionalRole)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Description, @FunctionalRole)";

        return await ExecuteScalarAsync<Guid>(sql,
            new SqlParameter("@Name", group.Name),
            new SqlParameter("@Description", (object?)group.Description ?? DBNull.Value),
            new SqlParameter("@FunctionalRole", (object?)group.FunctionalRole ?? DBNull.Value));
    }

    public async Task<List<FoodGroupDto>> GetFoodGroupsAsync(string? search, string? functionalRole, CancellationToken ct = default)
    {
        var conditions = new List<string> { "fg.IsActive = 1" };
        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(fg.Name LIKE @Search OR fg.Description LIKE @Search)");
            parameters.Add(new SqlParameter("@Search", $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(functionalRole))
        {
            conditions.Add("fg.FunctionalRole = @FunctionalRole");
            parameters.Add(new SqlParameter("@FunctionalRole", functionalRole));
        }

        var where = string.Join(" AND ", conditions);
        var sql = $@"
            SELECT
                fg.Id,
                fg.Name,
                fg.Description,
                fg.FunctionalRole,
                fg.IsActive,
                fg.CreatedAt,
                fg.UpdatedAt,
                COUNT(fgm.Id) AS MemberCount
            FROM FoodGroup fg
            LEFT JOIN FoodGroupMember fgm ON fgm.FoodGroupId = fg.Id AND fgm.IsActive = 1
            WHERE {where}
            GROUP BY fg.Id, fg.Name, fg.Description, fg.FunctionalRole, fg.IsActive, fg.CreatedAt, fg.UpdatedAt
            ORDER BY fg.Name";

        return await ExecuteReaderAsync(sql, MapFoodGroup, parameters.ToArray());
    }

    public async Task<FoodGroupDto?> GetFoodGroupByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                fg.Id,
                fg.Name,
                fg.Description,
                fg.FunctionalRole,
                fg.IsActive,
                fg.CreatedAt,
                fg.UpdatedAt,
                COUNT(fgm.Id) AS MemberCount
            FROM FoodGroup fg
            LEFT JOIN FoodGroupMember fgm ON fgm.FoodGroupId = fg.Id AND fgm.IsActive = 1
            WHERE fg.Id = @Id
            GROUP BY fg.Id, fg.Name, fg.Description, fg.FunctionalRole, fg.IsActive, fg.CreatedAt, fg.UpdatedAt";

        var results = await ExecuteReaderAsync(sql, MapFoodGroup, new SqlParameter("@Id", id));
        return results.FirstOrDefault();
    }

    public async Task<Guid> AddFoodGroupMemberAsync(FoodGroupMemberRecord member, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO FoodGroupMember (
                FoodGroupId, IngredientId, ProductId, CustomName,
                SubstitutionRatio, SubstitutionNotes, BestFor, NotSuitableFor,
                RankOrder, AllergenFreeJson,
                IsHomemadeRecipeAvailable, HomemadeRecipeId)
            OUTPUT INSERTED.Id
            VALUES (
                @FoodGroupId, @IngredientId, @ProductId, @CustomName,
                @SubstitutionRatio, @SubstitutionNotes, @BestFor, @NotSuitableFor,
                @RankOrder, @AllergenFreeJson,
                @IsHomemadeRecipeAvailable, @HomemadeRecipeId)";

        return await ExecuteScalarAsync<Guid>(sql,
            new SqlParameter("@FoodGroupId", member.FoodGroupId),
            new SqlParameter("@IngredientId", (object?)member.IngredientId ?? DBNull.Value),
            new SqlParameter("@ProductId", (object?)member.ProductId ?? DBNull.Value),
            new SqlParameter("@CustomName", (object?)member.CustomName ?? DBNull.Value),
            new SqlParameter("@SubstitutionRatio", (object?)member.SubstitutionRatio ?? DBNull.Value),
            new SqlParameter("@SubstitutionNotes", (object?)member.SubstitutionNotes ?? DBNull.Value),
            new SqlParameter("@BestFor", (object?)member.BestFor ?? DBNull.Value),
            new SqlParameter("@NotSuitableFor", (object?)member.NotSuitableFor ?? DBNull.Value),
            new SqlParameter("@RankOrder", member.RankOrder),
            new SqlParameter("@AllergenFreeJson", (object?)member.AllergenFreeJson ?? DBNull.Value),
            new SqlParameter("@IsHomemadeRecipeAvailable", member.IsHomemadeRecipeAvailable),
            new SqlParameter("@HomemadeRecipeId", (object?)member.HomemadeRecipeId ?? DBNull.Value));
    }

    public async Task<List<FoodGroupMemberDto>> GetFoodGroupMembersAsync(Guid foodGroupId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                Id, FoodGroupId, IngredientId, ProductId, CustomName,
                SubstitutionRatio, SubstitutionNotes, BestFor, NotSuitableFor,
                RankOrder, AllergenFreeJson,
                IsHomemadeRecipeAvailable, HomemadeRecipeId,
                IsActive, CreatedAt
            FROM FoodGroupMember
            WHERE FoodGroupId = @FoodGroupId AND IsActive = 1
            ORDER BY RankOrder, CustomName";

        return await ExecuteReaderAsync(sql, MapMember, new SqlParameter("@FoodGroupId", foodGroupId));
    }

    public async Task<List<FoodGroupMemberDto>> GetSubstitutesForIngredientAsync(Guid ingredientId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT DISTINCT
                m.Id, m.FoodGroupId, m.IngredientId, m.ProductId, m.CustomName,
                m.SubstitutionRatio, m.SubstitutionNotes, m.BestFor, m.NotSuitableFor,
                m.RankOrder, m.AllergenFreeJson,
                m.IsHomemadeRecipeAvailable, m.HomemadeRecipeId,
                m.IsActive, m.CreatedAt
            FROM FoodGroupMember m
            INNER JOIN FoodGroup fg ON fg.Id = m.FoodGroupId AND fg.IsActive = 1
            WHERE fg.Id IN (
                SELECT FoodGroupId
                FROM FoodGroupMember
                WHERE IngredientId = @IngredientId AND IsActive = 1
            )
            AND m.IsActive = 1
            AND m.IngredientId != @IngredientId
            ORDER BY m.RankOrder";

        return await ExecuteReaderAsync(sql, MapMember, new SqlParameter("@IngredientId", ingredientId));
    }

    public async Task<int> GetFoodGroupCountAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM FoodGroup";
        return await ExecuteScalarAsync<int>(sql);
    }

    // -----------------------------------------------------------------------
    // Substitution History
    // -----------------------------------------------------------------------

    public async Task<Guid> RecordSubstitutionAsync(SubstitutionHistoryRecord record, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO IngredientSubstitutionHistory (
                UserId, OriginalIngredientId, OriginalCustomName,
                SubstituteIngredientId, SubstituteCustomName,
                RecipeId, CookedAt, UserRating, Notes)
            OUTPUT INSERTED.Id
            VALUES (
                @UserId, @OriginalIngredientId, @OriginalCustomName,
                @SubstituteIngredientId, @SubstituteCustomName,
                @RecipeId, @CookedAt, @UserRating, @Notes)";

        return await ExecuteScalarAsync<Guid>(sql,
            new SqlParameter("@UserId", record.UserId),
            new SqlParameter("@OriginalIngredientId", (object?)record.OriginalIngredientId ?? DBNull.Value),
            new SqlParameter("@OriginalCustomName", (object?)record.OriginalCustomName ?? DBNull.Value),
            new SqlParameter("@SubstituteIngredientId", (object?)record.SubstituteIngredientId ?? DBNull.Value),
            new SqlParameter("@SubstituteCustomName", (object?)record.SubstituteCustomName ?? DBNull.Value),
            new SqlParameter("@RecipeId", (object?)record.RecipeId ?? DBNull.Value),
            new SqlParameter("@CookedAt", (object?)record.CookedAt ?? DBNull.Value),
            new SqlParameter("@UserRating", (object?)record.UserRating ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)record.Notes ?? DBNull.Value));
    }

    public async Task<List<SubstitutionHistoryDto>> GetUserSubstitutionHistoryAsync(Guid userId, Guid ingredientId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                Id, UserId, OriginalIngredientId, OriginalCustomName,
                SubstituteIngredientId, SubstituteCustomName,
                RecipeId, CookedAt, UserRating, Notes, CreatedAt
            FROM IngredientSubstitutionHistory
            WHERE UserId = @UserId
              AND (OriginalIngredientId = @IngredientId OR SubstituteIngredientId = @IngredientId)
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(sql, MapHistory,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@IngredientId", ingredientId));
    }

    // -----------------------------------------------------------------------
    // Mappers
    // -----------------------------------------------------------------------

    private static FoodGroupDto MapFoodGroup(SqlDataReader reader) =>
        new FoodGroupDto
        {
            Id = GetGuid(reader, "Id"),
            Name = GetString(reader, "Name") ?? string.Empty,
            Description = GetString(reader, "Description"),
            FunctionalRole = GetString(reader, "FunctionalRole"),
            IsActive = GetBoolean(reader, "IsActive"),
            CreatedAt = GetDateTime(reader, "CreatedAt"),
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt"),
            MemberCount = GetInt32(reader, "MemberCount")
        };

    private static FoodGroupMemberDto MapMember(SqlDataReader reader) =>
        new FoodGroupMemberDto
        {
            Id = GetGuid(reader, "Id"),
            FoodGroupId = GetGuid(reader, "FoodGroupId"),
            IngredientId = GetGuidNullable(reader, "IngredientId"),
            ProductId = GetGuidNullable(reader, "ProductId"),
            CustomName = GetString(reader, "CustomName"),
            SubstitutionRatio = GetString(reader, "SubstitutionRatio"),
            SubstitutionNotes = GetString(reader, "SubstitutionNotes"),
            BestFor = GetString(reader, "BestFor"),
            NotSuitableFor = GetString(reader, "NotSuitableFor"),
            RankOrder = GetInt32(reader, "RankOrder"),
            AllergenFreeJson = GetString(reader, "AllergenFreeJson"),
            IsHomemadeRecipeAvailable = GetBoolean(reader, "IsHomemadeRecipeAvailable"),
            HomemadeRecipeId = GetGuidNullable(reader, "HomemadeRecipeId"),
            IsActive = GetBoolean(reader, "IsActive"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        };

    private static SubstitutionHistoryDto MapHistory(SqlDataReader reader) =>
        new SubstitutionHistoryDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            OriginalIngredientId = GetGuidNullable(reader, "OriginalIngredientId"),
            OriginalCustomName = GetString(reader, "OriginalCustomName"),
            SubstituteIngredientId = GetGuidNullable(reader, "SubstituteIngredientId"),
            SubstituteCustomName = GetString(reader, "SubstituteCustomName"),
            RecipeId = GetGuidNullable(reader, "RecipeId"),
            CookedAt = GetNullableDateTime(reader, "CookedAt"),
            UserRating = GetIntNullable(reader, "UserRating"),
            Notes = GetString(reader, "Notes"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        };
}
