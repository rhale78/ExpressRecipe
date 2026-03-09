using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Data;

public interface IMealCourseRepository
{
    Task<List<MealCourseDto>> GetCoursesAsync(Guid plannedMealId, CancellationToken ct = default);
    Task<Guid> AddCourseAsync(Guid plannedMealId, string courseType, Guid? recipeId,
        string? customName, decimal servings, int sortOrder, CancellationToken ct = default);
    Task UpdateCourseAsync(Guid courseId, Guid? recipeId, string? customName,
        decimal servings, int sortOrder, CancellationToken ct = default);
    Task DeleteCourseAsync(Guid courseId, CancellationToken ct = default);
    Task ReorderCoursesAsync(List<(Guid CourseId, int SortOrder)> ordering, CancellationToken ct = default);
    Task MarkCourseCompletedAsync(Guid courseId, CancellationToken ct = default);
}

public sealed record MealCourseDto
{
    public Guid Id { get; init; }
    public Guid PlannedMealId { get; init; }
    public string CourseType { get; init; } = string.Empty;
    public Guid? RecipeId { get; init; }
    public string? RecipeName { get; init; }
    public string? CustomName { get; init; }
    public decimal Servings { get; init; }
    public int SortOrder { get; init; }
    public bool IsCompleted { get; init; }
}

public class MealCourseRepository : IMealCourseRepository
{
    private readonly string _connectionString;

    public MealCourseRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<MealCourseDto>> GetCoursesAsync(Guid plannedMealId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, PlannedMealId, CourseType, RecipeId, NULL AS RecipeName, CustomName,
                   Servings, SortOrder, IsCompleted
            FROM MealCourse
            WHERE PlannedMealId = @PlannedMealId
            ORDER BY SortOrder, CreatedAt";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);

        List<MealCourseDto> courses = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            courses.Add(MapCourse(reader));
        }

        return courses;
    }

    public async Task<Guid> AddCourseAsync(Guid plannedMealId, string courseType, Guid? recipeId,
        string? customName, decimal servings, int sortOrder, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO MealCourse (Id, PlannedMealId, CourseType, RecipeId, CustomName, Servings, SortOrder)
            OUTPUT INSERTED.Id
            VALUES (NEWID(), @PlannedMealId, @CourseType, @RecipeId, @CustomName, @Servings, @SortOrder)";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PlannedMealId", plannedMealId);
        command.Parameters.AddWithValue("@CourseType", courseType);
        command.Parameters.AddWithValue("@RecipeId", recipeId.HasValue ? recipeId.Value : (object)DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Servings", servings);
        command.Parameters.AddWithValue("@SortOrder", sortOrder);

        return (Guid)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateCourseAsync(Guid courseId, Guid? recipeId, string? customName,
        decimal servings, int sortOrder, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE MealCourse
            SET RecipeId = @RecipeId, CustomName = @CustomName, Servings = @Servings, SortOrder = @SortOrder
            WHERE Id = @CourseId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CourseId", courseId);
        command.Parameters.AddWithValue("@RecipeId", recipeId.HasValue ? recipeId.Value : (object)DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Servings", servings);
        command.Parameters.AddWithValue("@SortOrder", sortOrder);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteCourseAsync(Guid courseId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM MealCourse WHERE Id = @CourseId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CourseId", courseId);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task ReorderCoursesAsync(List<(Guid CourseId, int SortOrder)> ordering, CancellationToken ct = default)
    {
        const string sql = "UPDATE MealCourse SET SortOrder = @SortOrder WHERE Id = @Id";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        foreach ((Guid courseId, int sortOrder) in ordering)
        {
            await using SqlCommand command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", courseId);
            command.Parameters.AddWithValue("@SortOrder", sortOrder);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task MarkCourseCompletedAsync(Guid courseId, CancellationToken ct = default)
    {
        const string sql = "UPDATE MealCourse SET IsCompleted = 1 WHERE Id = @CourseId";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CourseId", courseId);

        await command.ExecuteNonQueryAsync(ct);
    }

    private static MealCourseDto MapCourse(SqlDataReader reader) =>
        new()
        {
            Id = reader.GetGuid(0),
            PlannedMealId = reader.GetGuid(1),
            CourseType = reader.GetString(2),
            RecipeId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
            RecipeName = reader.IsDBNull(4) ? null : reader.GetString(4),
            CustomName = reader.IsDBNull(5) ? null : reader.GetString(5),
            Servings = reader.GetDecimal(6),
            SortOrder = reader.GetInt32(7),
            IsCompleted = reader.GetBoolean(8)
        };
}
