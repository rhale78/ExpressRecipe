using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Data;

public interface IMenuItemRepository
{
    Task<List<MenuItemDto>> SearchAsync(MenuItemSearchRequest request);
    Task<MenuItemDto?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(CreateMenuItemRequest request, Guid createdBy);
    Task<bool> UpdateAsync(Guid id, UpdateMenuItemRequest request, Guid updatedBy);
    Task<bool> DeleteAsync(Guid id, Guid deletedBy);
    Task<bool> MenuItemExistsAsync(Guid id);

    // Menu Item Ingredients
    Task<List<MenuItemIngredientDto>> GetMenuItemIngredientsAsync(Guid menuItemId);
    Task<Guid> AddMenuItemIngredientAsync(Guid menuItemId, Guid ingredientId, int orderIndex, Guid createdBy);
    Task<bool> RemoveMenuItemIngredientAsync(Guid menuItemIngredientId, Guid deletedBy);

    // Menu Item Nutrition
    Task<MenuItemNutritionDto?> GetMenuItemNutritionAsync(Guid menuItemId);
    Task<Guid> AddOrUpdateMenuItemNutritionAsync(Guid menuItemId, MenuItemNutritionDto nutrition, Guid createdBy);

    // User Ratings
    Task<List<UserMenuItemRatingDto>> GetMenuItemRatingsAsync(Guid menuItemId);
    Task<UserMenuItemRatingDto?> GetUserRatingAsync(Guid menuItemId, Guid userId);
    Task<Guid> AddOrUpdateRatingAsync(Guid menuItemId, Guid userId, RateMenuItemRequest request);
    Task<bool> DeleteRatingAsync(Guid menuItemId, Guid userId);
}

