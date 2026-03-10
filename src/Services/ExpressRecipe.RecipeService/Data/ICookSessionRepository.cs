using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecipeService.Data;

public interface ICookSessionRepository
{
    Task<Guid> LogSessionAsync(Guid userId, LogCookSessionRequest req,
        CancellationToken ct = default);
    Task<List<CookSessionDto>> GetSessionsAsync(Guid userId, Guid? recipeId = null,
        int limit = 20, CancellationToken ct = default);
    Task<int> GetCookCountAsync(Guid userId, Guid recipeId, CancellationToken ct = default);

    Task<Guid> SaveNoteAsync(Guid userId, SaveRecipeNoteRequest req, CancellationToken ct = default);
    Task<List<RecipeNoteDto>> GetNotesAsync(Guid userId, Guid recipeId, CancellationToken ct = default);
    Task DismissNoteAsync(Guid noteId, Guid userId, CancellationToken ct = default);
    Task DeleteNoteAsync(Guid noteId, Guid userId, CancellationToken ct = default);
}

public sealed class CookSessionRepository : ICookSessionRepository
{
    private readonly string _connectionString;

    public CookSessionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid> LogSessionAsync(Guid userId, LogCookSessionRequest req,
        CancellationToken ct = default)
    {
        Guid sessionId = Guid.NewGuid();
        const string sql = @"INSERT INTO UserCookSession
            (Id,UserId,HouseholdId,RecipeId,CookedAt,ServingsMade,Rating,WouldMakeAgain,
             GeneralNotes,IssueNotes,FixNotes,AIHelpUsed,CreatedAt)
            VALUES(@Id,@User,@Household,@Recipe,@Cooked,@Servings,@Rating,@Again,
                   @General,@Issue,@Fix,@AI,GETUTCDATE())";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id",        sessionId);
        cmd.Parameters.AddWithValue("@User",      userId);
        cmd.Parameters.AddWithValue("@Household", req.HouseholdId);
        cmd.Parameters.AddWithValue("@Recipe",    req.RecipeId);
        cmd.Parameters.AddWithValue("@Cooked",    (object?)(req.CookedAt) ?? DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@Servings",  (object?)req.ServingsMade ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Rating",    (object?)req.Rating ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Again",     (object?)req.WouldMakeAgain ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@General",   (object?)req.GeneralNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Issue",     (object?)req.IssueNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Fix",       (object?)req.FixNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AI",        req.AIHelpUsed);
        await cmd.ExecuteNonQueryAsync(ct);

        if (req.Rating.HasValue)
        {
            await UpdateAggregateRatingAsync(conn, userId, req.RecipeId, req.Rating.Value,
                req.WouldMakeAgain, ct);
        }

        return sessionId;
    }

    private static async Task UpdateAggregateRatingAsync(SqlConnection conn, Guid userId,
        Guid recipeId, int rating, bool? wouldMakeAgain, CancellationToken ct)
    {
        const string upsertRating = @"
            MERGE UserRecipeRating AS t
            USING (SELECT @UserId, @RecipeId) AS s(UserId, RecipeId)
            ON t.UserId=s.UserId AND t.RecipeId=s.RecipeId
            WHEN MATCHED THEN UPDATE SET
                Rating=@Rating,
                WouldMakeAgain=COALESCE(@Again, WouldMakeAgain),
                MadeItCount=MadeItCount+1,
                UpdatedAt=GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT
                (Id,UserId,RecipeId,Rating,WouldMakeAgain,MadeItCount,CreatedAt)
                VALUES(NEWID(),@UserId,@RecipeId,@Rating,@Again,1,GETUTCDATE());
            EXEC UpdateRecipeRating @RecipeId;";
        await using SqlCommand cmd = new(upsertRating, conn);
        cmd.Parameters.AddWithValue("@UserId",   userId);
        cmd.Parameters.AddWithValue("@RecipeId", recipeId);
        cmd.Parameters.AddWithValue("@Rating",   rating);
        cmd.Parameters.AddWithValue("@Again",    (object?)wouldMakeAgain ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<CookSessionDto>> GetSessionsAsync(Guid userId, Guid? recipeId,
        int limit, CancellationToken ct = default)
    {
        string sql = @"SELECT TOP (@Limit)
            cs.Id, cs.RecipeId, r.Title AS RecipeName, cs.CookedAt,
            cs.ServingsMade, cs.Rating, cs.WouldMakeAgain,
            cs.GeneralNotes, cs.IssueNotes, cs.FixNotes, cs.AIHelpUsed, cs.CreatedAt
        FROM UserCookSession cs
        JOIN Recipe r ON r.Id = cs.RecipeId
        WHERE cs.UserId = @UserId"
            + (recipeId.HasValue ? " AND cs.RecipeId = @RecipeId" : "")
            + " ORDER BY cs.CookedAt DESC";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Limit",  limit);
        cmd.Parameters.AddWithValue("@UserId", userId);
        if (recipeId.HasValue) { cmd.Parameters.AddWithValue("@RecipeId", recipeId.Value); }
        List<CookSessionDto> result = new();
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new CookSessionDto
            {
                Id             = reader.GetGuid(0),
                RecipeId       = reader.GetGuid(1),
                RecipeName     = reader.GetString(2),
                CookedAt       = reader.GetDateTime(3),
                ServingsMade   = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Rating         = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                WouldMakeAgain = reader.IsDBNull(6) ? null : reader.GetBoolean(6),
                GeneralNotes   = reader.IsDBNull(7) ? null : reader.GetString(7),
                IssueNotes     = reader.IsDBNull(8) ? null : reader.GetString(8),
                FixNotes       = reader.IsDBNull(9) ? null : reader.GetString(9),
                AIHelpUsed     = reader.GetBoolean(10),
                CreatedAt      = reader.GetDateTime(11)
            });
        }
        return result;
    }

