using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Recipe;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// Repository for recipe data access using ADO.NET
/// </summary>
public class RecipeRepository : SqlHelper, IRecipeRepository
{
    public RecipeRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<Guid> CreateRecipeAsync(CreateRecipeRequest request, Guid createdBy)
    {
        const string sql = @"
            INSERT INTO Recipe (
                Name, Description, Category, Cuisine, DifficultyLevel,
                PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                ImageUrl, VideoUrl, Instructions, Notes,
                IsPublic, SourceUrl, AuthorId, CreatedBy, CreatedAt
            )
            OUTPUT INSERTED.Id
            VALUES (
                @Name, @Description, @Category, @Cuisine, @DifficultyLevel,
                @PrepTimeMinutes, @CookTimeMinutes, @TotalTimeMinutes, @Servings,
                @ImageUrl, @VideoUrl, @Instructions, @Notes,
                @IsPublic, @SourceUrl, @AuthorId, @CreatedBy, GETUTCDATE()
            )";

        var totalTime = (request.PrepTimeMinutes ?? 0) + (request.CookTimeMinutes ?? 0);
        if (totalTime == 0) totalTime = request.TotalTimeMinutes ?? 0;

        var recipeId = await ExecuteScalarAsync<Guid>(sql,
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@Category", (object?)request.Category ?? DBNull.Value),
            new SqlParameter("@Cuisine", (object?)request.Cuisine ?? DBNull.Value),
            new SqlParameter("@DifficultyLevel", (object?)request.DifficultyLevel ?? DBNull.Value),
            new SqlParameter("@PrepTimeMinutes", (object?)request.PrepTimeMinutes ?? DBNull.Value),
            new SqlParameter("@CookTimeMinutes", (object?)request.CookTimeMinutes ?? DBNull.Value),
            new SqlParameter("@TotalTimeMinutes", totalTime > 0 ? totalTime : DBNull.Value),
            new SqlParameter("@Servings", (object?)request.Servings ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)request.ImageUrl ?? DBNull.Value),
            new SqlParameter("@VideoUrl", (object?)request.VideoUrl ?? DBNull.Value),
            new SqlParameter("@Instructions", (object?)request.Instructions ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value),
            new SqlParameter("@IsPublic", request.IsPublic),
            new SqlParameter("@SourceUrl", (object?)request.SourceUrl ?? DBNull.Value),
            new SqlParameter("@AuthorId", request.CreatedBy),
            new SqlParameter("@CreatedBy", createdBy)
        );

