using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

public interface IMealVotingRepository
{
    Task UpsertVoteAsync(Guid plannedMealId, Guid voterId, string reaction, string? comment, CancellationToken ct = default);
    Task DeleteVoteAsync(Guid plannedMealId, Guid voterId, CancellationToken ct = default);
    Task<VoteSummaryDto> GetVoteSummaryAsync(Guid plannedMealId, CancellationToken ct = default);
    Task UpsertPostMealReviewAsync(Guid plannedMealId, Guid reviewerId, byte rating, string? comment, bool? wouldHaveAgain, CancellationToken ct = default);
    Task UpsertCourseReviewAsync(Guid plannedMealId, Guid recipeId, string? courseType, Guid reviewerId, byte rating, string? comment, CancellationToken ct = default);
    Task<List<PostMealReviewDto>> GetReviewsAsync(Guid plannedMealId, CancellationToken ct = default);
}

public sealed record VoteSummaryDto
{
    public Guid PlannedMealId { get; init; }
    public int Love { get; init; }
    public int Like { get; init; }
    public int Neutral { get; init; }
    public int Dislike { get; init; }
    public int Veto { get; init; }
    public bool HasVeto => Veto > 0;
    public List<VoterReactionDto> Reactions { get; init; } = new();
}

public sealed record VoterReactionDto
{
    public Guid VoterId { get; init; }
    public string Reaction { get; init; } = string.Empty;
    public string? Comment { get; init; }
}

public sealed record PostMealReviewDto
{
    public Guid ReviewerId { get; init; }
    public byte MealRating { get; init; }
    public string? Comment { get; init; }
    public bool? WouldHaveAgain { get; init; }
    public DateTime ReviewedAt { get; init; }
    public List<CourseReviewDto> CourseReviews { get; init; } = new();
}

public sealed record CourseReviewDto
{
    public Guid RecipeId { get; init; }
    public string? CourseType { get; init; }
    public byte Rating { get; init; }
    public string? Comment { get; init; }
}

public sealed class MealVotingRepository : IMealVotingRepository
{
    private readonly string _connectionString;

    public MealVotingRepository(string connectionString) { _connectionString = connectionString; }