    public async Task<int> GetCookCountAsync(Guid userId, Guid recipeId,
        CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "SELECT COUNT(*) FROM UserCookSession WHERE UserId=@U AND RecipeId=@R", conn);
        cmd.Parameters.AddWithValue("@U", userId);
        cmd.Parameters.AddWithValue("@R", recipeId);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<Guid> SaveNoteAsync(Guid userId, SaveRecipeNoteRequest req,
        CancellationToken ct = default)
    {
        Guid id = Guid.NewGuid();
        const string sql = @"INSERT INTO UserRecipeNote
            (Id,UserId,RecipeId,NoteType,NoteText,IsFromAI,IsDismissed,DisplayOrder,CreatedAt)
            VALUES(@Id,@User,@Recipe,@Type,@Text,@AI,0,@Order,GETUTCDATE())";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Id",     id);
        cmd.Parameters.AddWithValue("@User",   userId);
        cmd.Parameters.AddWithValue("@Recipe", req.RecipeId);
        cmd.Parameters.AddWithValue("@Type",   req.NoteType);
        cmd.Parameters.AddWithValue("@Text",   req.NoteText);
        cmd.Parameters.AddWithValue("@AI",     req.IsFromAI);
        cmd.Parameters.AddWithValue("@Order",  req.DisplayOrder);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<List<RecipeNoteDto>> GetNotesAsync(Guid userId, Guid recipeId,
        CancellationToken ct = default)
    {
        const string sql = @"SELECT Id,RecipeId,NoteType,NoteText,IsFromAI,IsDismissed,
            DisplayOrder,CreatedAt
        FROM UserRecipeNote
        WHERE UserId=@UserId AND RecipeId=@RecipeId AND IsDismissed=0
        ORDER BY DisplayOrder, CreatedAt";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",   userId);
        cmd.Parameters.AddWithValue("@RecipeId", recipeId);
        List<RecipeNoteDto> notes = new();
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            notes.Add(new RecipeNoteDto
            {
                Id           = reader.GetGuid(0),
                RecipeId     = reader.GetGuid(1),
                NoteType     = reader.GetString(2),
                NoteText     = reader.GetString(3),
                IsFromAI     = reader.GetBoolean(4),
                IsDismissed  = reader.GetBoolean(5),
                DisplayOrder = reader.GetInt32(6),
                CreatedAt    = reader.GetDateTime(7)
            });
        }
        return notes;
    }

    public async Task DismissNoteAsync(Guid noteId, Guid userId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "UPDATE UserRecipeNote SET IsDismissed=1, UpdatedAt=GETUTCDATE() " +
            "WHERE Id=@Id AND UserId=@UserId", conn);
        cmd.Parameters.AddWithValue("@Id",     noteId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteNoteAsync(Guid noteId, Guid userId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "DELETE FROM UserRecipeNote WHERE Id=@Id AND UserId=@UserId", conn);
        cmd.Parameters.AddWithValue("@Id",     noteId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
