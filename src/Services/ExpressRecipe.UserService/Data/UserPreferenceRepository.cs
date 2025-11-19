using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IUserPreferenceRepository
{
    // Preferred Cuisines
    Task<List<UserPreferredCuisineDto>> GetUserPreferredCuisinesAsync(Guid userId);
    Task<Guid> AddPreferredCuisineAsync(Guid userId, AddUserPreferredCuisineRequest request);
    Task<bool> UpdatePreferredCuisineAsync(Guid userId, Guid cuisineId, UpdateUserPreferredCuisineRequest request);
    Task<bool> RemovePreferredCuisineAsync(Guid userId, Guid cuisineId);

    // Health Goals
    Task<List<UserHealthGoalDto>> GetUserHealthGoalsAsync(Guid userId);
    Task<Guid> AddHealthGoalAsync(Guid userId, AddUserHealthGoalRequest request);
    Task<bool> UpdateHealthGoalAsync(Guid userId, Guid goalId, UpdateUserHealthGoalRequest request);
    Task<bool> RemoveHealthGoalAsync(Guid userId, Guid goalId);

    // Favorite Ingredients
    Task<List<UserFavoriteIngredientDto>> GetUserFavoriteIngredientsAsync(Guid userId);
    Task<Guid> AddFavoriteIngredientAsync(Guid userId, AddUserFavoriteIngredientRequest request);
    Task<bool> UpdateFavoriteIngredientAsync(Guid userId, Guid ingredientId, UpdateUserFavoriteIngredientRequest request);
    Task<bool> RemoveFavoriteIngredientAsync(Guid userId, Guid ingredientId);

    // Disliked Ingredients
    Task<List<UserDislikedIngredientDto>> GetUserDislikedIngredientsAsync(Guid userId);
    Task<Guid> AddDislikedIngredientAsync(Guid userId, AddUserDislikedIngredientRequest request);
    Task<bool> UpdateDislikedIngredientAsync(Guid userId, Guid ingredientId, UpdateUserDislikedIngredientRequest request);
    Task<bool> RemoveDislikedIngredientAsync(Guid userId, Guid ingredientId);
}

public class UserPreferenceRepository : SqlHelper, IUserPreferenceRepository
{
    public UserPreferenceRepository(string connectionString) : base(connectionString)
    {
    }

    #region Preferred Cuisines

    public async Task<List<UserPreferredCuisineDto>> GetUserPreferredCuisinesAsync(Guid userId)
    {
        const string sql = @"
            SELECT upc.UserId, upc.CuisineId, upc.PreferenceLevel, upc.Notes,
                   c.Name AS CuisineName, c.Description AS CuisineDescription
            FROM UserPreferredCuisine upc
            INNER JOIN Cuisine c ON upc.CuisineId = c.Id
            WHERE upc.UserId = @UserId
            ORDER BY upc.PreferenceLevel DESC, c.Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserPreferredCuisineDto
            {
                UserId = GetGuid(reader, "UserId"),
                CuisineId = GetGuid(reader, "CuisineId"),
                CuisineName = GetString(reader, "CuisineName") ?? string.Empty,
                CuisineDescription = GetString(reader, "CuisineDescription"),
                PreferenceLevel = GetInt(reader, "PreferenceLevel") ?? 3,
                Notes = GetString(reader, "Notes")
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<Guid> AddPreferredCuisineAsync(Guid userId, AddUserPreferredCuisineRequest request)
    {
        const string sql = @"
            INSERT INTO UserPreferredCuisine (Id, UserId, CuisineId, PreferenceLevel, Notes)
            VALUES (@Id, @UserId, @CuisineId, @PreferenceLevel, @Notes)";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@UserId", userId),
            CreateParameter("@CuisineId", request.CuisineId),
            CreateParameter("@PreferenceLevel", request.PreferenceLevel),
            CreateParameter("@Notes", request.Notes));

        return id;
    }

    public async Task<bool> UpdatePreferredCuisineAsync(Guid userId, Guid cuisineId, UpdateUserPreferredCuisineRequest request)
    {
        const string sql = @"
            UPDATE UserPreferredCuisine
            SET PreferenceLevel = @PreferenceLevel,
                Notes = @Notes
            WHERE UserId = @UserId AND CuisineId = @CuisineId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@CuisineId", cuisineId),
            CreateParameter("@PreferenceLevel", request.PreferenceLevel),
            CreateParameter("@Notes", request.Notes));