    public async Task UpsertVoteAsync(Guid plannedMealId, Guid voterId,
        string reaction, string? comment, CancellationToken ct = default)
    {
        const string sql = @"MERGE PlannedMealVote AS target
            USING (SELECT @PlannedMealId, @VoterId) AS source (PlannedMealId, VoterId)
            ON target.PlannedMealId = source.PlannedMealId AND target.VoterId = source.VoterId
            WHEN MATCHED THEN UPDATE SET Reaction=@Reaction, Comment=@Comment, VotedAt=GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (Id, PlannedMealId, VoterId, Reaction, Comment, VotedAt)
                VALUES (NEWID(), @PlannedMealId, @VoterId, @Reaction, @Comment, GETUTCDATE());";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
        cmd.Parameters.AddWithValue("@VoterId",       voterId);
        cmd.Parameters.AddWithValue("@Reaction",      reaction);
        cmd.Parameters.AddWithValue("@Comment",       comment ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteVoteAsync(Guid plannedMealId, Guid voterId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new("DELETE FROM PlannedMealVote WHERE PlannedMealId=@PlanMealId AND VoterId=@VoterId", conn);
        cmd.Parameters.AddWithValue("@PlanMealId", plannedMealId);
        cmd.Parameters.AddWithValue("@VoterId",    voterId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<VoteSummaryDto> GetVoteSummaryAsync(Guid plannedMealId, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new("SELECT VoterId, Reaction, Comment FROM PlannedMealVote WHERE PlannedMealId=@Id", conn);
        cmd.Parameters.AddWithValue("@Id", plannedMealId);
        List<VoterReactionDto> reactions = new();
        await using (SqlDataReader r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                reactions.Add(new VoterReactionDto
                {
                    VoterId  = r.GetGuid(0),
                    Reaction = r.GetString(1),
                    Comment  = r.IsDBNull(2) ? null : r.GetString(2)
                });
            }
        }
        return new VoteSummaryDto
        {
            PlannedMealId = plannedMealId,
            Love    = reactions.Count(x => x.Reaction == "Love"),
            Like    = reactions.Count(x => x.Reaction == "Like"),
            Neutral = reactions.Count(x => x.Reaction == "Neutral"),
            Dislike = reactions.Count(x => x.Reaction == "Dislike"),
            Veto    = reactions.Count(x => x.Reaction == "Veto"),
            Reactions = reactions
        };
    }

    public async Task UpsertPostMealReviewAsync(Guid plannedMealId, Guid reviewerId,
        byte rating, string? comment, bool? wouldHaveAgain, CancellationToken ct = default)
    {
        const string sql = @"MERGE PostMealReview AS target
            USING (SELECT @PlannedMealId, @ReviewerId) AS source (PlannedMealId, ReviewerId)
            ON target.PlannedMealId = source.PlannedMealId AND target.ReviewerId = source.ReviewerId
            WHEN MATCHED THEN UPDATE SET MealRating=@Rating, Comment=@Comment, WouldHaveAgain=@WouldHaveAgain, ReviewedAt=GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (Id, PlannedMealId, ReviewerId, MealRating, Comment, WouldHaveAgain, ReviewedAt)
                VALUES (NEWID(), @PlannedMealId, @ReviewerId, @Rating, @Comment, @WouldHaveAgain, GETUTCDATE());";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlannedMealId",  plannedMealId);
        cmd.Parameters.AddWithValue("@ReviewerId",     reviewerId);
        cmd.Parameters.AddWithValue("@Rating",         rating);
        cmd.Parameters.AddWithValue("@Comment",        comment ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@WouldHaveAgain", wouldHaveAgain.HasValue ? wouldHaveAgain.Value : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertCourseReviewAsync(Guid plannedMealId, Guid recipeId, string? courseType,
        Guid reviewerId, byte rating, string? comment, CancellationToken ct = default)
    {
        const string sql = @"MERGE PostMealCourseReview AS target
            USING (SELECT @PlannedMealId, @RecipeId, @ReviewerId) AS source (PlannedMealId, RecipeId, ReviewerId)
            ON target.PlannedMealId = source.PlannedMealId
               AND target.RecipeId = source.RecipeId
               AND target.ReviewerId = source.ReviewerId
            WHEN MATCHED THEN UPDATE SET CourseType=@CourseType, Rating=@Rating, Comment=@Comment, ReviewedAt=GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (Id, PlannedMealId, RecipeId, CourseType, ReviewerId, Rating, Comment, ReviewedAt)
                VALUES (NEWID(), @PlannedMealId, @RecipeId, @CourseType, @ReviewerId, @Rating, @Comment, GETUTCDATE());";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
        cmd.Parameters.AddWithValue("@RecipeId",      recipeId);
        cmd.Parameters.AddWithValue("@CourseType",    courseType ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ReviewerId",    reviewerId);
        cmd.Parameters.AddWithValue("@Rating",        rating);
        cmd.Parameters.AddWithValue("@Comment",       comment ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<PostMealReviewDto>> GetReviewsAsync(Guid plannedMealId, CancellationToken ct = default)
    {
        const string reviewSql = @"SELECT ReviewerId, MealRating, Comment, WouldHaveAgain, ReviewedAt
            FROM PostMealReview WHERE PlannedMealId=@Id ORDER BY ReviewedAt DESC";
        const string courseSql = @"SELECT ReviewerId, RecipeId, CourseType, Rating, Comment
            FROM PostMealCourseReview WHERE PlannedMealId=@Id";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);

        List<PostMealReviewDto> reviews = new();
        await using (SqlCommand cmd = new(reviewSql, conn))
        {
            cmd.Parameters.AddWithValue("@Id", plannedMealId);
            await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                reviews.Add(new PostMealReviewDto
                {
                    ReviewerId     = r.GetGuid(0),
                    MealRating     = r.GetByte(1),
                    Comment        = r.IsDBNull(2) ? null : r.GetString(2),
                    WouldHaveAgain = r.IsDBNull(3) ? null : r.GetBoolean(3),
                    ReviewedAt     = r.GetDateTime(4)
                });
            }
        }

        // Build a lookup: reviewerId -> list of course reviews
        Dictionary<Guid, List<CourseReviewDto>> coursesByReviewer = new();
        await using (SqlCommand cmd = new(courseSql, conn))
        {
            cmd.Parameters.AddWithValue("@Id", plannedMealId);
            await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                Guid reviewerId = r.GetGuid(0);
                if (!coursesByReviewer.TryGetValue(reviewerId, out List<CourseReviewDto>? list))
                {
                    list = new List<CourseReviewDto>();
                    coursesByReviewer[reviewerId] = list;
                }
                list.Add(new CourseReviewDto
                {
                    RecipeId   = r.GetGuid(1),
                    CourseType = r.IsDBNull(2) ? null : r.GetString(2),
                    Rating     = r.GetByte(3),
                    Comment    = r.IsDBNull(4) ? null : r.GetString(4)
                });
            }
        }

        foreach (PostMealReviewDto review in reviews)
        {
            if (coursesByReviewer.TryGetValue(review.ReviewerId, out List<CourseReviewDto>? courses))
            {
                review.CourseReviews.AddRange(courses);
            }
        }

        return reviews;
    }
}
