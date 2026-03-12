using ExpressRecipe.Data.Common;
using ExpressRecipe.CookbookService.Models;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.CookbookService.Data;

public class CookbookRepository : SqlHelper, ICookbookRepository
{
    public CookbookRepository(string connectionString) : base(connectionString)
    {
    }

    // ---- Cookbook CRUD ----

    public async Task<Guid> CreateCookbookAsync(CreateCookbookRequest request, Guid ownerId)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO Cookbook (Id, Title, Subtitle, Description, CoverImageUrl, AuthorName,
                Visibility, Tags, TitlePageContent, IntroductionContent, OwnerId, CreatedBy, CreatedAt)
            VALUES (@Id, @Title, @Subtitle, @Description, @CoverImageUrl, @AuthorName,
                @Visibility, @Tags, @TitlePageContent, @IntroductionContent, @OwnerId, @OwnerId, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Title", request.Title),
            new SqlParameter("@Subtitle", (object?)request.Subtitle ?? DBNull.Value),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@CoverImageUrl", (object?)request.CoverImageUrl ?? DBNull.Value),
            new SqlParameter("@AuthorName", (object?)request.AuthorName ?? DBNull.Value),
            new SqlParameter("@Visibility", request.Visibility),
            new SqlParameter("@Tags", (object?)request.Tags ?? DBNull.Value),
            new SqlParameter("@TitlePageContent", (object?)request.TitlePageContent ?? DBNull.Value),
            new SqlParameter("@IntroductionContent", (object?)request.IntroductionContent ?? DBNull.Value),
            new SqlParameter("@OwnerId", ownerId));

