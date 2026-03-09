using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Recipe;
using ExpressRecipe.Client.Shared.Services;
using Shared = ExpressRecipe.Shared.DTOs.Recipe;
using Microsoft.Data.SqlClient;
using CQ = ExpressRecipe.RecipeService.CQRS.Queries;
using System.Data;
using System.Text;
using System.Text.Json;

namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// Repository for recipe data access using ADO.NET
/// </summary>
public class RecipeRepository : SqlHelper, IRecipeRepository
{
    private readonly IIngredientServiceClient? _ingredientClient;

    public RecipeRepository(string connectionString, IIngredientServiceClient? ingredientClient = null) : base(connectionString)
    {
        _ingredientClient = ingredientClient;
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
            Difficulty = difficulty,
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
        }, new SqlParameter("@Term", term), new SqlParameter("@Limit", limit), new SqlParameter("@Offset", offset));
    }

    public async Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> GetAllRecipesAsync(int limit = 50, int offset = 0)
    {
        var sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes, IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE IsDeleted = 0
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

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
        }, new SqlParameter("@Limit", limit), new SqlParameter("@Offset", offset));
    }

    public async Task<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto?> GetRecipeByIdAsync(Guid id)
    {
        var sql = @"
            SELECT Id, Name, Description, Category, Cuisine, DifficultyLevel,
                   PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings,
                   ImageUrl, VideoUrl, Instructions, Notes, IsPublic, IsApproved, SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE Id = @Id AND IsDeleted = 0";

        var recipes = await ExecuteReaderAsync(sql, reader => new ExpressRecipe.Shared.DTOs.Recipe.RecipeDto
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
        }, new SqlParameter("@Id", id));

        return recipes.FirstOrDefault();
    }

    public async Task<Guid> CreateRecipeAsync(CreateRecipeRequest request, Guid createdBy)
    {
        const string sql = @"
            INSERT INTO Recipe (Id, Name, Description, Category, Cuisine, DifficultyLevel, 
                               PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings, 
                               ImageUrl, VideoUrl, Instructions, Notes, IsPublic, SourceUrl, AuthorId, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Id, @Name, @Description, @Category, @Cuisine, @DifficultyLevel, 
                    @PrepTimeMinutes, @CookTimeMinutes, @TotalTimeMinutes, @Servings, 
                    @ImageUrl, @VideoUrl, @Instructions, @Notes, @IsPublic, @SourceUrl, @AuthorId, GETUTCDATE())";

        var id = await ExecuteScalarAsync<Guid>(sql,
            new SqlParameter("@Id", Guid.NewGuid()),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@Category", (object?)request.Category ?? DBNull.Value),
            new SqlParameter("@Cuisine", (object?)request.Cuisine ?? DBNull.Value),
            new SqlParameter("@DifficultyLevel", (object?)request.Difficulty ?? DBNull.Value),
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
            new SqlParameter("@AuthorId", createdBy));

        return id;
    }

    public async Task AddRecipeIngredientsAsync(Guid recipeId, List<RecipeIngredientDto> ingredients, Guid? createdBy = null)
    {
        if (!ingredients.Any()) return;

        // Ensure ingredients are registered in the microservice
        if (_ingredientClient != null)
        {
            var ingredientNames = ingredients.Where(i => i.IngredientId == null && !string.IsNullOrWhiteSpace(i.IngredientName))
                                            .Select(i => i.IngredientName!)
                                            .Distinct()
                                            .ToList();

            if (ingredientNames.Any())
            {
                // Bulk lookup
                var ingredientMap = await _ingredientClient.LookupIngredientIdsAsync(ingredientNames);
                
                // Identify completely missing ingredients
                var missingNames = ingredientNames.Where(name => !ingredientMap.ContainsKey(name)).ToList();
                if (missingNames.Any())
                {
                    await _ingredientClient.BulkCreateIngredientsAsync(missingNames);
                    // Re-lookup to get the new IDs
                    var updatedMap = await _ingredientClient.LookupIngredientIdsAsync(missingNames);
                    foreach (var kvp in updatedMap) ingredientMap[kvp.Key] = kvp.Value;
                }

                // Update the DTOs with the correct IngredientId
                foreach (var ing in ingredients.Where(i => i.IngredientId == null && !string.IsNullOrWhiteSpace(i.IngredientName)))
                {
                    if (ingredientMap.TryGetValue(ing.IngredientName!, out var id))
                    {
                        ing.IngredientId = id;
                    }
                }
            }
        }

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(Guid));
            dt.Columns.Add("RecipeId", typeof(Guid));
            dt.Columns.Add("IngredientId", typeof(Guid));
            dt.Columns.Add("IngredientName", typeof(string));
            dt.Columns.Add("Quantity", typeof(decimal));
            dt.Columns.Add("Unit", typeof(string));
            dt.Columns.Add("OrderIndex", typeof(int));
            dt.Columns.Add("PreparationNote", typeof(string));
            dt.Columns.Add("IsOptional", typeof(bool));
            dt.Columns.Add("SubstituteNotes", typeof(string));
            dt.Columns.Add("CreatedBy", typeof(Guid));
            dt.Columns.Add("CreatedAt", typeof(DateTime));

            foreach (var ing in ingredients)
            {
                dt.Rows.Add(
                    Guid.NewGuid(),
                    recipeId,
                    (object?)ing.IngredientId ?? DBNull.Value,
                    (object?)ing.IngredientName ?? DBNull.Value,
                    (object?)ing.Quantity ?? DBNull.Value,
                    (object?)ing.Unit ?? DBNull.Value,
                    ing.OrderIndex,
                    (object?)ing.PreparationNote ?? DBNull.Value,
                    ing.IsOptional,
                    (object?)ing.SubstituteNotes ?? DBNull.Value,
                    (object?)createdBy ?? DBNull.Value,
                    DateTime.UtcNow
                );
            }

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "RecipeIngredient";
                foreach (DataColumn col in dt.Columns) bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                await bulkCopy.WriteToServerAsync(dt);
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
            INSERT INTO RecipeNutrition (Id, RecipeId, ServingSize, Calories, TotalFat, SaturatedFat, TransFat, 
                                        Cholesterol, Sodium, TotalCarbohydrates, DietaryFiber, Sugars, Protein, 
                                        VitaminD, Calcium, Iron, Potassium, CreatedAt)
            VALUES (@Id, @RecipeId, @ServingSize, @Calories, @TotalFat, @SaturatedFat, @TransFat, 
                    @Cholesterol, @Sodium, @TotalCarbohydrates, @DietaryFiber, @Sugars, @Protein, 
                    @VitaminD, @Calcium, @Iron, @Potassium, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", Guid.NewGuid()),
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
            new SqlParameter("@Potassium", (object?)nutrition.Potassium ?? DBNull.Value));
    }

    public async Task AddRecipeAllergensAsync(Guid recipeId, List<RecipeAllergenWarningDto> allergens)
    {
        if (!allergens.Any()) return;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(Guid));
            dt.Columns.Add("RecipeId", typeof(Guid));
            dt.Columns.Add("AllergenId", typeof(Guid));
            dt.Columns.Add("AllergenName", typeof(string));
            dt.Columns.Add("SourceIngredientId", typeof(Guid));

            foreach (var allergen in allergens)
            {
                dt.Rows.Add(
                    Guid.NewGuid(),
                    recipeId,
                    allergen.AllergenId,
                    allergen.AllergenName,
                    (object?)allergen.SourceIngredientId ?? DBNull.Value
                );
            }

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "RecipeAllergenWarning";
                foreach (DataColumn col in dt.Columns) bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                await bulkCopy.WriteToServerAsync(dt);
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
                const string getTagSql = "SELECT Id FROM RecipeTag WHERE Name = @Name";
                Guid tagId;
                using (var command = new SqlCommand(getTagSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@Name", tagName);
                    var result = await command.ExecuteScalarAsync();
                    if (result == null)
                    {
                        const string createTagSql = "INSERT INTO RecipeTag (Name) OUTPUT INSERTED.Id VALUES (@Name)";
                        using var createCommand = new SqlCommand(createTagSql, connection, transaction);
                        createCommand.Parameters.AddWithValue("@Name", tagName);
                        tagId = (Guid)(await createCommand.ExecuteScalarAsync())!;
                    }
                    else tagId = (Guid)result;
                }

                const string mappingSql = @"
                    IF NOT EXISTS (SELECT 1 FROM RecipeTagMapping WHERE RecipeId = @RecipeId AND TagId = @TagId)
                    INSERT INTO RecipeTagMapping (RecipeId, TagId, CreatedAt) VALUES (@RecipeId, @TagId, GETUTCDATE())";

                using (var command = new SqlCommand(mappingSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@RecipeId", recipeId);
                    command.Parameters.AddWithValue("@TagId", tagId);
                    await command.ExecuteNonQueryAsync();
                }
            }
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task AddRecipeCategoryAsync(Guid recipeId, string categoryName) { await Task.CompletedTask; }
    public async Task AddRecipeTagAsync(Guid recipeId, string tagName) { await Task.CompletedTask; }
    public async Task AddIngredientAsync(Guid recipeId, Guid? productId, string name, decimal quantity, string unit, string? notes, bool isOptional) { await Task.CompletedTask; }
    public async Task AddInstructionAsync(Guid recipeId, int stepNumber, string instruction, int? timeMinutes) { await Task.CompletedTask; }
    public async Task UpdateNutritionAsync(Guid recipeId, int? calories, decimal? protein, decimal? carbs, decimal? fat, decimal? fiber, decimal? sugar) { await Task.CompletedTask; }
    public async Task<RecipeDto?> FindDuplicateRecipeAsync(string name, Guid authorId) { return null; }
    public async Task<List<RecipeDto>> GetUserRecipesAsync(Guid userId, int limit = 50) { return new List<RecipeDto>(); }
    public async Task<List<RecipeDto>> GetRecipesByCategoryAsync(string category, int limit = 50) { return new List<RecipeDto>(); }
    public async Task<List<RecipeDto>> GetRecipesByCuisineAsync(string cuisine, int limit = 50) { return new List<RecipeDto>(); }
    public async Task<List<RecipeDto>> GetRecipesByTagAsync(string tag, int limit = 50) { return new List<RecipeDto>(); }
    public async Task<List<RecipeDto>> GetRecipesByIngredientAsync(string ingredient, int limit = 50) { return new List<RecipeDto>(); }
    public async Task<List<string>> GetRecipeCategoriesAsync(Guid recipeId) { return new List<string>(); }
    public async Task<List<string>> GetRecipeTagsAsync(Guid recipeId) { return new List<string>(); }
    public async Task<List<RecipeIngredientDto>> GetIngredientsAsync(Guid recipeId) { return new List<RecipeIngredientDto>(); }
    public async Task<List<CQ.RecipeInstructionDto>> GetInstructionsAsync(Guid recipeId) { return new List<CQ.RecipeInstructionDto>(); }
    public async Task<CQ.RecipeNutritionDto?> GetNutritionAsync(Guid recipeId) { return null; }
    public async Task<CQ.RecipeDetailsDto?> GetRecipeDetailsAsync(Guid recipeId) { return null; }
    public async Task IncrementViewCountAsync(Guid recipeId) { await Task.CompletedTask; }
    public async Task UpdateRecipeInstructionsAsync(Guid recipeId, string instructions) { await Task.CompletedTask; }
    public async Task ClearRecipeIngredientsAsync(Guid recipeId) { await Task.CompletedTask; }
    public async Task ClearRecipeInstructionsAsync(Guid recipeId) { await Task.CompletedTask; }
    public async Task ClearRecipeTagsAsync(Guid recipeId) { await Task.CompletedTask; }
    public async Task UpdateRecipeAsync(Guid id, UpdateRecipeRequest request, Guid userId) { await Task.CompletedTask; }
    public async Task DeleteRecipeAsync(Guid id) { await Task.CompletedTask; }
    public async Task<(decimal AverageRating, int RatingCount)> GetAverageRatingAsync(Guid recipeId) { return (0, 0); }
    public async Task<List<RecipeIngredientDto>> GetRecipeIngredientsAsync(Guid recipeId) { return new List<RecipeIngredientDto>(); }
    public async Task<RecipeNutritionDto?> GetRecipeNutritionAsync(Guid recipeId) { return null; }
    public async Task<List<RecipeAllergenWarningDto>> GetRecipeAllergensAsync(Guid recipeId) { return new List<RecipeAllergenWarningDto>(); }

    public async Task<HashSet<string>> GetAllRecipeTitlesAsync()
    {
        const string sql = "SELECT Name FROM Recipe WITH (NOLOCK) WHERE IsDeleted = 0";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await ExecuteReaderAsync<bool>(sql, reader => {
            result.Add(reader.GetString(0));
            return true;
        });
        return result;
    }

    public async Task AddRecipeImagesAsync(Guid recipeId, List<RecipeImageDto> images)
    {
        if (!images.Any()) return;
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var img in images)
            {
                const string sql = @"
                    INSERT INTO RecipeImage (Id, RecipeId, ImageUrl, LocalPath, IsPrimary, DisplayOrder, SourceSystem, CreatedAt)
                    VALUES (@Id, @RecipeId, @ImageUrl, @LocalPath, @IsPrimary, @DisplayOrder, @SourceSystem, GETUTCDATE())";
                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@Id", BulkOperationsHelper.CreateSequentialGuid());
                cmd.Parameters.AddWithValue("@RecipeId", recipeId);
                cmd.Parameters.AddWithValue("@ImageUrl", img.ImageUrl);
                cmd.Parameters.AddWithValue("@LocalPath", (object?)img.LocalPath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IsPrimary", img.IsPrimary);
                cmd.Parameters.AddWithValue("@DisplayOrder", img.DisplayOrder);
                cmd.Parameters.AddWithValue("@SourceSystem", (object?)img.SourceSystem ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task<int> BulkCreateFullRecipesAsync(List<FullRecipeImportDto> recipes)
    {
        return await BulkCreateFullRecipesHighSpeedAsync(recipes);
    }

    public async Task<int> BulkCreateFullRecipesHighSpeedAsync(List<FullRecipeImportDto> importBatch)
    {
        if (!importBatch.Any()) return 0;

        // 1. Pre-register all ingredients in the microservice for ultra-high speed lookups
        if (_ingredientClient != null)
        {
            var allIngredientNames = importBatch
                .Where(r => r.Ingredients != null)
                .SelectMany(r => r.Ingredients!)
                .Where(i => i.IngredientId == null && !string.IsNullOrWhiteSpace(i.IngredientName))
                .Select(i => i.IngredientName!)
                .Distinct()
                .ToList();

            if (allIngredientNames.Any())
            {
                // Bulk lookup from microservice (high speed gRPC/REST + local HybridCache)
                var ingredientMap = await _ingredientClient.LookupIngredientIdsAsync(allIngredientNames);
                
                // Identify completely missing ingredients
                var missingNames = allIngredientNames.Where(name => !ingredientMap.ContainsKey(name)).ToList();
                if (missingNames.Any())
                {
                    await _ingredientClient.BulkCreateIngredientsAsync(missingNames);
                    // Re-lookup to get the new IDs
                    var updatedMap = await _ingredientClient.LookupIngredientIdsAsync(missingNames);
                    foreach (var kvp in updatedMap) ingredientMap[kvp.Key] = kvp.Value;
                }

                // Update the DTOs in the batch with the correct IngredientId
                foreach (var r in importBatch.Where(r => r.Ingredients != null))
                {
                    foreach (var ing in r.Ingredients!.Where(i => i.IngredientId == null && !string.IsNullOrWhiteSpace(i.IngredientName)))
                    {
                        if (ingredientMap.TryGetValue(ing.IngredientName!, out var id))
                        {
                            ing.IngredientId = id;
                        }
                    }
                }
            }
        }

        int retryCount = 0;
        const int maxRetries = 3;
        while (retryCount <= maxRetries)
        {
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var timestamp = DateTime.UtcNow;
                var allBatchTags = importBatch.Where(i => i.Tags != null).SelectMany(i => i.Tags!).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var tagMapping = await UpsertTagsInBulkAsync(allBatchTags);
                var uniqueBatch = importBatch.GroupBy(i => i.Recipe.Name.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
                                                var recipeDataTable = CreateRecipeDataTable();
                                                var recipeUpsertResult = await BulkOperationsHelper.BulkUpsertWithOutputAsync(
                                                    connection, transaction,
                                                    uniqueBatch, "Recipe", "#TempRecipes", new[] { "Name", "AuthorId" },
                                                    r => $"{r.Recipe.Name.Trim()}|{(r.Recipe.CreatedBy == Guid.Empty ? Guid.Empty : r.Recipe.CreatedBy)}",
                                                    (r, row) => {                                        row["Id"] = BulkOperationsHelper.CreateSequentialGuid();
                                        row["Name"] = r.Recipe.Name.Trim();
                                        row["Description"] = (object?)r.Recipe.Description ?? DBNull.Value;
                                        row["CookTimeMinutes"] = (object?)r.Recipe.CookTimeMinutes ?? DBNull.Value;
                                        row["Servings"] = (object?)r.Recipe.Servings ?? DBNull.Value;
                                        row["ImageUrl"] = (object?)r.Recipe.ImageUrl ?? DBNull.Value;
                                        row["SourceUrl"] = (object?)r.Recipe.SourceUrl ?? DBNull.Value;
                                        row["Notes"] = (object?)r.Recipe.Notes ?? DBNull.Value;
                                        row["IsPublic"] = r.Recipe.IsPublic;
                                        row["AuthorId"] = r.Recipe.CreatedBy == Guid.Empty ? Guid.Empty : r.Recipe.CreatedBy;
                                        row["CreatedBy"] = r.Recipe.CreatedBy;
                                        row["CreatedAt"] = timestamp;
                                        row["UpdatedAt"] = timestamp;
                                        row["IsDeleted"] = false;
                                        return row;
                                    },
                                    recipeDataTable);

                var ingredientData = new List<object[]>();
                var instructionData = new List<object[]>();
                var imageData = new List<object[]>();
                var tagMappingData = new List<object[]>();
                var updatedRecipeIds = new List<Guid>();

                foreach (var item in uniqueBatch)
                {
                    if (!recipeUpsertResult.TryGetValue(item.Recipe.Name.Trim(), out var result)) continue;
                    var recipeId = result.Id;
                    if (result.Action == "UPDATE") updatedRecipeIds.Add(recipeId);

                    if (item.Ingredients != null)
                    {
                        foreach (var ing in item.Ingredients)
                        {
                            ingredientData.Add(new object[] {
                                BulkOperationsHelper.CreateSequentialGuid(), recipeId, (object?)ing.IngredientId ?? DBNull.Value,
                                (object?)ing.IngredientName ?? DBNull.Value, (object?)ing.Quantity ?? DBNull.Value, (object?)ing.Unit ?? DBNull.Value, ing.OrderIndex,
                                (object?)ing.PreparationNote ?? DBNull.Value, ing.IsOptional, (object?)ing.SubstituteNotes ?? DBNull.Value, Guid.Empty, timestamp,
                                DBNull.Value, DBNull.Value, false, DBNull.Value, (object?)ing.GroupName ?? DBNull.Value, (object?)ing.OriginalText ?? DBNull.Value
                            });
                        }
                    }

                    if (item.Steps != null)
                    {
                        foreach (var step in item.Steps)
                        {
                            instructionData.Add(new object[] {
                                BulkOperationsHelper.CreateSequentialGuid(), recipeId, step.OrderIndex, step.Instruction, 
                                (object?)step.DurationMinutes ?? DBNull.Value, (object?)step.ImageUrl ?? DBNull.Value, (object?)step.Tips ?? DBNull.Value,
                                Guid.Empty, timestamp, DBNull.Value, DBNull.Value
                            });
                        }
                    }

                    if (item.Images != null)
                    {
                        foreach (var img in item.Images)
                        {
                            imageData.Add(new object[] {
                                BulkOperationsHelper.CreateSequentialGuid(), recipeId, img.ImageUrl, (object?)img.LocalPath ?? DBNull.Value,
                                img.IsPrimary, img.DisplayOrder, (object?)img.SourceSystem ?? DBNull.Value, timestamp
                            });
                        }
                    }

                    if (item.Tags != null)
                    {
                        foreach (var tagName in item.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            if (tagMapping.TryGetValue(tagName, out var tagId))
                                tagMappingData.Add(new object[] { BulkOperationsHelper.CreateSequentialGuid(), recipeId, tagId, timestamp });
                        }
                    }
                }

                if (updatedRecipeIds.Any()) await ClearChildDataForRecipesAsync(connection, transaction, updatedRecipeIds);
                await DisableNonEssentialIndexesAsync(connection, transaction);
                await CreateStagingTablesAsync(connection, transaction, 1);

                if (ingredientData.Any()) await BulkInsertToStagingTableAsync(connection, transaction, "#RecipeIngredient_W0", ingredientData, new[] { "Id", "RecipeId", "IngredientId", "IngredientName", "Quantity", "Unit", "OrderIndex", "PreparationNote", "IsOptional", "SubstituteNotes", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "IsDeleted", "DeletedAt", "GroupName", "OriginalText" });
                if (instructionData.Any()) await BulkInsertToStagingTableAsync(connection, transaction, "#RecipeInstruction_W0", instructionData, new[] { "Id", "RecipeId", "OrderIndex", "Instruction", "TimeMinutes", "ImageUrl", "Tips", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt" });
                if (imageData.Any()) await BulkInsertToStagingTableAsync(connection, transaction, "#RecipeImage_W0", imageData, new[] { "Id", "RecipeId", "ImageUrl", "LocalPath", "IsPrimary", "DisplayOrder", "SourceSystem", "CreatedAt" });
                if (tagMappingData.Any()) await BulkInsertToStagingTableAsync(connection, transaction, "#RecipeTagMapping_W0", tagMappingData, new[] { "Id", "RecipeId", "TagId", "CreatedAt" });

                await MergeFromStagingTablesAsync(connection, transaction, 1);
                await RebuildNonEssentialIndexesAsync(connection, transaction);
                await transaction.CommitAsync();
                return uniqueBatch.Count;
            }
            catch (SqlException ex) when ((ex.Number == 1205 || ex.Number == 4891) && retryCount < maxRetries)
            {
                if (transaction.Connection != null) try { await transaction.RollbackAsync(); } catch { }
                retryCount++; await Task.Delay(1000 * retryCount);
            }
            catch { if (transaction.Connection != null) try { await transaction.RollbackAsync(); } catch { } throw; }
        }
        return 0;
    }

    private DataTable CreateRecipeDataTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(Guid)); dt.Columns.Add("Name", typeof(string)); dt.Columns.Add("Description", typeof(string));
        dt.Columns.Add("CookTimeMinutes", typeof(int)); dt.Columns.Add("Servings", typeof(int)); dt.Columns.Add("ImageUrl", typeof(string));
        dt.Columns.Add("SourceUrl", typeof(string)); dt.Columns.Add("Notes", typeof(string)); dt.Columns.Add("IsPublic", typeof(bool));
        dt.Columns.Add("AuthorId", typeof(Guid));
        dt.Columns.Add("CreatedBy", typeof(Guid)); dt.Columns.Add("CreatedAt", typeof(DateTime)); dt.Columns.Add("UpdatedAt", typeof(DateTime));
        dt.Columns.Add("IsDeleted", typeof(bool)); return dt;
    }

    private async Task<Dictionary<string, Guid>> UpsertTagsInBulkAsync(IEnumerable<string> tags)
    {
        var tagList = tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!tagList.Any()) return new Dictionary<string, Guid>();
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        using (var cmd = new SqlCommand("CREATE TABLE #TempTags (Name NVARCHAR(100))", connection)) { cmd.CommandTimeout = 120; await cmd.ExecuteNonQueryAsync(); }
        var dt = new DataTable(); dt.Columns.Add("Name", typeof(string));
        foreach (var tag in tagList) dt.Rows.Add(tag);
        using (var bulkCopy = new SqlBulkCopy(connection)) { bulkCopy.DestinationTableName = "#TempTags"; await bulkCopy.WriteToServerAsync(dt); }
                const string sql = @"
                    MERGE RecipeTag AS target
                    USING (SELECT DISTINCT Name FROM #TempTags) AS source
                    ON (target.Name = source.Name)
                    WHEN NOT MATCHED THEN
                        INSERT (Id, Name)
                        VALUES (NEWID(), source.Name);
                    
                    SELECT Name, Id FROM RecipeTag WHERE Name IN (SELECT Name FROM #TempTags)";
        using (var cmd = new SqlCommand(sql, connection)) { cmd.CommandTimeout = 120; using var reader = await cmd.ExecuteReaderAsync(); while (await reader.ReadAsync()) result[reader.GetString(0)] = reader.GetGuid(1); }
        return result;
    }

    private async Task ClearChildDataForRecipesAsync(SqlConnection connection, SqlTransaction transaction, List<Guid> recipeIds)
    {
        if (!recipeIds.Any()) return;
        using (var cmd = new SqlCommand("CREATE TABLE #ClearIds (Id UNIQUEIDENTIFIER PRIMARY KEY)", connection, transaction)) await cmd.ExecuteNonQueryAsync();
        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)) { bulkCopy.DestinationTableName = "#ClearIds"; var dt = new DataTable(); dt.Columns.Add("Id", typeof(Guid)); foreach (var id in recipeIds) dt.Rows.Add(id); await bulkCopy.WriteToServerAsync(dt); }
        const string sql = "DELETE FROM RecipeIngredient WHERE RecipeId IN (SELECT Id FROM #ClearIds); DELETE FROM RecipeInstruction WHERE RecipeId IN (SELECT Id FROM #ClearIds); DELETE FROM RecipeImage WHERE RecipeId IN (SELECT Id FROM #ClearIds); DELETE FROM RecipeTagMapping WHERE RecipeId IN (SELECT Id FROM #ClearIds); DELETE FROM RecipeNutrition WHERE RecipeId IN (SELECT Id FROM #ClearIds); DROP TABLE #ClearIds;";
        using (var cmd = new SqlCommand(sql, connection, transaction)) { cmd.CommandTimeout = 600; await cmd.ExecuteNonQueryAsync(); }
    }

    private async Task DisableNonEssentialIndexesAsync(SqlConnection connection, SqlTransaction transaction) { await Task.CompletedTask; }

    private async Task RebuildNonEssentialIndexesAsync(SqlConnection connection, SqlTransaction transaction)
    {
        const string rebuildSql = "ALTER INDEX IX_RecipeIngredient_RecipeId ON RecipeIngredient REBUILD WITH (FILLFACTOR = 70); ALTER INDEX IX_RecipeInstruction_RecipeId ON RecipeInstruction REBUILD WITH (FILLFACTOR = 70); ALTER INDEX IX_RecipeImage_RecipeId ON RecipeImage REBUILD WITH (FILLFACTOR = 70); ALTER INDEX IX_RecipeTagMapping_TagId ON RecipeTagMapping REBUILD WITH (FILLFACTOR = 70);";
        using var cmd = new SqlCommand(rebuildSql, connection, transaction); cmd.CommandTimeout = 600;
        try { await cmd.ExecuteNonQueryAsync(); } catch { }
    }

    private async Task CreateStagingTablesAsync(SqlConnection connection, SqlTransaction transaction, int workerCount)
    {
        var createSql = new StringBuilder();
        for (int i = 0; i < workerCount; i++)
        {
            createSql.AppendLine($@"
                CREATE TABLE #RecipeIngredient_W{i} (Id UNIQUEIDENTIFIER, RecipeId UNIQUEIDENTIFIER, IngredientId UNIQUEIDENTIFIER, IngredientName NVARCHAR(200), Quantity DECIMAL(18,4), Unit NVARCHAR(50), OrderIndex INT, PreparationNote NVARCHAR(500), IsOptional BIT, SubstituteNotes NVARCHAR(500), CreatedBy UNIQUEIDENTIFIER, CreatedAt DATETIME2, UpdatedBy UNIQUEIDENTIFIER, UpdatedAt DATETIME2, IsDeleted BIT, DeletedAt DATETIME2, GroupName NVARCHAR(100), OriginalText NVARCHAR(MAX));
                CREATE TABLE #RecipeInstruction_W{i} (Id UNIQUEIDENTIFIER, RecipeId UNIQUEIDENTIFIER, OrderIndex INT, Instruction NVARCHAR(MAX), TimeMinutes INT, ImageUrl NVARCHAR(500), Tips NVARCHAR(MAX), CreatedBy UNIQUEIDENTIFIER, CreatedAt DATETIME2, UpdatedBy UNIQUEIDENTIFIER, UpdatedAt DATETIME2);
                CREATE TABLE #RecipeImage_W{i} (Id UNIQUEIDENTIFIER, RecipeId UNIQUEIDENTIFIER, ImageUrl NVARCHAR(500), LocalPath NVARCHAR(500), IsPrimary BIT, DisplayOrder INT, SourceSystem NVARCHAR(100), CreatedAt DATETIME2);
                CREATE TABLE #RecipeTagMapping_W{i} (Id UNIQUEIDENTIFIER, RecipeId UNIQUEIDENTIFIER, TagId UNIQUEIDENTIFIER, CreatedAt DATETIME2);");
        }
        using var cmd = new SqlCommand(createSql.ToString(), connection, transaction); cmd.CommandTimeout = 120; await cmd.ExecuteNonQueryAsync();
    }

    private async Task BulkInsertToStagingTableAsync(SqlConnection connection, SqlTransaction transaction, string stagingTableName, List<object[]> data, string[] columns)
    {
        if (!data.Any()) return;
        var dt = new DataTable(); foreach (var col in columns) dt.Columns.Add(col);
        foreach (var row in data) {
            var dtRow = dt.NewRow();
            for (int i = 0; i < columns.Length; i++) {
                var val = row[i];
                if (val is decimal d) {
                    if (d > 99999999999999.9999m) d = 99999999999999.9999m;
                    if (d < -99999999999999.9999m) d = -99999999999999.9999m;
                    dtRow[i] = d;
                } else dtRow[i] = val ?? DBNull.Value;
            }
            dt.Rows.Add(dtRow);
        }
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
        bulkCopy.DestinationTableName = stagingTableName; bulkCopy.BatchSize = 5000; bulkCopy.BulkCopyTimeout = 600;
        foreach (var col in columns) bulkCopy.ColumnMappings.Add(col, col);
        await bulkCopy.WriteToServerAsync(dt);
    }

    private async Task MergeFromStagingTablesAsync(SqlConnection connection, SqlTransaction transaction, int workerCount)
    {
        string ingredientCols = "Id, RecipeId, IngredientId, IngredientName, Quantity, Unit, OrderIndex, PreparationNote, IsOptional, SubstituteNotes, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted, DeletedAt, GroupName, OriginalText";
        var ingredientUnion = new StringBuilder($"INSERT INTO RecipeIngredient ({ingredientCols}) SELECT {ingredientCols} FROM #RecipeIngredient_W0");
        string instructionCols = "Id, RecipeId, OrderIndex, Instruction, TimeMinutes, ImageUrl, Tips, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt";
        var instructionUnion = new StringBuilder($"INSERT INTO RecipeInstruction ({instructionCols}) SELECT {instructionCols} FROM #RecipeInstruction_W0");
        string imageCols = "Id, RecipeId, ImageUrl, LocalPath, IsPrimary, DisplayOrder, SourceSystem, CreatedAt";
        var imageUnion = new StringBuilder($"INSERT INTO RecipeImage ({imageCols}) SELECT {imageCols} FROM #RecipeImage_W0");
        string tagMappingCols = "Id, RecipeId, TagId, CreatedAt";
        var tagMappingUnion = new StringBuilder($"INSERT INTO RecipeTagMapping ({tagMappingCols}) SELECT {tagMappingCols} FROM #RecipeTagMapping_W0");

        for (int i = 1; i < workerCount; i++) {
            ingredientUnion.AppendLine($" UNION ALL SELECT {ingredientCols} FROM #RecipeIngredient_W{i}");
            instructionUnion.AppendLine($" UNION ALL SELECT {instructionCols} FROM #RecipeInstruction_W{i}");
            imageUnion.AppendLine($" UNION ALL SELECT {imageCols} FROM #RecipeImage_W{i}");
            tagMappingUnion.AppendLine($" UNION ALL SELECT {tagMappingCols} FROM #RecipeTagMapping_W{i}");
        }
        using (var cmd = new SqlCommand(ingredientUnion.ToString(), connection, transaction)) { cmd.CommandTimeout = 600; await cmd.ExecuteNonQueryAsync(); }
        using (var cmd = new SqlCommand(instructionUnion.ToString(), connection, transaction)) { cmd.CommandTimeout = 600; await cmd.ExecuteNonQueryAsync(); }
        using (var cmd = new SqlCommand(imageUnion.ToString(), connection, transaction)) { cmd.CommandTimeout = 600; await cmd.ExecuteNonQueryAsync(); }
        using (var cmd = new SqlCommand(tagMappingUnion.ToString(), connection, transaction)) { cmd.CommandTimeout = 600; await cmd.ExecuteNonQueryAsync(); }
    }

    public async Task<Dictionary<string, bool>> GetAllRecipeTitlesCompletenessAsync()
    {
        const string sql = "SELECT Name, 1 as IsComplete FROM Recipe WITH (NOLOCK) WHERE IsDeleted = 0";
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        await ExecuteReaderAsync<bool>(sql, reader => {
            var name = reader.GetString(0);
            if (!result.ContainsKey(name)) result[name] = true;
            return true;
        });
        return result;
    }

    public async Task<List<string>> GetAllCategoriesAsync()
    {
        const string sql = "SELECT DISTINCT Category FROM Recipe WHERE IsDeleted = 0 AND Category IS NOT NULL ORDER BY Category";
        return await ExecuteReaderAsync(sql, reader => reader.GetString(0));
    }

    public async Task<List<string>> GetAllCuisinesAsync()
    {
        const string sql = "SELECT DISTINCT Cuisine FROM Recipe WHERE IsDeleted = 0 AND Cuisine IS NOT NULL ORDER BY Cuisine";
        return await ExecuteReaderAsync(sql, reader => reader.GetString(0));
    }

    public async Task<object?> GetByExactTitleAsync(string title)
    {
        const string sql = @"
            SELECT TOP 1
                Id, Name, Description, Category, Cuisine, DifficultyLevel, 
                PrepTimeMinutes, CookTimeMinutes, TotalTimeMinutes, Servings, 
                ImageUrl, VideoUrl, Instructions, Notes, IsPublic, IsApproved, 
                SourceUrl, AuthorId, CreatedAt, UpdatedAt
            FROM Recipe
            WHERE Name = @Title 
                AND IsDeleted = 0";

        var recipes = await ExecuteReaderAsync(sql, reader => new
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? null : reader.GetString(reader.GetOrdinal("Instructions"))
        }, new SqlParameter("@Title", title));

        return recipes.FirstOrDefault();
    }

    // -----------------------------------------------------------------------
    // Share Tokens
    // -----------------------------------------------------------------------

    public async Task<string> GenerateShareTokenAsync(Guid recipeId, Guid createdBy, int expiryDays, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO RecipeShareToken (RecipeId, Token, CreatedBy, ExpiresAt)
            VALUES (@RecipeId, @Token, @CreatedBy, @ExpiresAt)";

        const int MaxAttempts = 5;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            string token = Convert.ToBase64String(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .Replace("=", string.Empty, StringComparison.Ordinal)[..64];

            try
            {
                await ExecuteNonQueryAsync(sql, ct,
                    new SqlParameter("@RecipeId", recipeId),
                    new SqlParameter("@Token", token),
                    new SqlParameter("@CreatedBy", createdBy),
                    new SqlParameter("@ExpiresAt", DateTime.UtcNow.AddDays(expiryDays)));

                return token;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when ((ex.Number == 2627 || ex.Number == 2601)
                      && ex.Message.Contains("UQ_RecipeShareToken", StringComparison.OrdinalIgnoreCase))
            {
                if (attempt == MaxAttempts) { throw; }
                // Token collision (extremely rare) – generate a new one and retry
            }
        }

        // Unreachable, but satisfies the compiler
        throw new InvalidOperationException("Failed to generate a unique share token.");
    }

    public async Task<RecipeShareTokenDto?> GetByShareTokenAsync(string token, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                st.Id, st.RecipeId, st.Token, st.CreatedBy, st.CreatedAt,
                st.ExpiresAt, st.ViewCount, st.MaxViews,
                r.Id AS R_Id, r.Name AS R_Name, r.Description AS R_Description,
                r.Category AS R_Category, r.Cuisine AS R_Cuisine,
                r.DifficultyLevel AS R_DifficultyLevel,
                r.PrepTimeMinutes AS R_PrepTimeMinutes,
                r.CookTimeMinutes AS R_CookTimeMinutes,
                r.TotalTimeMinutes AS R_TotalTimeMinutes,
                r.Servings AS R_Servings,
                r.ImageUrl AS R_ImageUrl,
                r.AuthorId AS R_AuthorId,
                r.IsPublic AS R_IsPublic,
                r.IsApproved AS R_IsApproved,
                r.CreatedAt AS R_CreatedAt
            FROM RecipeShareToken st
            INNER JOIN Recipe r ON r.Id = st.RecipeId AND r.IsDeleted = 0
            WHERE st.Token = @Token
              AND st.ExpiresAt > GETUTCDATE()
              AND (st.MaxViews IS NULL OR st.ViewCount < st.MaxViews)";

        List<RecipeShareTokenDto> results = await ExecuteReaderAsync(sql, reader =>
        {
            int viewCount = reader.IsDBNull(reader.GetOrdinal("ViewCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("ViewCount"));
            int? maxViews = reader.IsDBNull(reader.GetOrdinal("MaxViews")) ? null : reader.GetInt32(reader.GetOrdinal("MaxViews"));

            RecipeDto recipe = new RecipeDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("R_Id")),
                Name = reader.GetString(reader.GetOrdinal("R_Name")),
                Description = reader.IsDBNull(reader.GetOrdinal("R_Description")) ? null : reader.GetString(reader.GetOrdinal("R_Description")),
                Category = reader.IsDBNull(reader.GetOrdinal("R_Category")) ? null : reader.GetString(reader.GetOrdinal("R_Category")),
                Cuisine = reader.IsDBNull(reader.GetOrdinal("R_Cuisine")) ? null : reader.GetString(reader.GetOrdinal("R_Cuisine")),
                DifficultyLevel = reader.IsDBNull(reader.GetOrdinal("R_DifficultyLevel")) ? null : reader.GetString(reader.GetOrdinal("R_DifficultyLevel")),
                PrepTimeMinutes = reader.IsDBNull(reader.GetOrdinal("R_PrepTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("R_PrepTimeMinutes")),
                CookTimeMinutes = reader.IsDBNull(reader.GetOrdinal("R_CookTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("R_CookTimeMinutes")),
                TotalTimeMinutes = reader.IsDBNull(reader.GetOrdinal("R_TotalTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("R_TotalTimeMinutes")),
                Servings = reader.IsDBNull(reader.GetOrdinal("R_Servings")) ? null : reader.GetInt32(reader.GetOrdinal("R_Servings")),
                ImageUrl = reader.IsDBNull(reader.GetOrdinal("R_ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("R_ImageUrl")),
                AuthorId = reader.GetGuid(reader.GetOrdinal("R_AuthorId")),
                IsPublic = reader.GetBoolean(reader.GetOrdinal("R_IsPublic")),
                IsApproved = reader.GetBoolean(reader.GetOrdinal("R_IsApproved")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("R_CreatedAt"))
            };

            return new RecipeShareTokenDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                RecipeId = reader.GetGuid(reader.GetOrdinal("RecipeId")),
                Token = reader.GetString(reader.GetOrdinal("Token")),
                CreatedBy = reader.GetGuid(reader.GetOrdinal("CreatedBy")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                ExpiresAt = reader.GetDateTime(reader.GetOrdinal("ExpiresAt")),
                ViewCount = viewCount,
                MaxViews = maxViews,
                Recipe = recipe
            };
        }, new SqlParameter("@Token", token));

        return results.FirstOrDefault();
    }

    public async Task IncrementTokenViewCountAsync(string token, CancellationToken ct = default)
    {
        // Atomic conditional UPDATE: only increment when the token is still valid
        // (not expired and under MaxViews). This prevents over-incrementing under
        // concurrent requests and after token expiry.
        const string sql = @"
            UPDATE RecipeShareToken
            SET ViewCount = ViewCount + 1
            WHERE Token = @Token
              AND ExpiresAt > GETUTCDATE()
              AND (MaxViews IS NULL OR ViewCount < MaxViews)";

        await ExecuteNonQueryAsync(sql, ct, new SqlParameter("@Token", token));
    }

    public async Task<bool> ExpireShareTokenAsync(string token, Guid requestedBy, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE RecipeShareToken
            SET ExpiresAt = GETUTCDATE()
            WHERE Token = @Token AND CreatedBy = @RequestedBy";

        int affected = await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@Token", token),
            new SqlParameter("@RequestedBy", requestedBy));

        return affected > 0;
    }

    // -----------------------------------------------------------------------
    // Household Favorites
    // -----------------------------------------------------------------------

    public async Task SetFavoriteHouseholdShareAsync(Guid favoriteId, Guid userId, bool shared, Guid? householdId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE UserFavoriteRecipe
            SET IsSharedWithHousehold = @Shared,
                HouseholdId = @HouseholdId
            WHERE Id = @FavoriteId AND UserId = @UserId";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@FavoriteId", favoriteId),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Shared", shared),
            new SqlParameter("@HouseholdId", (object?)householdId ?? DBNull.Value));
    }

    public async Task<List<RecipeDto>> GetHouseholdSharedFavoritesAsync(Guid householdId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT DISTINCT
                r.Id, r.Name, r.Description, r.Category, r.Cuisine,
                r.DifficultyLevel, r.PrepTimeMinutes, r.CookTimeMinutes,
                r.TotalTimeMinutes, r.Servings, r.ImageUrl, r.VideoUrl,
                r.Instructions, r.Notes, r.IsPublic, r.IsApproved,
                r.SourceUrl, r.AuthorId, r.CreatedAt, r.UpdatedAt
            FROM UserFavoriteRecipe uf
            INNER JOIN Recipe r ON r.Id = uf.RecipeId AND r.IsDeleted = 0
            WHERE uf.HouseholdId = @HouseholdId
              AND uf.IsSharedWithHousehold = 1
            ORDER BY r.Name";

        return await ExecuteReaderAsync(sql, reader =>
        {
            int descOrd = reader.GetOrdinal("Description");
            int catOrd = reader.GetOrdinal("Category");
            int cuisOrd = reader.GetOrdinal("Cuisine");
            int diffOrd = reader.GetOrdinal("DifficultyLevel");
            int prepOrd = reader.GetOrdinal("PrepTimeMinutes");
            int cookOrd = reader.GetOrdinal("CookTimeMinutes");
            int totalOrd = reader.GetOrdinal("TotalTimeMinutes");
            int servOrd = reader.GetOrdinal("Servings");
            int imgOrd = reader.GetOrdinal("ImageUrl");
            int vidOrd = reader.GetOrdinal("VideoUrl");
            int instrOrd = reader.GetOrdinal("Instructions");
            int notesOrd = reader.GetOrdinal("Notes");
            int srcOrd = reader.GetOrdinal("SourceUrl");
            int updOrd = reader.GetOrdinal("UpdatedAt");

            return new RecipeDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.IsDBNull(descOrd) ? null : reader.GetString(descOrd),
                Category = reader.IsDBNull(catOrd) ? null : reader.GetString(catOrd),
                Cuisine = reader.IsDBNull(cuisOrd) ? null : reader.GetString(cuisOrd),
                DifficultyLevel = reader.IsDBNull(diffOrd) ? null : reader.GetString(diffOrd),
                PrepTimeMinutes = reader.IsDBNull(prepOrd) ? null : reader.GetInt32(prepOrd),
                CookTimeMinutes = reader.IsDBNull(cookOrd) ? null : reader.GetInt32(cookOrd),
                TotalTimeMinutes = reader.IsDBNull(totalOrd) ? null : reader.GetInt32(totalOrd),
                Servings = reader.IsDBNull(servOrd) ? null : reader.GetInt32(servOrd),
                ImageUrl = reader.IsDBNull(imgOrd) ? null : reader.GetString(imgOrd),
                VideoUrl = reader.IsDBNull(vidOrd) ? null : reader.GetString(vidOrd),
                Instructions = reader.IsDBNull(instrOrd) ? null : reader.GetString(instrOrd),
                Notes = reader.IsDBNull(notesOrd) ? null : reader.GetString(notesOrd),
                IsPublic = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
                IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
                SourceUrl = reader.IsDBNull(srcOrd) ? null : reader.GetString(srcOrd),
                AuthorId = reader.GetGuid(reader.GetOrdinal("AuthorId")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.IsDBNull(updOrd) ? null : reader.GetDateTime(updOrd)
            };
        }, new SqlParameter("@HouseholdId", householdId));
    }
}