        return rowsAffected > 0;
    }

    public async Task<bool> RemovePreferredCuisineAsync(Guid userId, Guid cuisineId)
    {
        const string sql = @"
            DELETE FROM UserPreferredCuisine
            WHERE UserId = @UserId AND CuisineId = @CuisineId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@CuisineId", cuisineId));

        return rowsAffected > 0;
    }

    #endregion

    #region Health Goals

    public async Task<List<UserHealthGoalDto>> GetUserHealthGoalsAsync(Guid userId)
    {
        const string sql = @"
            SELECT uhg.UserId, uhg.HealthGoalId, uhg.Priority, uhg.Notes, uhg.TargetDate,
                   hg.Name AS GoalName, hg.Description AS GoalDescription, hg.Category AS GoalCategory
            FROM UserHealthGoal uhg
            INNER JOIN HealthGoal hg ON uhg.HealthGoalId = hg.Id
            WHERE uhg.UserId = @UserId
            ORDER BY uhg.Priority DESC, hg.Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserHealthGoalDto
            {
                UserId = GetGuid(reader, "UserId"),
                HealthGoalId = GetGuid(reader, "HealthGoalId"),
                GoalName = GetString(reader, "GoalName") ?? string.Empty,
                GoalDescription = GetString(reader, "GoalDescription"),
                GoalCategory = GetString(reader, "GoalCategory"),
                Priority = GetInt(reader, "Priority") ?? 3,
                Notes = GetString(reader, "Notes"),
                TargetDate = GetDateTime(reader, "TargetDate")
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<Guid> AddHealthGoalAsync(Guid userId, AddUserHealthGoalRequest request)
    {
        const string sql = @"
            INSERT INTO UserHealthGoal (Id, UserId, HealthGoalId, Priority, Notes, TargetDate)
            VALUES (@Id, @UserId, @HealthGoalId, @Priority, @Notes, @TargetDate)";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@UserId", userId),
            CreateParameter("@HealthGoalId", request.HealthGoalId),
            CreateParameter("@Priority", request.Priority),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@TargetDate", request.TargetDate));

        return id;
    }

    public async Task<bool> UpdateHealthGoalAsync(Guid userId, Guid goalId, UpdateUserHealthGoalRequest request)
    {
        const string sql = @"
            UPDATE UserHealthGoal
            SET Priority = @Priority,
                Notes = @Notes,
                TargetDate = @TargetDate
            WHERE UserId = @UserId AND HealthGoalId = @HealthGoalId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@HealthGoalId", goalId),
            CreateParameter("@Priority", request.Priority),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@TargetDate", request.TargetDate));

        return rowsAffected > 0;
    }

    public async Task<bool> RemoveHealthGoalAsync(Guid userId, Guid goalId)
    {
        const string sql = @"
            DELETE FROM UserHealthGoal
            WHERE UserId = @UserId AND HealthGoalId = @HealthGoalId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@HealthGoalId", goalId));

        return rowsAffected > 0;
    }

    #endregion

    #region Favorite Ingredients

    public async Task<List<UserFavoriteIngredientDto>> GetUserFavoriteIngredientsAsync(Guid userId)
    {
        const string sql = @"
            SELECT UserId, IngredientId, Rating, Notes
            FROM UserFavoriteIngredient
            WHERE UserId = @UserId
            ORDER BY Rating DESC, IngredientId";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserFavoriteIngredientDto
            {
                UserId = GetGuid(reader, "UserId"),
                IngredientId = GetGuid(reader, "IngredientId"),
                Rating = GetInt(reader, "Rating"),
                Notes = GetString(reader, "Notes")
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<Guid> AddFavoriteIngredientAsync(Guid userId, AddUserFavoriteIngredientRequest request)
    {
        const string sql = @"
            INSERT INTO UserFavoriteIngredient (Id, UserId, IngredientId, Rating, Notes)
            VALUES (@Id, @UserId, @IngredientId, @Rating, @Notes)";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@UserId", userId),
            CreateParameter("@IngredientId", request.IngredientId),
            CreateParameter("@Rating", request.Rating),
            CreateParameter("@Notes", request.Notes));

        return id;
    }

    public async Task<bool> UpdateFavoriteIngredientAsync(Guid userId, Guid ingredientId, UpdateUserFavoriteIngredientRequest request)
    {
        const string sql = @"
            UPDATE UserFavoriteIngredient
            SET Rating = @Rating,
                Notes = @Notes
            WHERE UserId = @UserId AND IngredientId = @IngredientId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@IngredientId", ingredientId),
            CreateParameter("@Rating", request.Rating),
            CreateParameter("@Notes", request.Notes));

        return rowsAffected > 0;
    }

    public async Task<bool> RemoveFavoriteIngredientAsync(Guid userId, Guid ingredientId)
    {
        const string sql = @"
            DELETE FROM UserFavoriteIngredient
            WHERE UserId = @UserId AND IngredientId = @IngredientId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@IngredientId", ingredientId));

        return rowsAffected > 0;
    }

    #endregion

    #region Disliked Ingredients

    public async Task<List<UserDislikedIngredientDto>> GetUserDislikedIngredientsAsync(Guid userId)
    {
        const string sql = @"
            SELECT UserId, IngredientId, Severity, Reason
            FROM UserDislikedIngredient
            WHERE UserId = @UserId
            ORDER BY Severity DESC, IngredientId";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserDislikedIngredientDto
            {
                UserId = GetGuid(reader, "UserId"),
                IngredientId = GetGuid(reader, "IngredientId"),
                Severity = GetInt(reader, "Severity"),
                Reason = GetString(reader, "Reason")
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<Guid> AddDislikedIngredientAsync(Guid userId, AddUserDislikedIngredientRequest request)
    {
        const string sql = @"
            INSERT INTO UserDislikedIngredient (Id, UserId, IngredientId, Severity, Reason)
            VALUES (@Id, @UserId, @IngredientId, @Severity, @Reason)";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@UserId", userId),
            CreateParameter("@IngredientId", request.IngredientId),
            CreateParameter("@Severity", request.Severity),
            CreateParameter("@Reason", request.Reason));

        return id;
    }

    public async Task<bool> UpdateDislikedIngredientAsync(Guid userId, Guid ingredientId, UpdateUserDislikedIngredientRequest request)
    {
        const string sql = @"
            UPDATE UserDislikedIngredient
            SET Severity = @Severity,
                Reason = @Reason
            WHERE UserId = @UserId AND IngredientId = @IngredientId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@IngredientId", ingredientId),
            CreateParameter("@Severity", request.Severity),
            CreateParameter("@Reason", request.Reason));

        return rowsAffected > 0;
    }

    public async Task<bool> RemoveDislikedIngredientAsync(Guid userId, Guid ingredientId)
    {
        const string sql = @"
            DELETE FROM UserDislikedIngredient
            WHERE UserId = @UserId AND IngredientId = @IngredientId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@IngredientId", ingredientId));

        return rowsAffected > 0;
    }

    #endregion
}