        return id;
    }

    public async Task<CookbookDto?> GetCookbookByIdAsync(Guid id, bool includeSections = true)
    {
        const string sql = @"
            SELECT c.Id, c.Title, c.Subtitle, c.Description, c.CoverImageUrl, c.AuthorName,
                c.Visibility, c.IsFavorite, c.Tags, c.TitlePageContent, c.IntroductionContent,
                c.IndexContent, c.NotesContent, c.WebSlug, c.ViewCount, c.OwnerId,
                c.CreatedAt, c.UpdatedAt,
                ISNULL(AVG(CAST(r.Rating AS DECIMAL(5,2))), 0) AS AverageRating,
                COUNT(DISTINCT r.Id) AS RatingCount,
                COUNT(DISTINCT cm.Id) AS CommentCount
            FROM Cookbook c
            LEFT JOIN CookbookRating r ON r.CookbookId = c.Id
            LEFT JOIN CookbookComment cm ON cm.CookbookId = c.Id AND cm.IsDeleted = 0
            WHERE c.Id = @Id AND c.IsDeleted = 0
            GROUP BY c.Id, c.Title, c.Subtitle, c.Description, c.CoverImageUrl, c.AuthorName,
                c.Visibility, c.IsFavorite, c.Tags, c.TitlePageContent, c.IntroductionContent,
                c.IndexContent, c.NotesContent, c.WebSlug, c.ViewCount, c.OwnerId,
                c.CreatedAt, c.UpdatedAt";

        var cookbooks = await ExecuteReaderAsync(sql, MapCookbookDto,
            new SqlParameter("@Id", id));

        var cookbook = cookbooks.FirstOrDefault();
        if (cookbook == null) return null;

        if (includeSections)
        {
            cookbook.Sections = await GetSectionsAsync(id);
            cookbook.UnsectionedRecipes = await GetUnsectionedRecipesAsync(id);
        }

        return cookbook;
    }

    public async Task<CookbookDto?> GetCookbookBySlugAsync(string slug)
    {
        const string sql = @"
            SELECT c.Id, c.Title, c.Subtitle, c.Description, c.CoverImageUrl, c.AuthorName,
                c.Visibility, c.IsFavorite, c.Tags, c.TitlePageContent, c.IntroductionContent,
                c.IndexContent, c.NotesContent, c.WebSlug, c.ViewCount, c.OwnerId,
                c.CreatedAt, c.UpdatedAt,
                ISNULL(AVG(CAST(r.Rating AS DECIMAL(5,2))), 0) AS AverageRating,
                COUNT(DISTINCT r.Id) AS RatingCount,
                COUNT(DISTINCT cm.Id) AS CommentCount
            FROM Cookbook c
            LEFT JOIN CookbookRating r ON r.CookbookId = c.Id
            LEFT JOIN CookbookComment cm ON cm.CookbookId = c.Id AND cm.IsDeleted = 0
            WHERE c.WebSlug = @Slug AND c.IsDeleted = 0
            GROUP BY c.Id, c.Title, c.Subtitle, c.Description, c.CoverImageUrl, c.AuthorName,
                c.Visibility, c.IsFavorite, c.Tags, c.TitlePageContent, c.IntroductionContent,
                c.IndexContent, c.NotesContent, c.WebSlug, c.ViewCount, c.OwnerId,
                c.CreatedAt, c.UpdatedAt";

        var cookbooks = await ExecuteReaderAsync(sql, MapCookbookDto,
            new SqlParameter("@Slug", slug));

        var cookbook = cookbooks.FirstOrDefault();
        if (cookbook == null) return null;

        cookbook.Sections = await GetSectionsAsync(cookbook.Id);
        cookbook.UnsectionedRecipes = await GetUnsectionedRecipesAsync(cookbook.Id);

        return cookbook;
    }

    public async Task<List<CookbookSummaryDto>> GetUserCookbooksAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        const string sql = @"
            SELECT c.Id, c.Title, c.Subtitle, c.CoverImageUrl, c.AuthorName, c.Visibility,
                c.IsFavorite, c.Tags, c.WebSlug, c.ViewCount, c.OwnerId, c.CreatedAt, c.UpdatedAt,
                ISNULL(AVG(CAST(r.Rating AS DECIMAL(5,2))), 0) AS AverageRating,
                COUNT(DISTINCT r.Id) AS RatingCount,
                COUNT(DISTINCT cr.Id) AS RecipeCount,
                COUNT(DISTINCT cs.Id) AS SectionCount,
                CAST(0 AS BIT) AS IsUserFavorite
            FROM Cookbook c
            LEFT JOIN CookbookRating r ON r.CookbookId = c.Id
            LEFT JOIN CookbookRecipe cr ON cr.CookbookId = c.Id
            LEFT JOIN CookbookSection cs ON cs.CookbookId = c.Id
            WHERE c.OwnerId = @UserId AND c.IsDeleted = 0
            GROUP BY c.Id, c.Title, c.Subtitle, c.CoverImageUrl, c.AuthorName, c.Visibility,
                c.IsFavorite, c.Tags, c.WebSlug, c.ViewCount, c.OwnerId, c.CreatedAt, c.UpdatedAt
            ORDER BY c.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        return await ExecuteReaderAsync(sql, MapCookbookSummaryDto,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Offset", (page - 1) * pageSize),
            new SqlParameter("@PageSize", pageSize));
    }

    public async Task<int> GetUserCookbookCountAsync(Guid userId)
    {
        const string sql = "SELECT COUNT(*) FROM Cookbook WHERE OwnerId = @UserId AND IsDeleted = 0";
        return await ExecuteScalarAsync<int>(sql, new SqlParameter("@UserId", userId));
    }

    public async Task<List<CookbookSummaryDto>> SearchCookbooksAsync(string? searchTerm, string? visibility, int page = 1, int pageSize = 20)
    {
        const string sql = @"
            SELECT c.Id, c.Title, c.Subtitle, c.CoverImageUrl, c.AuthorName, c.Visibility,
                c.IsFavorite, c.Tags, c.WebSlug, c.ViewCount, c.OwnerId, c.CreatedAt, c.UpdatedAt,
                ISNULL(AVG(CAST(r.Rating AS DECIMAL(5,2))), 0) AS AverageRating,
                COUNT(DISTINCT r.Id) AS RatingCount,
                COUNT(DISTINCT cr.Id) AS RecipeCount,
                COUNT(DISTINCT cs.Id) AS SectionCount,
                CAST(0 AS BIT) AS IsUserFavorite
            FROM Cookbook c
            LEFT JOIN CookbookRating r ON r.CookbookId = c.Id
            LEFT JOIN CookbookRecipe cr ON cr.CookbookId = c.Id
            LEFT JOIN CookbookSection cs ON cs.CookbookId = c.Id
            WHERE c.IsDeleted = 0
              AND c.Visibility = @Visibility
              AND (@SearchTerm IS NULL OR c.Title LIKE @SearchTermLike OR c.Description LIKE @SearchTermLike)
            GROUP BY c.Id, c.Title, c.Subtitle, c.CoverImageUrl, c.AuthorName, c.Visibility,
                c.IsFavorite, c.Tags, c.WebSlug, c.ViewCount, c.OwnerId, c.CreatedAt, c.UpdatedAt
            ORDER BY c.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var vis = visibility ?? "Public";
        var termLike = searchTerm != null ? $"%{searchTerm}%" : null;

        return await ExecuteReaderAsync(sql, MapCookbookSummaryDto,
            new SqlParameter("@Visibility", vis),
            new SqlParameter("@SearchTerm", (object?)searchTerm ?? DBNull.Value),
            new SqlParameter("@SearchTermLike", (object?)termLike ?? DBNull.Value),
            new SqlParameter("@Offset", (page - 1) * pageSize),
            new SqlParameter("@PageSize", pageSize));
    }

    public async Task<int> SearchCookbooksCountAsync(string? searchTerm, string? visibility)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM Cookbook c
            WHERE c.IsDeleted = 0
              AND c.Visibility = @Visibility
              AND (@SearchTerm IS NULL OR c.Title LIKE @SearchTermLike OR c.Description LIKE @SearchTermLike)";

        var vis = visibility ?? "Public";
        var termLike = searchTerm != null ? $"%{searchTerm}%" : null;

        return await ExecuteScalarAsync<int>(sql,
            new SqlParameter("@Visibility", vis),
            new SqlParameter("@SearchTerm", (object?)searchTerm ?? DBNull.Value),
            new SqlParameter("@SearchTermLike", (object?)termLike ?? DBNull.Value));
    }

    public async Task<bool> UpdateCookbookAsync(Guid id, Guid userId, UpdateCookbookRequest request)
    {
        var setClauses = new List<string> { "UpdatedAt = GETUTCDATE()", "UpdatedBy = @UserId" };
        var parameters = new List<SqlParameter> { new("@Id", id), new("@UserId", userId) };

        if (request.Title != null) { setClauses.Add("Title = @Title"); parameters.Add(new("@Title", request.Title)); }
        if (request.Subtitle != null) { setClauses.Add("Subtitle = @Subtitle"); parameters.Add(new("@Subtitle", request.Subtitle)); }
        if (request.Description != null) { setClauses.Add("Description = @Description"); parameters.Add(new("@Description", request.Description)); }
        if (request.CoverImageUrl != null) { setClauses.Add("CoverImageUrl = @CoverImageUrl"); parameters.Add(new("@CoverImageUrl", request.CoverImageUrl)); }
        if (request.AuthorName != null) { setClauses.Add("AuthorName = @AuthorName"); parameters.Add(new("@AuthorName", request.AuthorName)); }
        if (request.Visibility != null) { setClauses.Add("Visibility = @Visibility"); parameters.Add(new("@Visibility", request.Visibility)); }
        if (request.IsFavorite.HasValue) { setClauses.Add("IsFavorite = @IsFavorite"); parameters.Add(new("@IsFavorite", request.IsFavorite.Value)); }
        if (request.Tags != null) { setClauses.Add("Tags = @Tags"); parameters.Add(new("@Tags", request.Tags)); }
        if (request.TitlePageContent != null) { setClauses.Add("TitlePageContent = @TitlePageContent"); parameters.Add(new("@TitlePageContent", request.TitlePageContent)); }
        if (request.IntroductionContent != null) { setClauses.Add("IntroductionContent = @IntroductionContent"); parameters.Add(new("@IntroductionContent", request.IntroductionContent)); }
        if (request.IndexContent != null) { setClauses.Add("IndexContent = @IndexContent"); parameters.Add(new("@IndexContent", request.IndexContent)); }
        if (request.NotesContent != null) { setClauses.Add("NotesContent = @NotesContent"); parameters.Add(new("@NotesContent", request.NotesContent)); }
        if (request.WebSlug != null) { setClauses.Add("WebSlug = @WebSlug"); parameters.Add(new("@WebSlug", request.WebSlug)); }

        var sql = $"UPDATE Cookbook SET {string.Join(", ", setClauses)} WHERE Id = @Id AND OwnerId = @UserId AND IsDeleted = 0";
        var rows = await ExecuteNonQueryAsync(sql, parameters.ToArray());
        return rows > 0;
    }

    public async Task<bool> DeleteCookbookAsync(Guid id, Guid userId)
    {
        const string sql = @"
            UPDATE Cookbook SET IsDeleted = 1, DeletedAt = GETUTCDATE()
            WHERE Id = @Id AND OwnerId = @UserId AND IsDeleted = 0";
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId));
        return rows > 0;
    }

    // ---- Section management ----

    public async Task<Guid> CreateSectionAsync(Guid cookbookId, Guid userId, CreateCookbookSectionRequest request)
    {
        if (!await IsOwnerAsync(cookbookId, userId))
            throw new UnauthorizedAccessException("User does not own this cookbook");

        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO CookbookSection (Id, CookbookId, Title, Description, TitlePageContent, CategoryOrMealType, SortOrder, CreatedAt)
            VALUES (@Id, @CookbookId, @Title, @Description, @TitlePageContent, @CategoryOrMealType, @SortOrder, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@Title", request.Title),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@TitlePageContent", (object?)request.TitlePageContent ?? DBNull.Value),
            new SqlParameter("@CategoryOrMealType", (object?)request.CategoryOrMealType ?? DBNull.Value),
            new SqlParameter("@SortOrder", request.SortOrder));

        return id;
    }

    public async Task<bool> UpdateSectionAsync(Guid sectionId, Guid userId, UpdateCookbookSectionRequest request)
    {
        var setClauses = new List<string> { "UpdatedAt = GETUTCDATE()" };
        var parameters = new List<SqlParameter> { new("@Id", sectionId) };

        if (request.Title != null) { setClauses.Add("Title = @Title"); parameters.Add(new("@Title", request.Title)); }
        if (request.Description != null) { setClauses.Add("Description = @Description"); parameters.Add(new("@Description", request.Description)); }
        if (request.TitlePageContent != null) { setClauses.Add("TitlePageContent = @TitlePageContent"); parameters.Add(new("@TitlePageContent", request.TitlePageContent)); }
        if (request.CategoryOrMealType != null) { setClauses.Add("CategoryOrMealType = @CategoryOrMealType"); parameters.Add(new("@CategoryOrMealType", request.CategoryOrMealType)); }
        if (request.SortOrder.HasValue) { setClauses.Add("SortOrder = @SortOrder"); parameters.Add(new("@SortOrder", request.SortOrder.Value)); }

        var sql = $@"UPDATE cs SET {string.Join(", ", setClauses)}
            FROM CookbookSection cs
            INNER JOIN Cookbook c ON c.Id = cs.CookbookId
            WHERE cs.Id = @Id AND c.OwnerId = @UserId AND c.IsDeleted = 0";

        parameters.Add(new SqlParameter("@UserId", userId));
        var rows = await ExecuteNonQueryAsync(sql, parameters.ToArray());
        return rows > 0;
    }

    public async Task<bool> DeleteSectionAsync(Guid sectionId, Guid userId)
    {
        const string sql = @"
            DELETE cs FROM CookbookSection cs
            INNER JOIN Cookbook c ON c.Id = cs.CookbookId
            WHERE cs.Id = @Id AND c.OwnerId = @UserId AND c.IsDeleted = 0";
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", sectionId),
            new SqlParameter("@UserId", userId));
        return rows > 0;
    }

    public async Task<bool> ReorderSectionsAsync(Guid cookbookId, Guid userId, List<Guid> sectionIds)
    {
        if (!await IsOwnerAsync(cookbookId, userId)) return false;

        return await ExecuteTransactionAsync<bool>(async (conn, tx) =>
        {
            for (int i = 0; i < sectionIds.Count; i++)
            {
                const string sql = "UPDATE CookbookSection SET SortOrder = @Order WHERE Id = @Id AND CookbookId = @CookbookId";
                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@Order", i);
                cmd.Parameters.AddWithValue("@Id", sectionIds[i]);
                cmd.Parameters.AddWithValue("@CookbookId", cookbookId);
                await cmd.ExecuteNonQueryAsync();
            }
            return true;
        });
    }

    // ---- Recipe management ----

    public async Task<Guid> AddRecipeToCookbookAsync(Guid cookbookId, Guid userId, AddCookbookRecipeRequest request)
    {
        if (!await CanEditAsync(cookbookId, userId))
            throw new UnauthorizedAccessException("User cannot edit this cookbook");

        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO CookbookRecipe (Id, CookbookId, SectionId, RecipeId, RecipeName, SortOrder, Notes, CreatedAt)
            VALUES (@Id, @CookbookId, @SectionId, @RecipeId, @RecipeName, @SortOrder, @Notes, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@SectionId", (object?)request.SectionId ?? DBNull.Value),
            new SqlParameter("@RecipeId", request.RecipeId),
            new SqlParameter("@RecipeName", request.RecipeName),
            new SqlParameter("@SortOrder", request.SortOrder),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        return id;
    }

    public async Task<bool> AddRecipesBatchAsync(Guid cookbookId, Guid userId, Guid? sectionId, List<AddCookbookRecipeRequest> recipes)
    {
        if (!await CanEditAsync(cookbookId, userId)) return false;

        return await ExecuteTransactionAsync<bool>(async (conn, tx) =>
        {
            foreach (var recipe in recipes)
            {
                const string sql = @"
                    INSERT INTO CookbookRecipe (Id, CookbookId, SectionId, RecipeId, RecipeName, SortOrder, Notes, CreatedAt)
                    VALUES (@Id, @CookbookId, @SectionId, @RecipeId, @RecipeName, @SortOrder, @Notes, GETUTCDATE())";
                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("@CookbookId", cookbookId);
                cmd.Parameters.AddWithValue("@SectionId", (object?)(recipe.SectionId ?? sectionId) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RecipeId", recipe.RecipeId);
                cmd.Parameters.AddWithValue("@RecipeName", recipe.RecipeName);
                cmd.Parameters.AddWithValue("@SortOrder", recipe.SortOrder);
                cmd.Parameters.AddWithValue("@Notes", (object?)recipe.Notes ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            return true;
        });
    }

    public async Task<bool> RemoveRecipeFromCookbookAsync(Guid cookbookRecipeId, Guid userId)
    {
        const string sql = @"
            DELETE cr FROM CookbookRecipe cr
            INNER JOIN Cookbook c ON c.Id = cr.CookbookId
            WHERE cr.Id = @Id AND c.OwnerId = @UserId AND c.IsDeleted = 0";
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", cookbookRecipeId),
            new SqlParameter("@UserId", userId));
        return rows > 0;
    }

    public async Task<bool> MoveRecipeToSectionAsync(Guid cookbookRecipeId, Guid userId, Guid? newSectionId)
    {
        const string sql = @"
            UPDATE cr SET cr.SectionId = @SectionId, cr.UpdatedAt = GETUTCDATE()
            FROM CookbookRecipe cr
            INNER JOIN Cookbook c ON c.Id = cr.CookbookId
            WHERE cr.Id = @Id AND c.OwnerId = @UserId AND c.IsDeleted = 0";
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", cookbookRecipeId),
            new SqlParameter("@SectionId", (object?)newSectionId ?? DBNull.Value),
            new SqlParameter("@UserId", userId));
        return rows > 0;
    }

    public async Task<bool> ReorderRecipesAsync(Guid cookbookId, Guid? sectionId, List<Guid> recipeIds)
    {
        return await ExecuteTransactionAsync<bool>(async (conn, tx) =>
        {
            for (int i = 0; i < recipeIds.Count; i++)
            {
                const string sql = "UPDATE CookbookRecipe SET SortOrder = @Order WHERE Id = @Id AND CookbookId = @CookbookId";
                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@Order", i);
                cmd.Parameters.AddWithValue("@Id", recipeIds[i]);
                cmd.Parameters.AddWithValue("@CookbookId", cookbookId);
                await cmd.ExecuteNonQueryAsync();
            }
            return true;
        });
    }

    // ---- Ratings and comments ----

    public async Task<bool> RateCookbookAsync(Guid cookbookId, Guid userId, int rating)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM CookbookRating WHERE CookbookId = @CookbookId AND UserId = @UserId)
                UPDATE CookbookRating SET Rating = @Rating, UpdatedAt = GETUTCDATE()
                WHERE CookbookId = @CookbookId AND UserId = @UserId
            ELSE
                INSERT INTO CookbookRating (Id, CookbookId, UserId, Rating, CreatedAt)
                VALUES (NEWID(), @CookbookId, @UserId, @Rating, GETUTCDATE())";
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Rating", rating));
        return rows > 0;
    }

    public async Task<(decimal Average, int Count)> GetRatingsAsync(Guid cookbookId)
    {
        const string sql = @"
            SELECT ISNULL(AVG(CAST(Rating AS DECIMAL(5,2))), 0) AS Avg, COUNT(*) AS Cnt
            FROM CookbookRating WHERE CookbookId = @CookbookId";
        var results = await ExecuteReaderAsync(sql, reader => (
            Average: reader.IsDBNull(0) ? 0m : reader.GetDecimal(0),
            Count: reader.GetInt32(1)
        ), new SqlParameter("@CookbookId", cookbookId));
        return results.FirstOrDefault();
    }

    public async Task<Guid> AddCommentAsync(Guid cookbookId, Guid userId, string content)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO CookbookComment (Id, CookbookId, UserId, Content, CreatedAt)
            VALUES (@Id, @CookbookId, @UserId, @Content, GETUTCDATE())";
        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Content", content));
        return id;
    }

    public async Task<List<CookbookCommentDto>> GetCommentsAsync(Guid cookbookId, int page = 1, int pageSize = 20)
    {
        const string sql = @"
            SELECT Id, CookbookId, UserId, Content, CreatedAt, UpdatedAt
            FROM CookbookComment
            WHERE CookbookId = @CookbookId AND IsDeleted = 0
            ORDER BY CreatedAt ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        return await ExecuteReaderAsync(sql, reader => new CookbookCommentDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            CookbookId = reader.GetGuid(reader.GetOrdinal("CookbookId")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            Content = reader.GetString(reader.GetOrdinal("Content")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@CookbookId", cookbookId),
        new SqlParameter("@Offset", (page - 1) * pageSize),
        new SqlParameter("@PageSize", pageSize));
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, Guid userId)
    {
        const string sql = @"
            UPDATE CookbookComment SET IsDeleted = 1
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", commentId),
            new SqlParameter("@UserId", userId));
        return rows > 0;
    }

    // ---- Favorites ----

    public async Task<bool> FavoriteCookbookAsync(Guid cookbookId, Guid userId)
    {
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM CookbookFavorite WHERE CookbookId = @CookbookId AND UserId = @UserId)
                INSERT INTO CookbookFavorite (Id, CookbookId, UserId, CreatedAt)
                VALUES (NEWID(), @CookbookId, @UserId, GETUTCDATE())";
        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@UserId", userId));
        return true;
    }

    public async Task<bool> UnfavoriteCookbookAsync(Guid cookbookId, Guid userId)
    {
        const string sql = "DELETE FROM CookbookFavorite WHERE CookbookId = @CookbookId AND UserId = @UserId";
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@UserId", userId));
        return rows > 0;
    }

    public async Task<bool> IsFavoritedAsync(Guid cookbookId, Guid userId)
    {
        const string sql = "SELECT COUNT(1) FROM CookbookFavorite WHERE CookbookId = @CookbookId AND UserId = @UserId";
        var count = await ExecuteScalarAsync<int>(sql,
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@UserId", userId));
        return count > 0;
    }

    public async Task<List<CookbookSummaryDto>> GetFavoriteCookbooksAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        const string sql = @"
            SELECT c.Id, c.Title, c.Subtitle, c.CoverImageUrl, c.AuthorName, c.Visibility,
                c.IsFavorite, c.Tags, c.WebSlug, c.ViewCount, c.OwnerId, c.CreatedAt, c.UpdatedAt,
                ISNULL(AVG(CAST(r.Rating AS DECIMAL(5,2))), 0) AS AverageRating,
                COUNT(DISTINCT r.Id) AS RatingCount,
                COUNT(DISTINCT cr.Id) AS RecipeCount,
                COUNT(DISTINCT cs.Id) AS SectionCount,
                CAST(1 AS BIT) AS IsUserFavorite
            FROM Cookbook c
            INNER JOIN CookbookFavorite f ON f.CookbookId = c.Id AND f.UserId = @UserId
            LEFT JOIN CookbookRating r ON r.CookbookId = c.Id
            LEFT JOIN CookbookRecipe cr ON cr.CookbookId = c.Id
            LEFT JOIN CookbookSection cs ON cs.CookbookId = c.Id
            WHERE c.IsDeleted = 0
            GROUP BY c.Id, c.Title, c.Subtitle, c.CoverImageUrl, c.AuthorName, c.Visibility,
                c.IsFavorite, c.Tags, c.WebSlug, c.ViewCount, c.OwnerId, c.CreatedAt, c.UpdatedAt,
                f.CreatedAt
            ORDER BY f.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        return await ExecuteReaderAsync(sql, MapCookbookSummaryDto,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Offset", (page - 1) * pageSize),
            new SqlParameter("@PageSize", pageSize));
    }

    // ---- Merge and split ----

    public async Task<Guid> MergeCookbooksAsync(Guid userId, MergeCookbooksRequest request)
    {
        return await ExecuteTransactionAsync<Guid>(async (conn, tx) =>
        {
            var newId = Guid.NewGuid();
            const string insertCookbook = @"
                INSERT INTO Cookbook (Id, Title, Description, Visibility, OwnerId, CreatedBy, CreatedAt)
                VALUES (@Id, @Title, @Description, 'Private', @OwnerId, @OwnerId, GETUTCDATE())";
            await using (var cmd = new SqlCommand(insertCookbook, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Title", request.NewTitle);
                cmd.Parameters.AddWithValue("@Description", (object?)request.NewDescription ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OwnerId", userId);
                await cmd.ExecuteNonQueryAsync();
            }

            foreach (var sourceId in request.SourceCookbookIds)
            {
                var sections = new List<(Guid OldId, string Title, string? Desc, string? TitlePage, string? Cat, int Sort)>();
                const string getSections = "SELECT Id, Title, Description, TitlePageContent, CategoryOrMealType, SortOrder FROM CookbookSection WHERE CookbookId = @CookbookId ORDER BY SortOrder";
                await using (var cmd = new SqlCommand(getSections, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@CookbookId", sourceId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        sections.Add((
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.IsDBNull(2) ? null : reader.GetString(2),
                            reader.IsDBNull(3) ? null : reader.GetString(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4),
                            reader.GetInt32(5)
                        ));
                    }
                }

                var sectionMap = new Dictionary<Guid, Guid>();
                foreach (var section in sections)
                {
                    var newSectionId = Guid.NewGuid();
                    sectionMap[section.OldId] = newSectionId;
                    const string insertSection = @"
                        INSERT INTO CookbookSection (Id, CookbookId, Title, Description, TitlePageContent, CategoryOrMealType, SortOrder, CreatedAt)
                        VALUES (@Id, @CookbookId, @Title, @Desc, @TitlePage, @Cat, @Sort, GETUTCDATE())";
                    await using var cmd = new SqlCommand(insertSection, conn, tx);
                    cmd.Parameters.AddWithValue("@Id", newSectionId);
                    cmd.Parameters.AddWithValue("@CookbookId", newId);
                    cmd.Parameters.AddWithValue("@Title", section.Title);
                    cmd.Parameters.AddWithValue("@Desc", (object?)section.Desc ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TitlePage", (object?)section.TitlePage ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Cat", (object?)section.Cat ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Sort", section.Sort);
                    await cmd.ExecuteNonQueryAsync();
                }

                const string getRecipes = "SELECT RecipeId, RecipeName, SectionId, SortOrder, Notes FROM CookbookRecipe WHERE CookbookId = @CookbookId";
                await using (var cmd = new SqlCommand(getRecipes, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@CookbookId", sourceId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    var recipesToInsert = new List<(Guid RecipeId, string Name, Guid? SectionId, int Sort, string? Notes)>();
                    while (await reader.ReadAsync())
                    {
                        Guid? oldSectionId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
                        Guid? newSectionId = oldSectionId.HasValue && sectionMap.ContainsKey(oldSectionId.Value)
                            ? sectionMap[oldSectionId.Value] : null;
                        recipesToInsert.Add((reader.GetGuid(0), reader.GetString(1), newSectionId, reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
                    }

                    foreach (var recipe in recipesToInsert)
                    {
                        const string insertRecipe = @"
                            INSERT INTO CookbookRecipe (Id, CookbookId, SectionId, RecipeId, RecipeName, SortOrder, Notes, CreatedAt)
                            VALUES (@Id, @CookbookId, @SectionId, @RecipeId, @RecipeName, @SortOrder, @Notes, GETUTCDATE())";
                        await using var rcmd = new SqlCommand(insertRecipe, conn, tx);
                        rcmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
                        rcmd.Parameters.AddWithValue("@CookbookId", newId);
                        rcmd.Parameters.AddWithValue("@SectionId", (object?)recipe.SectionId ?? DBNull.Value);
                        rcmd.Parameters.AddWithValue("@RecipeId", recipe.RecipeId);
                        rcmd.Parameters.AddWithValue("@RecipeName", recipe.Name);
                        rcmd.Parameters.AddWithValue("@SortOrder", recipe.Sort);
                        rcmd.Parameters.AddWithValue("@Notes", (object?)recipe.Notes ?? DBNull.Value);
                        await rcmd.ExecuteNonQueryAsync();
                    }
                }

                if (request.DeleteSources)
                {
                    const string deleteSrc = "UPDATE Cookbook SET IsDeleted = 1, DeletedAt = GETUTCDATE() WHERE Id = @Id AND OwnerId = @OwnerId";
                    await using var cmd = new SqlCommand(deleteSrc, conn, tx);
                    cmd.Parameters.AddWithValue("@Id", sourceId);
                    cmd.Parameters.AddWithValue("@OwnerId", userId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return newId;
        });
    }

    public async Task<List<Guid>> SplitCookbookAsync(Guid cookbookId, Guid userId, SplitCookbookRequest request)
    {
        if (!await IsOwnerAsync(cookbookId, userId))
            throw new UnauthorizedAccessException("User does not own this cookbook");

        return await ExecuteTransactionAsync<List<Guid>>(async (conn, tx) =>
        {
            var newCookbookIds = new List<Guid>();

            foreach (var sectionId in request.SectionIds)
            {
                string sectionTitle = string.Empty;
                const string getSection = "SELECT Title FROM CookbookSection WHERE Id = @Id AND CookbookId = @CookbookId";
                await using (var cmd = new SqlCommand(getSection, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", sectionId);
                    cmd.Parameters.AddWithValue("@CookbookId", cookbookId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result == null) continue;
                    sectionTitle = (string)result;
                }

                var newId = Guid.NewGuid();
                const string insertCookbook = @"
                    INSERT INTO Cookbook (Id, Title, Visibility, OwnerId, CreatedBy, CreatedAt)
                    VALUES (@Id, @Title, 'Private', @OwnerId, @OwnerId, GETUTCDATE())";
                await using (var cmd = new SqlCommand(insertCookbook, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", newId);
                    cmd.Parameters.AddWithValue("@Title", sectionTitle);
                    cmd.Parameters.AddWithValue("@OwnerId", userId);
                    await cmd.ExecuteNonQueryAsync();
                }

                const string getRecipes = "SELECT RecipeId, RecipeName, SortOrder, Notes FROM CookbookRecipe WHERE SectionId = @SectionId";
                await using (var cmd = new SqlCommand(getRecipes, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@SectionId", sectionId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    var recipesToInsert = new List<(Guid RecipeId, string Name, int Sort, string? Notes)>();
                    while (await reader.ReadAsync())
                    {
                        recipesToInsert.Add((reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
                    }

                    foreach (var recipe in recipesToInsert)
                    {
                        const string insertRecipe = @"
                            INSERT INTO CookbookRecipe (Id, CookbookId, RecipeId, RecipeName, SortOrder, Notes, CreatedAt)
                            VALUES (@Id, @CookbookId, @RecipeId, @RecipeName, @SortOrder, @Notes, GETUTCDATE())";
                        await using var rcmd = new SqlCommand(insertRecipe, conn, tx);
                        rcmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
                        rcmd.Parameters.AddWithValue("@CookbookId", newId);
                        rcmd.Parameters.AddWithValue("@RecipeId", recipe.RecipeId);
                        rcmd.Parameters.AddWithValue("@RecipeName", recipe.Name);
                        rcmd.Parameters.AddWithValue("@SortOrder", recipe.Sort);
                        rcmd.Parameters.AddWithValue("@Notes", (object?)recipe.Notes ?? DBNull.Value);
                        await rcmd.ExecuteNonQueryAsync();
                    }
                }

                newCookbookIds.Add(newId);
            }

            return newCookbookIds;
        });
    }

    public async Task<List<Guid>> ExtractRecipesFromCookbookAsync(Guid cookbookId, Guid userId)
    {
        if (!await IsOwnerAsync(cookbookId, userId))
            throw new UnauthorizedAccessException("User does not own this cookbook");

        const string sql = "SELECT RecipeId FROM CookbookRecipe WHERE CookbookId = @CookbookId";
        return await ExecuteReaderAsync(sql,
            reader => reader.GetGuid(reader.GetOrdinal("RecipeId")),
            new SqlParameter("@CookbookId", cookbookId));
    }

    // ---- Sharing ----

    public async Task<bool> ShareCookbookAsync(Guid cookbookId, Guid ownerId, Guid targetUserId, bool canEdit)
    {
        if (!await IsOwnerAsync(cookbookId, ownerId)) return false;

        const string sql = @"
            IF EXISTS (SELECT 1 FROM CookbookShare WHERE CookbookId = @CookbookId AND SharedWithUserId = @TargetUserId)
                UPDATE CookbookShare SET CanEdit = @CanEdit WHERE CookbookId = @CookbookId AND SharedWithUserId = @TargetUserId
            ELSE
                INSERT INTO CookbookShare (Id, CookbookId, SharedWithUserId, CanEdit, CreatedAt)
                VALUES (NEWID(), @CookbookId, @TargetUserId, @CanEdit, GETUTCDATE())";

        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@TargetUserId", targetUserId),
            new SqlParameter("@CanEdit", canEdit));
        return rows > 0;
    }

    public async Task<bool> RevokeCookbookShareAsync(Guid cookbookId, Guid ownerId, Guid targetUserId)
    {
        if (!await IsOwnerAsync(cookbookId, ownerId)) return false;

        const string sql = "DELETE FROM CookbookShare WHERE CookbookId = @CookbookId AND SharedWithUserId = @TargetUserId";
        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CookbookId", cookbookId),
            new SqlParameter("@TargetUserId", targetUserId));
        return rows > 0;
    }

    // ---- View tracking ----

    public async Task IncrementViewCountAsync(Guid cookbookId)
    {
        const string sql = "UPDATE Cookbook SET ViewCount = ViewCount + 1 WHERE Id = @Id AND IsDeleted = 0";
        await ExecuteNonQueryAsync(sql, new SqlParameter("@Id", cookbookId));
    }

    // ---- Ownership checks ----

    public async Task<bool> IsOwnerAsync(Guid cookbookId, Guid userId)
    {
        const string sql = "SELECT COUNT(1) FROM Cookbook WHERE Id = @Id AND OwnerId = @UserId AND IsDeleted = 0";
        var count = await ExecuteScalarAsync<int>(sql,
            new SqlParameter("@Id", cookbookId),
            new SqlParameter("@UserId", userId));
        return count > 0;
    }

    public async Task<bool> CanViewAsync(Guid cookbookId, Guid userId)
    {
        const string sql = @"
            SELECT COUNT(1) FROM Cookbook c
            WHERE c.Id = @Id AND c.IsDeleted = 0
              AND (c.OwnerId = @UserId OR c.Visibility = 'Public'
                   OR EXISTS (SELECT 1 FROM CookbookShare s WHERE s.CookbookId = c.Id AND s.SharedWithUserId = @UserId))";
        var count = await ExecuteScalarAsync<int>(sql,
            new SqlParameter("@Id", cookbookId),
            new SqlParameter("@UserId", userId));
        return count > 0;
    }

    public async Task<bool> CanEditAsync(Guid cookbookId, Guid userId)
    {
        const string sql = @"
            SELECT COUNT(1) FROM Cookbook c
            WHERE c.Id = @Id AND c.IsDeleted = 0
              AND (c.OwnerId = @UserId
                   OR EXISTS (SELECT 1 FROM CookbookShare s WHERE s.CookbookId = c.Id AND s.SharedWithUserId = @UserId AND s.CanEdit = 1))";
        var count = await ExecuteScalarAsync<int>(sql,
            new SqlParameter("@Id", cookbookId),
            new SqlParameter("@UserId", userId));
        return count > 0;
    }

    // ---- Private helpers ----

    private async Task<List<CookbookSectionDto>> GetSectionsAsync(Guid cookbookId)
    {
        const string sql = @"
            SELECT Id, CookbookId, Title, Description, TitlePageContent, CategoryOrMealType, SortOrder
            FROM CookbookSection WHERE CookbookId = @CookbookId ORDER BY SortOrder";

        var sections = await ExecuteReaderAsync(sql, reader => new CookbookSectionDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            CookbookId = reader.GetGuid(reader.GetOrdinal("CookbookId")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            TitlePageContent = reader.IsDBNull(reader.GetOrdinal("TitlePageContent")) ? null : reader.GetString(reader.GetOrdinal("TitlePageContent")),
            CategoryOrMealType = reader.IsDBNull(reader.GetOrdinal("CategoryOrMealType")) ? null : reader.GetString(reader.GetOrdinal("CategoryOrMealType")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder"))
        }, new SqlParameter("@CookbookId", cookbookId));

        var allRecipes = await GetAllCookbookRecipesAsync(cookbookId);
        foreach (var section in sections)
        {
            section.Recipes = allRecipes.Where(r => r.SectionId == section.Id).ToList();
        }

        return sections;
    }

    private async Task<List<CookbookRecipeDto>> GetUnsectionedRecipesAsync(Guid cookbookId)
    {
        var allRecipes = await GetAllCookbookRecipesAsync(cookbookId);
        return allRecipes.Where(r => r.SectionId == null).ToList();
    }

    private async Task<List<CookbookRecipeDto>> GetAllCookbookRecipesAsync(Guid cookbookId)
    {
        const string sql = @"
            SELECT Id, CookbookId, SectionId, RecipeId, RecipeName, SortOrder, Notes, PageNumber
            FROM CookbookRecipe WHERE CookbookId = @CookbookId ORDER BY SortOrder";

        return await ExecuteReaderAsync(sql, reader => new CookbookRecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            CookbookId = reader.GetGuid(reader.GetOrdinal("CookbookId")),
            SectionId = reader.IsDBNull(reader.GetOrdinal("SectionId")) ? null : reader.GetGuid(reader.GetOrdinal("SectionId")),
            RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
            RecipeName = reader.GetString(reader.GetOrdinal("RecipeName")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            PageNumber = reader.IsDBNull(reader.GetOrdinal("PageNumber")) ? null : reader.GetInt32(reader.GetOrdinal("PageNumber"))
        }, new SqlParameter("@CookbookId", cookbookId));
    }

    private static CookbookDto MapCookbookDto(SqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        Title = reader.GetString(reader.GetOrdinal("Title")),
        Subtitle = reader.IsDBNull(reader.GetOrdinal("Subtitle")) ? null : reader.GetString(reader.GetOrdinal("Subtitle")),
        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
        CoverImageUrl = reader.IsDBNull(reader.GetOrdinal("CoverImageUrl")) ? null : reader.GetString(reader.GetOrdinal("CoverImageUrl")),
        AuthorName = reader.IsDBNull(reader.GetOrdinal("AuthorName")) ? null : reader.GetString(reader.GetOrdinal("AuthorName")),
        Visibility = reader.GetString(reader.GetOrdinal("Visibility")),
        IsFavorite = reader.GetBoolean(reader.GetOrdinal("IsFavorite")),
        Tags = reader.IsDBNull(reader.GetOrdinal("Tags")) ? null : reader.GetString(reader.GetOrdinal("Tags")),
        TitlePageContent = reader.IsDBNull(reader.GetOrdinal("TitlePageContent")) ? null : reader.GetString(reader.GetOrdinal("TitlePageContent")),
        IntroductionContent = reader.IsDBNull(reader.GetOrdinal("IntroductionContent")) ? null : reader.GetString(reader.GetOrdinal("IntroductionContent")),
        IndexContent = reader.IsDBNull(reader.GetOrdinal("IndexContent")) ? null : reader.GetString(reader.GetOrdinal("IndexContent")),
        NotesContent = reader.IsDBNull(reader.GetOrdinal("NotesContent")) ? null : reader.GetString(reader.GetOrdinal("NotesContent")),
        WebSlug = reader.IsDBNull(reader.GetOrdinal("WebSlug")) ? null : reader.GetString(reader.GetOrdinal("WebSlug")),
        ViewCount = reader.GetInt32(reader.GetOrdinal("ViewCount")),
        OwnerId = reader.GetGuid(reader.GetOrdinal("OwnerId")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
        AverageRating = reader.GetDecimal(reader.GetOrdinal("AverageRating")),
        RatingCount = reader.GetInt32(reader.GetOrdinal("RatingCount")),
        CommentCount = reader.GetInt32(reader.GetOrdinal("CommentCount"))
    };

    private static CookbookSummaryDto MapCookbookSummaryDto(SqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        Title = reader.GetString(reader.GetOrdinal("Title")),
        Subtitle = reader.IsDBNull(reader.GetOrdinal("Subtitle")) ? null : reader.GetString(reader.GetOrdinal("Subtitle")),
        CoverImageUrl = reader.IsDBNull(reader.GetOrdinal("CoverImageUrl")) ? null : reader.GetString(reader.GetOrdinal("CoverImageUrl")),
        AuthorName = reader.IsDBNull(reader.GetOrdinal("AuthorName")) ? null : reader.GetString(reader.GetOrdinal("AuthorName")),
        Visibility = reader.GetString(reader.GetOrdinal("Visibility")),
        IsFavorite = reader.GetBoolean(reader.GetOrdinal("IsFavorite")),
        Tags = reader.IsDBNull(reader.GetOrdinal("Tags")) ? null : reader.GetString(reader.GetOrdinal("Tags")),
        WebSlug = reader.IsDBNull(reader.GetOrdinal("WebSlug")) ? null : reader.GetString(reader.GetOrdinal("WebSlug")),
        ViewCount = reader.GetInt32(reader.GetOrdinal("ViewCount")),
        OwnerId = reader.GetGuid(reader.GetOrdinal("OwnerId")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
        AverageRating = reader.GetDecimal(reader.GetOrdinal("AverageRating")),
        RatingCount = reader.GetInt32(reader.GetOrdinal("RatingCount")),
        RecipeCount = reader.GetInt32(reader.GetOrdinal("RecipeCount")),
        SectionCount = reader.GetInt32(reader.GetOrdinal("SectionCount")),
        IsUserFavorite = reader.GetBoolean(reader.GetOrdinal("IsUserFavorite"))
    };

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM CookbookRecipe   WHERE CookbookId IN (SELECT Id FROM Cookbook WHERE UserId = @UserId);
DELETE FROM CookbookSection  WHERE CookbookId IN (SELECT Id FROM Cookbook WHERE UserId = @UserId);
DELETE FROM CookbookShare    WHERE UserId = @UserId OR SharedWithUserId = @UserId;
DELETE FROM Cookbook         WHERE UserId = @UserId;";

        await ExecuteNonQueryAsync(sql, CreateParameter("@UserId", userId));
    }
}
