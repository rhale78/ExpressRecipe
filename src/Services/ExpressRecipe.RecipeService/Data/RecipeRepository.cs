using ExpressRecipe.Data.Common;
using Shared = ExpressRecipe.Shared.DTOs.Recipe;
using Microsoft.Data.SqlClient;
using CQ = ExpressRecipe.RecipeService.CQRS.Queries;
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

    // Convenience overload used by CQRS handlers
    public async Task<Guid> CreateRecipeAsync(Guid userId, string name, string? description, int? prepTimeMinutes, int? cookTimeMinutes, int? totalTimeMinutes, int servings, string difficulty)
    {
        var request = new ExpressRecipe.Shared.DTOs.Recipe.CreateRecipeRequest
        {
            Name = name,
            Description = description,
            PrepTimeMinutes = prepTimeMinutes,
            CookTimeMinutes = cookTimeMinutes,
            TotalTimeMinutes = totalTimeMinutes,
            Servings = servings,
            DifficultyLevel = difficulty,
            CreatedBy = userId,
            IsPublic = true
        };

        return await CreateRecipeAsync(request, userId);
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> SearchRecipesAsync(string searchTerm, int limit = 50, int offset = 0)
    {
        var sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes, IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE IsDeleted = 0 AND (Name LIKE @Term OR Description LIKE @Term)
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

        var term = $"%{searchTerm}%";

        var results = await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            Cuisine = reader.IsDBNull(reader.GetOrdinal("Cuisine")) ? null : reader.GetString(reader.GetOrdinal("Cuisine")),
            DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel")) ? null : reader.GetString(reader.GetOrdinal("DifficultyLevel")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? null : reader.GetInt32(reader.GetOrdinal("Servings")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("VideoUrl")) ? null : reader.GetString(reader.GetOrdinal("VideoUrl")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? null : reader.GetString(reader.GetOrdinal("Instructions")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@Term", term),
        new SqlParameter("@Offset", offset),
        new SqlParameter("@Limit", limit));

        return results;
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> GetAllRecipesAsync(int limit = 50, int offset = 0)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes, IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE IsDeleted = 0
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

        var results = await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            Cuisine = reader.IsDBNull(reader.GetOrdinal("Cuisine")) ? null : reader.GetString(reader.GetOrdinal("Cuisine")),
            DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel")) ? null : reader.GetString(reader.GetOrdinal("DifficultyLevel")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? null : reader.GetInt32(reader.GetOrdinal("Servings")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("VideoUrl")) ? null : reader.GetString(reader.GetOrdinal("VideoUrl")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? null : reader.GetString(reader.GetOrdinal("Instructions")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        }, new SqlParameter("@Offset", offset), new SqlParameter("@Limit", limit));

        return results;
    }

    public async Task<(decimal AverageRating, int RatingCount)> GetAverageRatingAsync(Guid recipeId)
    {
        const string sql = @"SELECT AVG(CAST(Rating AS decimal(5,2))) AS AvgRating, COUNT(*) AS Count FROM RecipeRating WHERE RecipeId = @RecipeId";

        var readerResults = await ExecuteReaderAsync(sql, reader => new
        {
            AvgRating = reader.IsDBNull(reader.GetOrdinal("AvgRating")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("AvgRating")),
            Count = reader.IsDBNull(reader.GetOrdinal("Count")) ? 0 : reader.GetInt32(reader.GetOrdinal("Count"))
        }, new SqlParameter("@RecipeId", recipeId));

        var r = readerResults.FirstOrDefault();
        return (r?.AvgRating ?? 0m, r?.Count ?? 0);
    }

    public async Task<List<string>> GetRecipeCategoriesAsync(Guid recipeId)
    {
        const string sql = "SELECT Name FROM RecipeCategory WHERE RecipeId = @RecipeId ORDER BY Name";
        var categories = await ExecuteReaderAsync(sql, reader => reader.GetString(reader.GetOrdinal("Name")), new SqlParameter("@RecipeId", recipeId));
        return categories;
    }

    public async Task<List<string>> GetRecipeTagsAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT rt.Name
            FROM RecipeTagMapping rtm
            INNER JOIN RecipeTag rt ON rtm.TagId = rt.Id
            WHERE rtm.RecipeId = @RecipeId
            ORDER BY rt.Name";

        var tags = await ExecuteReaderAsync(sql, reader => reader.GetString(reader.GetOrdinal("Name")), new SqlParameter("@RecipeId", recipeId));
        return tags;
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeIngredientDto>> GetIngredientsAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT Id, RecipeId, IngredientId, BaseIngredientId, IngredientName, Quantity, Unit, OrderIndex, PreparationNote, IsOptional, SubstituteNotes
            FROM RecipeIngredient
            WHERE RecipeId = @RecipeId
            ORDER BY OrderIndex";

        var ingredients = await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeIngredientDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
            IngredientId = reader.IsDBNull(reader.GetOrdinal("IngredientId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("IngredientId")),
            BaseIngredientId = reader.IsDBNull(reader.GetOrdinal("BaseIngredientId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("BaseIngredientId")),
            IngredientName = reader.IsDBNull(reader.GetOrdinal("IngredientName")) ? null : reader.GetString(reader.GetOrdinal("IngredientName")),
            Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Quantity")),
            Unit = reader.IsDBNull(reader.GetOrdinal("Unit")) ? null : reader.GetString(reader.GetOrdinal("Unit")),
            OrderIndex = reader.GetInt32(reader.GetOrdinal("OrderIndex")),
            PreparationNote = reader.IsDBNull(reader.GetOrdinal("PreparationNote")) ? null : reader.GetString(reader.GetOrdinal("PreparationNote")),
            IsOptional = reader.GetBoolean(reader.GetOrdinal("IsOptional")),
            SubstituteNotes = reader.IsDBNull(reader.GetOrdinal("SubstituteNotes")) ? null : reader.GetString(reader.GetOrdinal("SubstituteNotes"))
        }, new SqlParameter("@RecipeId", recipeId));

        return ingredients;
    }

    public async Task<List<CQ.RecipeInstructionDto>> GetInstructionsAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT Id, RecipeId, StepNumber, Instruction, TimeMinutes
            FROM RecipeInstruction
            WHERE RecipeId = @RecipeId
            ORDER BY StepNumber";

        var instructions = await ExecuteReaderAsync(sql, reader => new CQ.RecipeInstructionDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            StepNumber = reader.GetInt32(reader.GetOrdinal("StepNumber")),
            Instruction = reader.IsDBNull(reader.GetOrdinal("Instruction")) ? string.Empty : reader.GetString(reader.GetOrdinal("Instruction")),
            TimeMinutes = reader.IsDBNull(reader.GetOrdinal("TimeMinutes")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("TimeMinutes"))
        }, new SqlParameter("@RecipeId", recipeId));

        return instructions;
    }

    public async Task<CQ.RecipeNutritionDto?> GetNutritionAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT Calories, Protein, TotalCarbohydrates AS Carbs, TotalFat AS Fat, DietaryFiber AS Fiber, Sugars AS Sugar, Sodium
            FROM RecipeNutrition
            WHERE RecipeId = @RecipeId";

        var results = await ExecuteReaderAsync(sql, reader => new CQ.RecipeNutritionDto
        {
            Calories = reader.IsDBNull(reader.GetOrdinal("Calories")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("Calories")),
            Protein = reader.IsDBNull(reader.GetOrdinal("Protein")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Protein")),
            Carbs = reader.IsDBNull(reader.GetOrdinal("Carbs")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Carbs")),
            Fat = reader.IsDBNull(reader.GetOrdinal("Fat")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Fat")),
            Fiber = reader.IsDBNull(reader.GetOrdinal("Fiber")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Fiber")),
            Sugar = reader.IsDBNull(reader.GetOrdinal("Sugar")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Sugar")),
            Sodium = reader.IsDBNull(reader.GetOrdinal("Sodium")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Sodium"))
        }, new SqlParameter("@RecipeId", recipeId));

        return results.FirstOrDefault();
    }

    public async Task<CQ.RecipeDetailsDto?> GetRecipeDetailsAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT Id, AuthorId AS UserId, Name, Description, ImageUrl, PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings, DifficultyLevel AS Difficulty, IsPublic, CreatedAt, UpdatedAt, ISNULL(ViewCount,0) AS ViewCount, ISNULL(SaveCount,0) AS SaveCount
            FROM Recipe
            WHERE Id = @RecipeId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(sql, reader => new CQ.RecipeDetailsDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? 0 : reader.GetInt32(reader.GetOrdinal("Servings")),
            Difficulty = reader.IsDBNull(reader.GetOrdinal("Difficulty")) ? "Medium" : reader.GetString(reader.GetOrdinal("Difficulty")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
            ViewCount = reader.IsDBNull(reader.GetOrdinal("ViewCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("ViewCount")),
            SaveCount = reader.IsDBNull(reader.GetOrdinal("SaveCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("SaveCount"))
        }, new SqlParameter("@RecipeId", recipeId));

        return results.FirstOrDefault();
    }

    public async Task IncrementViewCountAsync(Guid recipeId)
    {
        const string sql = @"UPDATE Recipe SET ViewCount = ISNULL(ViewCount,0) + 1 WHERE Id = @RecipeId";
        await ExecuteNonQueryAsync(sql, new SqlParameter("@RecipeId", recipeId));
    }

    public async Task<Guid> CreateRecipeAsync(ExpressRecipe.Shared.DTOs.Recipe.CreateRecipeRequest request, Guid createdBy)
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

    public async Task AddRecipeCategoryAsync(Guid recipeId, string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return;

        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM RecipeCategory WHERE RecipeId = @RecipeId AND Name = @Name)
            BEGIN
                INSERT INTO RecipeCategory (RecipeId, Name, CreatedAt)
                VALUES (@RecipeId, @Name, GETUTCDATE())
            END";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@Name", categoryName));
    }

    public async Task AddRecipeTagAsync(Guid recipeId, string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return;

        // Reuse AddRecipeTagsAsync for single tag
        await AddRecipeTagsAsync(recipeId, new List<string> { tagName });
    }

    public async Task AddIngredientAsync(Guid recipeId, Guid? productId, string name, decimal quantity, string unit, string? notes, bool isOptional)
    {
        const string sql = @"
            INSERT INTO RecipeIngredient (
                RecipeId, IngredientId, IngredientName, Quantity, Unit, OrderIndex, PreparationNote, IsOptional, CreatedAt
            )
            VALUES (@RecipeId, @IngredientId, @IngredientName, @Quantity, @Unit, @OrderIndex, @PreparationNote, @IsOptional, GETUTCDATE())";

        // Use highest order index + 1
        var orderIndex = 1;
        var existing = await ExecuteScalarAsync<int?>("SELECT MAX(OrderIndex) FROM RecipeIngredient WHERE RecipeId = @RecipeId", new SqlParameter("@RecipeId", recipeId));
        if (existing.HasValue) orderIndex = existing.Value + 1;

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@IngredientId", (object?)productId ?? DBNull.Value),
            new SqlParameter("@IngredientName", name),
            new SqlParameter("@Quantity", quantity),
            new SqlParameter("@Unit", (object?)unit ?? DBNull.Value),
            new SqlParameter("@OrderIndex", orderIndex),
            new SqlParameter("@PreparationNote", (object?)notes ?? DBNull.Value),
            new SqlParameter("@IsOptional", isOptional));
    }

    public async Task AddInstructionAsync(Guid recipeId, int stepNumber, string instruction, int? timeMinutes)
    {
        const string sql = @"
            INSERT INTO RecipeInstruction (RecipeId, StepNumber, Instruction, TimeMinutes, CreatedAt)
            VALUES (@RecipeId, @StepNumber, @Instruction, @TimeMinutes, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@StepNumber", stepNumber),
            new SqlParameter("@Instruction", instruction),
            new SqlParameter("@TimeMinutes", (object?)timeMinutes ?? DBNull.Value));
    }

    public async Task UpdateNutritionAsync(Guid recipeId, int? calories, decimal? protein, decimal? carbs, decimal? fat, decimal? fiber, decimal? sugar)
    {
        // Upsert nutrition row
        const string sql = @"
            IF EXISTS (SELECT 1 FROM RecipeNutrition WHERE RecipeId = @RecipeId)
            BEGIN
                UPDATE RecipeNutrition SET
                    Calories = @Calories,
                    Protein = @Protein,
                    TotalCarbohydrates = @Carbs,
                    TotalFat = @Fat,
                    DietaryFiber = @Fiber,
                    Sugars = @Sugar,
                    UpdatedAt = GETUTCDATE()
                WHERE RecipeId = @RecipeId
            END
            ELSE
            BEGIN
                INSERT INTO RecipeNutrition (RecipeId, Calories, Protein, TotalCarbohydrates, TotalFat, DietaryFiber, Sugars, CreatedAt)
                VALUES (@RecipeId, @Calories, @Protein, @Carbs, @Fat, @Fiber, @Sugar, GETUTCDATE())
            END";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@Calories", (object?)calories ?? DBNull.Value),
            new SqlParameter("@Protein", (object?)protein ?? DBNull.Value),
            new SqlParameter("@Carbs", (object?)carbs ?? DBNull.Value),
            new SqlParameter("@Fat", (object?)fat ?? DBNull.Value),
            new SqlParameter("@Fiber", (object?)fiber ?? DBNull.Value),
            new SqlParameter("@Sugar", (object?)sugar ?? DBNull.Value));
    }

    public async Task AddRecipeIngredientsAsync(Guid recipeId, List<ExpressRecipe.Shared.DTOs.Recipe.RecipeIngredientDto> ingredients, Guid? createdBy = null)
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

    public async Task AddRecipeNutritionAsync(Guid recipeId, ExpressRecipe.Shared.DTOs.Recipe.RecipeNutritionDto nutrition)
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

    public async Task AddRecipeAllergensAsync(Guid recipeId, List<ExpressRecipe.Shared.DTOs.Recipe.RecipeAllergenWarningDto> allergens)
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

    public async Task<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto?> FindDuplicateRecipeAsync(string name, Guid authorId)
    {
        const string sql = @"
            SELECT TOP 1 Id, Name, AuthorId, SourceUrl
            FROM Recipe
            WHERE Name = @Name
              AND AuthorId = @AuthorId
              AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        var recipes = await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
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

    public async Task<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto?> GetRecipeByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes,
                   IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE Id = @Id AND IsDeleted = 0";

        var recipes = await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
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

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeIngredientDto>> GetRecipeIngredientsAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT Id, RecipeId, IngredientId, BaseIngredientId, IngredientName,
                   Quantity, Unit, OrderIndex, PreparationNote, IsOptional, SubstituteNotes
            FROM RecipeIngredient
            WHERE RecipeId = @RecipeId AND IsDeleted = 0
            ORDER BY OrderIndex";

        return await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeIngredientDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
            IngredientId = reader.IsDBNull(reader.GetOrdinal("IngredientId")) ? null : reader.GetGuid(reader.GetOrdinal("IngredientId")),
            BaseIngredientId = reader.IsDBNull(reader.GetOrdinal("BaseIngredientId")) ? null : reader.GetGuid(reader.GetOrdinal("BaseIngredientId")),
            IngredientName = reader.IsDBNull(reader.GetOrdinal("IngredientName")) ? null : reader.GetString(reader.GetOrdinal("IngredientName")),
            Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? null : reader.GetDecimal(reader.GetOrdinal("Quantity")),
            Unit = reader.IsDBNull(reader.GetOrdinal("Unit")) ? null : reader.GetString(reader.GetOrdinal("Unit")),
            OrderIndex = reader.GetInt32(reader.GetOrdinal("OrderIndex")),
            PreparationNote = reader.IsDBNull(reader.GetOrdinal("PreparationNote")) ? null : reader.GetString(reader.GetOrdinal("PreparationNote")),
            IsOptional = reader.GetBoolean(reader.GetOrdinal("IsOptional")),
            SubstituteNotes = reader.IsDBNull(reader.GetOrdinal("SubstituteNotes")) ? null : reader.GetString(reader.GetOrdinal("SubstituteNotes"))
        },
        new SqlParameter("@RecipeId", recipeId));
    }

    public async Task<ExpressRecipe.Shared.DTOs.Recipe.RecipeNutritionDto?> GetRecipeNutritionAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT Id, RecipeId, ServingSize, Calories, TotalFat, SaturatedFat, TransFat,
                   Cholesterol, Sodium, TotalCarbohydrates, DietaryFiber, Sugars, Protein,
                   VitaminD, Calcium, Iron, Potassium
            FROM RecipeNutrition
            WHERE RecipeId = @RecipeId";

        var results = await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeNutritionDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
            ServingSize = reader.IsDBNull(reader.GetOrdinal("ServingSize")) ? null : reader.GetString(reader.GetOrdinal("ServingSize")),
            Calories = reader.IsDBNull(reader.GetOrdinal("Calories")) ? null : reader.GetDecimal(reader.GetOrdinal("Calories")),
            TotalFat = reader.IsDBNull(reader.GetOrdinal("TotalFat")) ? null : reader.GetDecimal(reader.GetOrdinal("TotalFat")),
            SaturatedFat = reader.IsDBNull(reader.GetOrdinal("SaturatedFat")) ? null : reader.GetDecimal(reader.GetOrdinal("SaturatedFat")),
            TransFat = reader.IsDBNull(reader.GetOrdinal("TransFat")) ? null : reader.GetDecimal(reader.GetOrdinal("TransFat")),
            Cholesterol = reader.IsDBNull(reader.GetOrdinal("Cholesterol")) ? null : reader.GetDecimal(reader.GetOrdinal("Cholesterol")),
            Sodium = reader.IsDBNull(reader.GetOrdinal("Sodium")) ? null : reader.GetDecimal(reader.GetOrdinal("Sodium")),
            TotalCarbohydrates = reader.IsDBNull(reader.GetOrdinal("TotalCarbohydrates")) ? null : reader.GetDecimal(reader.GetOrdinal("TotalCarbohydrates")),
            DietaryFiber = reader.IsDBNull(reader.GetOrdinal("DietaryFiber")) ? null : reader.GetDecimal(reader.GetOrdinal("DietaryFiber")),
            Sugars = reader.IsDBNull(reader.GetOrdinal("Sugars")) ? null : reader.GetDecimal(reader.GetOrdinal("Sugars")),
            Protein = reader.IsDBNull(reader.GetOrdinal("Protein")) ? null : reader.GetDecimal(reader.GetOrdinal("Protein")),
            VitaminD = reader.IsDBNull(reader.GetOrdinal("VitaminD")) ? null : reader.GetDecimal(reader.GetOrdinal("VitaminD")),
            Calcium = reader.IsDBNull(reader.GetOrdinal("Calcium")) ? null : reader.GetDecimal(reader.GetOrdinal("Calcium")),
            Iron = reader.IsDBNull(reader.GetOrdinal("Iron")) ? null : reader.GetDecimal(reader.GetOrdinal("Iron")),
            Potassium = reader.IsDBNull(reader.GetOrdinal("Potassium")) ? null : reader.GetDecimal(reader.GetOrdinal("Potassium"))
        },
        new SqlParameter("@RecipeId", recipeId));

        return results.FirstOrDefault();
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeAllergenWarningDto>> GetRecipeAllergensAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT Id, RecipeId, AllergenId, AllergenName, SourceIngredientId
            FROM RecipeAllergenWarning
            WHERE RecipeId = @RecipeId";

        return await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeAllergenWarningDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
            AllergenId = reader.GetGuid(reader.GetOrdinal("AllergenId")),
            AllergenName = reader.GetString(reader.GetOrdinal("AllergenName")),
            SourceIngredientId = reader.IsDBNull(reader.GetOrdinal("SourceIngredientId")) ? null : reader.GetGuid(reader.GetOrdinal("SourceIngredientId"))
        },
        new SqlParameter("@RecipeId", recipeId));
    }

    public async Task UpdateRecipeAsync(Guid id, ExpressRecipe.Shared.DTOs.Recipe.UpdateRecipeRequest request, Guid userId)
    {
        const string sql = @"
            UPDATE Recipe
            SET Name = @Name,
                Description = @Description,
                Category = @Category,
                Cuisine = @Cuisine,
                DifficultyLevel = @DifficultyLevel,
                PrepTimeMinutes = @PrepTimeMinutes,
                CookTimeMinutes = @CookTimeMinutes,
                TotalTimeMinutes = @TotalTimeMinutes,
                Servings = @Servings,
                ImageUrl = @ImageUrl,
                VideoUrl = @VideoUrl,
                Instructions = @Instructions,
                Notes = @Notes,
                IsPublic = @IsPublic,
                SourceUrl = @SourceUrl,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@Category", (object?)request.Category ?? DBNull.Value),
            new SqlParameter("@Cuisine", (object?)request.Cuisine ?? DBNull.Value),
            new SqlParameter("@DifficultyLevel", (object?)request.DifficultyLevel ?? DBNull.Value),
            new SqlParameter("@PrepTimeMinutes", (object?)request.PrepTimeMinutes ?? DBNull.Value),
            new SqlParameter("@CookTimeMinutes", (object?)request.CookTimeMinutes ?? DBNull.Value),
            new SqlParameter("@TotalTimeMinutes", (object?)request.TotalTimeMinutes ?? DBNull.Value),
            new SqlParameter("@Servings", (object?)request.Servings ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)request.ImageUrl ?? DBNull.Value),
            new SqlParameter("@VideoUrl", (object?)request.VideoUrl ?? DBNull.Value),
            new SqlParameter("@Instructions", (object?)request.Instructions ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value),
            new SqlParameter("@IsPublic", request.IsPublic),
            new SqlParameter("@SourceUrl", (object?)request.SourceUrl ?? DBNull.Value),
            new SqlParameter("@UpdatedBy", userId)
        );
    }

    public async Task DeleteRecipeAsync(Guid id)
    {
        const string sql = @"
            UPDATE Recipe
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql, new SqlParameter("@Id", id));
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> GetUserRecipesAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes, IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE IsDeleted = 0 AND AuthorId = @UserId
            ORDER BY CreatedAt DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        return await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            Cuisine = reader.IsDBNull(reader.GetOrdinal("Cuisine")) ? null : reader.GetString(reader.GetOrdinal("Cuisine")),
            DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel")) ? null : reader.GetString(reader.GetOrdinal("DifficultyLevel")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? null : reader.GetInt32(reader.GetOrdinal("Servings")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("VideoUrl")) ? null : reader.GetString(reader.GetOrdinal("VideoUrl")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? null : reader.GetString(reader.GetOrdinal("Instructions")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@UserId", userId),
        new SqlParameter("@Limit", limit));
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> GetRecipesByCategoryAsync(string category, int limit = 50)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes, IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE IsDeleted = 0 AND Category = @Category
            ORDER BY CreatedAt DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        return await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            Cuisine = reader.IsDBNull(reader.GetOrdinal("Cuisine")) ? null : reader.GetString(reader.GetOrdinal("Cuisine")),
            DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel")) ? null : reader.GetString(reader.GetOrdinal("DifficultyLevel")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? null : reader.GetInt32(reader.GetOrdinal("Servings")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("VideoUrl")) ? null : reader.GetString(reader.GetOrdinal("VideoUrl")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? null : reader.GetString(reader.GetOrdinal("Instructions")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@Category", category),
        new SqlParameter("@Limit", limit));
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> GetRecipesByCuisineAsync(string cuisine, int limit = 50)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes, IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE IsDeleted = 0 AND Cuisine = @Cuisine
            ORDER BY CreatedAt DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        return await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            Cuisine = reader.IsDBNull(reader.GetOrdinal("Cuisine")) ? null : reader.GetString(reader.GetOrdinal("Cuisine")),
            DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel")) ? null : reader.GetString(reader.GetOrdinal("DifficultyLevel")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? null : reader.GetInt32(reader.GetOrdinal("Servings")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("VideoUrl")) ? null : reader.GetString(reader.GetOrdinal("VideoUrl")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? null : reader.GetString(reader.GetOrdinal("Instructions")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@Cuisine", cuisine),
        new SqlParameter("@Limit", limit));
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> GetRecipesByTagAsync(string tag, int limit = 50)
    {
        const string sql = @"
            SELECT DISTINCT r.Id, r.Name, r.Description, r.Category, r.Cuisine, r.DifficultyLevel,
                   r.PrepTimeMinutes, r.CookTimeMinutes, r.TotalTimeMinutes, r.Servings,
                   r.ImageUrl, r.VideoUrl, r.Instructions, r.Notes, r.IsPublic, r.IsApproved, r.SourceUrl, r.AuthorId, r.CreatedAt, r.UpdatedAt
            FROM Recipe r
            INNER JOIN RecipeTagMapping rtm ON r.Id = rtm.RecipeId
            INNER JOIN RecipeTag rt ON rtm.TagId = rt.Id
            WHERE r.IsDeleted = 0 AND rt.Name = @Tag
            ORDER BY r.CreatedAt DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        return await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            Cuisine = reader.IsDBNull(reader.GetOrdinal("Cuisine")) ? null : reader.GetString(reader.GetOrdinal("Cuisine")),
            DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel")) ? null : reader.GetString(reader.GetOrdinal("DifficultyLevel")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? null : reader.GetInt32(reader.GetOrdinal("Servings")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("VideoUrl")) ? null : reader.GetString(reader.GetOrdinal("VideoUrl")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? null : reader.GetString(reader.GetOrdinal("Instructions")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@Tag", tag),
        new SqlParameter("@Limit", limit));
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> GetRecipesByIngredientAsync(string ingredient, int limit = 50)
    {
        const string sql = @"
            SELECT DISTINCT r.Id, r.Name, r.Description, r.Category, r.Cuisine, r.DifficultyLevel,
                   r.PrepTimeMinutes, r.CookTimeMinutes, r.TotalTimeMinutes, r.Servings,
                   r.ImageUrl, r.VideoUrl, r.Instructions, r.Notes, r.IsPublic, r.IsApproved, r.SourceUrl, r.AuthorId, r.CreatedAt, r.UpdatedAt
            FROM Recipe r
            INNER JOIN RecipeIngredient ri ON r.Id = ri.RecipeId
            WHERE r.IsDeleted = 0 AND ri.IsDeleted = 0 AND ri.IngredientName LIKE @Ingredient
            ORDER BY r.CreatedAt DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        return await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            Cuisine = reader.IsDBNull(reader.GetOrdinal("Cuisine")) ? null : reader.GetString(reader.GetOrdinal("Cuisine")),
            DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("DifficultyLevel")) ? null : reader.GetString(reader.GetOrdinal("DifficultyLevel")),
            PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("PrepTimeMinutes")),
            CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookTimeMinutes")),
            TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("TotalTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? null : reader.GetInt32(reader.GetOrdinal("Servings")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            VideoUrl = reader.IsDBNull(reader.GetOrdinal("VideoUrl")) ? null : reader.GetString(reader.GetOrdinal("VideoUrl")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? null : reader.GetString(reader.GetOrdinal("Instructions")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
            AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        },
        new SqlParameter("@Ingredient", $"%{ingredient}%"),
        new SqlParameter("@Limit", limit));
    }

    public async Task<List<string>> GetAllCategoriesAsync()
    {
        const string sql = @"
            SELECT DISTINCT Category
            FROM Recipe
            WHERE IsDeleted = 0 AND Category IS NOT NULL
            ORDER BY Category";

        return await ExecuteReaderAsync(sql, reader => reader.GetString(0));
    }

    public async Task<List<string>> GetAllCuisinesAsync()
    {
        const string sql = @"
            SELECT DISTINCT Cuisine
            FROM Recipe
            WHERE IsDeleted = 0 AND Cuisine IS NOT NULL
            ORDER BY Cuisine";

        return await ExecuteReaderAsync(sql, reader => reader.GetString(0));
    }
}