        return recipeId;
    }

    public async Task AddRecipeIngredientsAsync(Guid recipeId, List<RecipeIngredientDto> ingredients, Guid? createdBy = null)
    {
        if (!ingredients.Any()) return;

        const string sql = @"
            INSERT INTO RecipeIngredient (
                RecipeId, IngredientId, BaseIngredientId, IngredientName,
                Quantity, Unit, OrderIndex, PreparationNote,
                IsOptional, SubstituteNotes, CreatedBy, CreatedAt
            )
            VALUES (
                @RecipeId, @IngredientId, @BaseIngredientId, @IngredientName,
                @Quantity, @Unit, @OrderIndex, @PreparationNote,
                @IsOptional, @SubstituteNotes, @CreatedBy, GETUTCDATE()
            )";

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var ingredient in ingredients)
            {
                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@RecipeId", recipeId);
                command.Parameters.AddWithValue("@IngredientId", (object?)ingredient.IngredientId ?? DBNull.Value);
                command.Parameters.AddWithValue("@BaseIngredientId", (object?)ingredient.BaseIngredientId ?? DBNull.Value);
                command.Parameters.AddWithValue("@IngredientName", (object?)ingredient.IngredientName ?? DBNull.Value);
                command.Parameters.AddWithValue("@Quantity", (object?)ingredient.Quantity ?? DBNull.Value);
                command.Parameters.AddWithValue("@Unit", (object?)ingredient.Unit ?? DBNull.Value);
                command.Parameters.AddWithValue("@OrderIndex", ingredient.OrderIndex);
                command.Parameters.AddWithValue("@PreparationNote", (object?)ingredient.PreparationNote ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsOptional", ingredient.IsOptional);
                command.Parameters.AddWithValue("@SubstituteNotes", (object?)ingredient.SubstituteNotes ?? DBNull.Value);
                command.Parameters.AddWithValue("@CreatedBy", (object?)createdBy ?? DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddRecipeNutritionAsync(Guid recipeId, RecipeNutritionDto nutrition)
    {
        const string sql = @"
            INSERT INTO RecipeNutrition (
                RecipeId, ServingSize, Calories, TotalFat, SaturatedFat, TransFat,
                Cholesterol, Sodium, TotalCarbohydrates, DietaryFiber, Sugars, Protein,
                VitaminD, Calcium, Iron, Potassium, CreatedAt
            )
            VALUES (
                @RecipeId, @ServingSize, @Calories, @TotalFat, @SaturatedFat, @TransFat,
                @Cholesterol, @Sodium, @TotalCarbohydrates, @DietaryFiber, @Sugars, @Protein,
                @VitaminD, @Calcium, @Iron, @Potassium, GETUTCDATE()
            )";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@ServingSize", (object?)nutrition.ServingSize ?? DBNull.Value),
            new SqlParameter("@Calories", (object?)nutrition.Calories ?? DBNull.Value),
            new SqlParameter("@TotalFat", (object?)nutrition.TotalFat ?? DBNull.Value),
            new SqlParameter("@SaturatedFat", (object?)nutrition.SaturatedFat ?? DBNull.Value),
            new SqlParameter("@TransFat", (object?)nutrition.TransFat ?? DBNull.Value),
            new SqlParameter("@Cholesterol", (object?)nutrition.Cholesterol ?? DBNull.Value),
            new SqlParameter("@Sodium", (object?)nutrition.Sodium ?? DBNull.Value),
            new SqlParameter("@TotalCarbohydrates", (object?)nutrition.TotalCarbohydrates ?? DBNull.Value),
            new SqlParameter("@DietaryFiber", (object?)nutrition.DietaryFiber ?? DBNull.Value),
            new SqlParameter("@Sugars", (object?)nutrition.Sugars ?? DBNull.Value),
            new SqlParameter("@Protein", (object?)nutrition.Protein ?? DBNull.Value),
            new SqlParameter("@VitaminD", (object?)nutrition.VitaminD ?? DBNull.Value),
            new SqlParameter("@Calcium", (object?)nutrition.Calcium ?? DBNull.Value),
            new SqlParameter("@Iron", (object?)nutrition.Iron ?? DBNull.Value),
            new SqlParameter("@Potassium", (object?)nutrition.Potassium ?? DBNull.Value)
        );
    }

    public async Task AddRecipeAllergensAsync(Guid recipeId, List<RecipeAllergenWarningDto> allergens)
    {
        if (!allergens.Any()) return;

        const string sql = @"
            INSERT INTO RecipeAllergenWarning (RecipeId, AllergenId, AllergenName, SourceIngredientId, CreatedAt)
            VALUES (@RecipeId, @AllergenId, @AllergenName, @SourceIngredientId, GETUTCDATE())";

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var allergen in allergens)
            {
                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@RecipeId", recipeId);
                command.Parameters.AddWithValue("@AllergenId", allergen.AllergenId);
                command.Parameters.AddWithValue("@AllergenName", allergen.AllergenName);
                command.Parameters.AddWithValue("@SourceIngredientId", (object?)allergen.SourceIngredientId ?? DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddRecipeTagsAsync(Guid recipeId, List<string> tagNames)
    {
        if (!tagNames.Any()) return;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var tagName in tagNames.Distinct())
            {
                // Get or create tag
                const string getTagSql = @"
                    SELECT Id FROM RecipeTag WHERE Name = @Name";

                Guid tagId;
                using (var command = new SqlCommand(getTagSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@Name", tagName);
                    var result = await command.ExecuteScalarAsync();

                    if (result == null)
                    {
                        // Create tag
                        const string createTagSql = @"
                            INSERT INTO RecipeTag (Name, Description)
                            OUTPUT INSERTED.Id
                            VALUES (@Name, NULL)";

                        using var createCommand = new SqlCommand(createTagSql, connection, transaction);
                        createCommand.Parameters.AddWithValue("@Name", tagName);
                        tagId = (Guid)(await createCommand.ExecuteScalarAsync())!;
                    }
                    else
                    {
                        tagId = (Guid)result;
                    }
                }

                // Add mapping (ignore if already exists)
                const string mappingSql = @"
                    IF NOT EXISTS (SELECT 1 FROM RecipeTagMapping WHERE RecipeId = @RecipeId AND TagId = @TagId)
                    BEGIN
                        INSERT INTO RecipeTagMapping (RecipeId, TagId, CreatedAt)
                        VALUES (@RecipeId, @TagId, GETUTCDATE())
                    END";

                using (var command = new SqlCommand(mappingSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@RecipeId", recipeId);
                    command.Parameters.AddWithValue("@TagId", tagId);
                    await command.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<RecipeDto?> FindDuplicateRecipeAsync(string name, Guid authorId)
    {
        const string sql = @"
            SELECT TOP 1 Id, Name, AuthorId, SourceUrl
            FROM Recipe
            WHERE Name = @Name
              AND AuthorId = @AuthorId
              AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        var recipes = await ExecuteReaderAsync(sql, reader => new RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl"))
                ? null
                : reader.GetString(reader.GetOrdinal("SourceUrl"))
        },
        new SqlParameter("@Name", name),
        new SqlParameter("@AuthorId", authorId));

        return recipes.FirstOrDefault();
    }

    public async Task<RecipeDto?> GetRecipeByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes,
                   IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE Id = @Id AND IsDeleted = 0";

        var recipes = await ExecuteReaderAsync(sql, reader => new RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                ? null
                : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                ? null
                : reader.GetString(reader.GetOrdinal("Category")),
            Cuisine = reader.IsDBNull(reader.GetOrdinal("Cuisine"))
                ? null
                : reader.GetString(reader.GetOrdinal("Cuisine")),
            DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel"))
                ? null
                : reader.GetString(reader.GetOrdinal("DifficultyLevel")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("Servings")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl"))
                ? null
                : reader.GetString(reader.GetOrdinal("ImageUrl")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("VideoUrl"))
                ? null
                : reader.GetString(reader.GetOrdinal("VideoUrl")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions"))
                ? null
                : reader.GetString(reader.GetOrdinal("Instructions")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                ? null
                : reader.GetString(reader.GetOrdinal("Notes")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl"))
                ? null
                : reader.GetString(reader.GetOrdinal("SourceUrl")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@Id", id));

        return recipes.FirstOrDefault();
    }

    public async Task UpdateRecipeInstructionsAsync(Guid recipeId, string instructions)
    {
        const string sql = @"
            UPDATE Recipe
            SET Instructions = @Instructions,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @RecipeId";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@Instructions", instructions)
        );
    }
}