public class MenuItemRepository : SqlHelper, IMenuItemRepository
{
    public MenuItemRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<MenuItemDto>> SearchAsync(MenuItemSearchRequest request)
    {
        var whereClauses = new List<string> { "mi.IsDeleted = 0" };
        var parameters = new List<SqlParameter>();

        if (request.RestaurantId.HasValue)
        {
            whereClauses.Add("mi.RestaurantId = @RestaurantId");
            parameters.Add(CreateParameter("@RestaurantId", request.RestaurantId.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            whereClauses.Add("(mi.Name LIKE @SearchTerm OR mi.Description LIKE @SearchTerm)");
            parameters.Add(CreateParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            whereClauses.Add("mi.Category = @Category");
            parameters.Add(CreateParameter("@Category", request.Category));
        }

        if (request.IsAvailable.HasValue)
        {
            whereClauses.Add("mi.IsAvailable = @IsAvailable");
            parameters.Add(CreateParameter("@IsAvailable", request.IsAvailable.Value));
        }

        var sql = $@"
            SELECT mi.Id, mi.RestaurantId, mi.Name, mi.Description, mi.Category,
                   mi.Price, mi.Currency, mi.ServingSize, mi.ServingUnit,
                   mi.IsAvailable, mi.CreatedAt, mi.UpdatedAt,
                   r.Name AS RestaurantName
            FROM MenuItem mi
            INNER JOIN Restaurant r ON mi.RestaurantId = r.Id
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY mi.Name
            OFFSET {(request.PageNumber - 1) * request.PageSize} ROWS
            FETCH NEXT {request.PageSize} ROWS ONLY";

        return await ExecuteReaderAsync(
            sql,
            reader => MapMenuItemDto(reader),
            parameters.ToArray());
    }

    public async Task<MenuItemDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT mi.Id, mi.RestaurantId, mi.Name, mi.Description, mi.Category,
                   mi.Price, mi.Currency, mi.ServingSize, mi.ServingUnit,
                   mi.IsAvailable, mi.CreatedAt, mi.UpdatedAt,
                   r.Name AS RestaurantName
            FROM MenuItem mi
            INNER JOIN Restaurant r ON mi.RestaurantId = r.Id
            WHERE mi.Id = @Id AND mi.IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => MapMenuItemDto(reader),
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(CreateMenuItemRequest request, Guid createdBy)
    {
        const string sql = @"
            INSERT INTO MenuItem (
                Id, RestaurantId, Name, Description, Category,
                Price, Currency, ServingSize, ServingUnit, IsAvailable,
                CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @RestaurantId, @Name, @Description, @Category,
                @Price, @Currency, @ServingSize, @ServingUnit, @IsAvailable,
                @CreatedBy, GETUTCDATE()
            )";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@RestaurantId", request.RestaurantId),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Category", request.Category),
            CreateParameter("@Price", request.Price),
            CreateParameter("@Currency", request.Currency ?? "USD"),
            CreateParameter("@ServingSize", request.ServingSize),
            CreateParameter("@ServingUnit", request.ServingUnit),
            CreateParameter("@IsAvailable", request.IsAvailable ?? true),
            CreateParameter("@CreatedBy", createdBy));

        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateMenuItemRequest request, Guid updatedBy)
    {
        const string sql = @"
            UPDATE MenuItem
            SET Name = @Name,
                Description = @Description,
                Category = @Category,
                Price = @Price,
                Currency = @Currency,
                ServingSize = @ServingSize,
                ServingUnit = @ServingUnit,
                IsAvailable = @IsAvailable,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Category", request.Category),
            CreateParameter("@Price", request.Price),
            CreateParameter("@Currency", request.Currency ?? "USD"),
            CreateParameter("@ServingSize", request.ServingSize),
            CreateParameter("@ServingUnit", request.ServingUnit),
            CreateParameter("@IsAvailable", request.IsAvailable ?? true),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deletedBy)
    {
        const string sql = @"
            UPDATE MenuItem
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> MenuItemExistsAsync(Guid id)
    {
        const string sql = "SELECT COUNT(*) FROM MenuItem WHERE Id = @Id AND IsDeleted = 0";

        var count = await ExecuteScalarAsync<int>(
            sql,
            CreateParameter("@Id", id));

        return count > 0;
    }

    #region Menu Item Ingredients

    public async Task<List<MenuItemIngredientDto>> GetMenuItemIngredientsAsync(Guid menuItemId)
    {
        const string sql = @"
            SELECT mii.Id, mii.MenuItemId, mii.IngredientId, mii.OrderIndex, mii.IngredientListString,
                   i.Name AS IngredientName, i.Category AS IngredientCategory
            FROM MenuItemIngredient mii
            INNER JOIN Ingredient i ON mii.IngredientId = i.Id
            WHERE mii.MenuItemId = @MenuItemId AND mii.IsDeleted = 0
            ORDER BY mii.OrderIndex";

        return await ExecuteReaderAsync(
            sql,
            reader => new MenuItemIngredientDto
            {
                Id = GetGuid(reader, "Id"),
                MenuItemId = GetGuid(reader, "MenuItemId"),
                IngredientId = GetGuid(reader, "IngredientId"),
                IngredientName = GetString(reader, "IngredientName") ?? string.Empty,
                IngredientCategory = GetString(reader, "IngredientCategory"),
                OrderIndex = GetInt(reader, "OrderIndex") ?? 0,
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@MenuItemId", menuItemId));
    }

    public async Task<Guid> AddMenuItemIngredientAsync(Guid menuItemId, Guid ingredientId, int orderIndex, Guid createdBy)
    {
        const string sql = @"
            INSERT INTO MenuItemIngredient (Id, MenuItemId, IngredientId, OrderIndex, CreatedBy, CreatedAt)
            VALUES (@Id, @MenuItemId, @IngredientId, @OrderIndex, @CreatedBy, GETUTCDATE())";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@IngredientId", ingredientId),
            CreateParameter("@OrderIndex", orderIndex),
            CreateParameter("@CreatedBy", createdBy));

        return id;
    }

    public async Task<bool> RemoveMenuItemIngredientAsync(Guid menuItemIngredientId, Guid deletedBy)
    {
        const string sql = @"
            UPDATE MenuItemIngredient
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", menuItemIngredientId),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    #endregion

    #region Menu Item Nutrition

    public async Task<MenuItemNutritionDto?> GetMenuItemNutritionAsync(Guid menuItemId)
    {
        const string sql = @"
            SELECT MenuItemId, Calories, TotalFat, SaturatedFat, TransFat,
                   Cholesterol, Sodium, TotalCarbohydrates, DietaryFiber,
                   Sugars, Protein, VitaminD, Calcium, Iron, Potassium
            FROM MenuItemNutrition
            WHERE MenuItemId = @MenuItemId";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new MenuItemNutritionDto
            {
                MenuItemId = GetGuid(reader, "MenuItemId"),
                Calories = GetDecimal(reader, "Calories"),
                TotalFat = GetDecimal(reader, "TotalFat"),
                SaturatedFat = GetDecimal(reader, "SaturatedFat"),
                TransFat = GetDecimal(reader, "TransFat"),
                Cholesterol = GetDecimal(reader, "Cholesterol"),
                Sodium = GetDecimal(reader, "Sodium"),
                TotalCarbohydrates = GetDecimal(reader, "TotalCarbohydrates"),
                DietaryFiber = GetDecimal(reader, "DietaryFiber"),
                Sugars = GetDecimal(reader, "Sugars"),
                Protein = GetDecimal(reader, "Protein"),
                VitaminD = GetDecimal(reader, "VitaminD"),
                Calcium = GetDecimal(reader, "Calcium"),
                Iron = GetDecimal(reader, "Iron"),
                Potassium = GetDecimal(reader, "Potassium")
            },
            CreateParameter("@MenuItemId", menuItemId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddOrUpdateMenuItemNutritionAsync(Guid menuItemId, MenuItemNutritionDto nutrition, Guid createdBy)
    {
        // Try to update existing nutrition first
        const string updateSql = @"
            UPDATE MenuItemNutrition
            SET Calories = @Calories,
                TotalFat = @TotalFat,
                SaturatedFat = @SaturatedFat,
                TransFat = @TransFat,
                Cholesterol = @Cholesterol,
                Sodium = @Sodium,
                TotalCarbohydrates = @TotalCarbohydrates,
                DietaryFiber = @DietaryFiber,
                Sugars = @Sugars,
                Protein = @Protein,
                VitaminD = @VitaminD,
                Calcium = @Calcium,
                Iron = @Iron,
                Potassium = @Potassium,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE MenuItemId = @MenuItemId";

        var rowsAffected = await ExecuteNonQueryAsync(
            updateSql,
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@Calories", nutrition.Calories),
            CreateParameter("@TotalFat", nutrition.TotalFat),
            CreateParameter("@SaturatedFat", nutrition.SaturatedFat),
            CreateParameter("@TransFat", nutrition.TransFat),
            CreateParameter("@Cholesterol", nutrition.Cholesterol),
            CreateParameter("@Sodium", nutrition.Sodium),
            CreateParameter("@TotalCarbohydrates", nutrition.TotalCarbohydrates),
            CreateParameter("@DietaryFiber", nutrition.DietaryFiber),
            CreateParameter("@Sugars", nutrition.Sugars),
            CreateParameter("@Protein", nutrition.Protein),
            CreateParameter("@VitaminD", nutrition.VitaminD),
            CreateParameter("@Calcium", nutrition.Calcium),
            CreateParameter("@Iron", nutrition.Iron),
            CreateParameter("@Potassium", nutrition.Potassium),
            CreateParameter("@UpdatedBy", createdBy));

        if (rowsAffected > 0)
        {
            return Guid.Empty; // Updated existing
        }

        // Insert new nutrition
        const string insertSql = @"
            INSERT INTO MenuItemNutrition (
                Id, MenuItemId, Calories, TotalFat, SaturatedFat, TransFat,
                Cholesterol, Sodium, TotalCarbohydrates, DietaryFiber,
                Sugars, Protein, VitaminD, Calcium, Iron, Potassium,
                CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @MenuItemId, @Calories, @TotalFat, @SaturatedFat, @TransFat,
                @Cholesterol, @Sodium, @TotalCarbohydrates, @DietaryFiber,
                @Sugars, @Protein, @VitaminD, @Calcium, @Iron, @Potassium,
                @CreatedBy, GETUTCDATE()
            )";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            insertSql,
            CreateParameter("@Id", id),
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@Calories", nutrition.Calories),
            CreateParameter("@TotalFat", nutrition.TotalFat),
            CreateParameter("@SaturatedFat", nutrition.SaturatedFat),
            CreateParameter("@TransFat", nutrition.TransFat),
            CreateParameter("@Cholesterol", nutrition.Cholesterol),
            CreateParameter("@Sodium", nutrition.Sodium),
            CreateParameter("@TotalCarbohydrates", nutrition.TotalCarbohydrates),
            CreateParameter("@DietaryFiber", nutrition.DietaryFiber),
            CreateParameter("@Sugars", nutrition.Sugars),
            CreateParameter("@Protein", nutrition.Protein),
            CreateParameter("@VitaminD", nutrition.VitaminD),
            CreateParameter("@Calcium", nutrition.Calcium),
            CreateParameter("@Iron", nutrition.Iron),
            CreateParameter("@Potassium", nutrition.Potassium),
            CreateParameter("@CreatedBy", createdBy));

        return id;
    }

    #endregion

    #region User Ratings

    public async Task<List<UserMenuItemRatingDto>> GetMenuItemRatingsAsync(Guid menuItemId)
    {
        const string sql = @"
            SELECT UserId, MenuItemId, Rating, Review, WouldOrderAgain, CreatedAt, UpdatedAt
            FROM UserMenuItemRating
            WHERE MenuItemId = @MenuItemId
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserMenuItemRatingDto
            {
                UserId = GetGuid(reader, "UserId"),
                MenuItemId = GetGuid(reader, "MenuItemId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                Review = GetString(reader, "Review"),
                WouldOrderAgain = GetBool(reader, "WouldOrderAgain"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@MenuItemId", menuItemId));
    }

    public async Task<UserMenuItemRatingDto?> GetUserRatingAsync(Guid menuItemId, Guid userId)
    {
        const string sql = @"
            SELECT UserId, MenuItemId, Rating, Review, WouldOrderAgain, CreatedAt, UpdatedAt
            FROM UserMenuItemRating
            WHERE MenuItemId = @MenuItemId AND UserId = @UserId";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserMenuItemRatingDto
            {
                UserId = GetGuid(reader, "UserId"),
                MenuItemId = GetGuid(reader, "MenuItemId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                Review = GetString(reader, "Review"),
                WouldOrderAgain = GetBool(reader, "WouldOrderAgain"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@UserId", userId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddOrUpdateRatingAsync(Guid menuItemId, Guid userId, RateMenuItemRequest request)
    {
        // Try to update existing rating first
        const string updateSql = @"
            UPDATE UserMenuItemRating
            SET Rating = @Rating,
                Review = @Review,
                WouldOrderAgain = @WouldOrderAgain,
                UpdatedAt = GETUTCDATE()
            WHERE MenuItemId = @MenuItemId AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(
            updateSql,
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@UserId", userId),
            CreateParameter("@Rating", request.Rating),
            CreateParameter("@Review", request.Review),
            CreateParameter("@WouldOrderAgain", request.WouldOrderAgain));

        if (rowsAffected > 0)
        {
            return Guid.Empty; // Updated existing
        }

        // Insert new rating
        const string insertSql = @"
            INSERT INTO UserMenuItemRating (Id, UserId, MenuItemId, Rating, Review, WouldOrderAgain, CreatedAt)
            VALUES (@Id, @UserId, @MenuItemId, @Rating, @Review, @WouldOrderAgain, GETUTCDATE())";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            insertSql,
            CreateParameter("@Id", id),
            CreateParameter("@UserId", userId),
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@Rating", request.Rating),
            CreateParameter("@Review", request.Review),
            CreateParameter("@WouldOrderAgain", request.WouldOrderAgain));

        return id;
    }

    public async Task<bool> DeleteRatingAsync(Guid menuItemId, Guid userId)
    {
        const string sql = @"
            DELETE FROM UserMenuItemRating
            WHERE MenuItemId = @MenuItemId AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    #endregion

    private MenuItemDto MapMenuItemDto(IDataReader reader)
    {
        return new MenuItemDto
        {
            Id = GetGuid(reader, "Id"),
            RestaurantId = GetGuid(reader, "RestaurantId"),
            RestaurantName = GetString(reader, "RestaurantName") ?? string.Empty,
            Name = GetString(reader, "Name") ?? string.Empty,
            Description = GetString(reader, "Description"),
            Category = GetString(reader, "Category"),
            Price = GetDecimal(reader, "Price"),
            Currency = GetString(reader, "Currency") ?? "USD",
            ServingSize = GetDecimal(reader, "ServingSize"),
            ServingUnit = GetString(reader, "ServingUnit"),
            IsAvailable = GetBool(reader, "IsAvailable") ?? true,
            CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
            UpdatedAt = GetDateTime(reader, "UpdatedAt")
        };
    }
}
