using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.MenuItemService.Data;

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

    // GDPR
    Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default);
}

public class MenuItemRepository : SqlHelper, IMenuItemRepository
{
    private readonly HybridCacheService? _cache;
    private const string CachePrefix = "menuitem:";

    public MenuItemRepository(string connectionString, HybridCacheService? cache = null)
        : base(connectionString)
    {
        _cache = cache;
    }

    public async Task<List<MenuItemDto>> SearchAsync(MenuItemSearchRequest request)
    {
        var whereClauses = new List<string> { "mi.IsDeleted = 0" };
        var parameters = new List<SqlParameter>();

        if (request.RestaurantId.HasValue)
        {
            whereClauses.Add("mi.RestaurantId = @RestaurantId");
            parameters.Add((SqlParameter)CreateParameter("@RestaurantId", request.RestaurantId.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            whereClauses.Add("(mi.Name LIKE @SearchTerm OR mi.Description LIKE @SearchTerm)");
            parameters.Add((SqlParameter)CreateParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            whereClauses.Add("mi.Category = @Category");
            parameters.Add((SqlParameter)CreateParameter("@Category", request.Category));
        }

        if (request.OnlyAvailable == true)
        {
            whereClauses.Add("mi.IsAvailable = 1");
        }

        if (request.OnlyApproved == true)
        {
            whereClauses.Add("mi.ApprovalStatus = 'Approved'");
        }

        if (request.MaxPrice.HasValue)
        {
            whereClauses.Add("mi.Price <= @MaxPrice");
            parameters.Add((SqlParameter)CreateParameter("@MaxPrice", request.MaxPrice.Value));
        }

        var sql = $@"
            SELECT mi.Id, mi.RestaurantId, mi.Name, mi.Description, mi.Category,
                   mi.Price, mi.Currency, mi.ServingSize, mi.ServingUnit,
                   mi.IsAvailable, mi.IsSeasonalItem, mi.ApprovalStatus,
                   mi.AverageRating, mi.RatingCount,
                   mi.CreatedAt, mi.UpdatedAt
            FROM MenuItem mi
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
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}id:{id}",
                async (ct) => await GetByIdFromDbAsync(id),
                expiration: TimeSpan.FromHours(1));
        }

        return await GetByIdFromDbAsync(id);
    }

    private async Task<MenuItemDto?> GetByIdFromDbAsync(Guid id)
    {
        const string sql = @"
            SELECT mi.Id, mi.RestaurantId, mi.Name, mi.Description, mi.Category,
                   mi.Price, mi.Currency, mi.ServingSize, mi.ServingUnit,
                   mi.IsAvailable, mi.IsSeasonalItem, mi.ApprovalStatus,
                   mi.AverageRating, mi.RatingCount,
                   mi.CreatedAt, mi.UpdatedAt
            FROM MenuItem mi
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
                IsSeasonalItem, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @RestaurantId, @Name, @Description, @Category,
                @Price, @Currency, @ServingSize, @ServingUnit, @IsAvailable,
                @IsSeasonalItem, @CreatedBy, GETUTCDATE()
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
            CreateParameter("@IsAvailable", request.IsAvailable),
            CreateParameter("@IsSeasonalItem", request.IsSeasonalItem),
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
                IsSeasonalItem = @IsSeasonalItem,
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
            CreateParameter("@IsAvailable", request.IsAvailable),
            CreateParameter("@IsSeasonalItem", request.IsSeasonalItem),
            CreateParameter("@UpdatedBy", updatedBy));

        if (rowsAffected > 0 && _cache != null)
            await _cache.RemoveAsync($"{CachePrefix}id:{id}");

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

        if (rowsAffected > 0 && _cache != null)
            await _cache.RemoveAsync($"{CachePrefix}id:{id}");

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
            SELECT mii.Id, mii.MenuItemId, mii.IngredientId, mii.OrderIndex,
                   mii.IngredientListString, mii.Notes
            FROM MenuItemIngredient mii
            WHERE mii.MenuItemId = @MenuItemId AND mii.IsDeleted = 0
            ORDER BY mii.OrderIndex";

        return await ExecuteReaderAsync(
            sql,
            reader => new MenuItemIngredientDto
            {
                Id = GetGuid(reader, "Id"),
                MenuItemId = GetGuid(reader, "MenuItemId"),
                IngredientId = GetGuidNullable(reader, "IngredientId") ?? Guid.Empty,
                IngredientName = string.Empty,
                OrderIndex = GetInt(reader, "OrderIndex") ?? 0,
                Notes = GetString(reader, "Notes"),
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
            SELECT Id, MenuItemId, Calories, TotalFat, SaturatedFat, TransFat,
                   Cholesterol, Sodium, TotalCarbohydrates, DietaryFiber,
                   Sugars, Protein, VitaminD, Calcium, Iron, Potassium,
                   AdditionalNutrients
            FROM MenuItemNutrition
            WHERE MenuItemId = @MenuItemId";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new MenuItemNutritionDto
            {
                Id = GetGuid(reader, "Id"),
                MenuItemId = GetGuid(reader, "MenuItemId"),
                Calories = GetDecimalNullable(reader, "Calories"),
                TotalFat = GetDecimalNullable(reader, "TotalFat"),
                SaturatedFat = GetDecimalNullable(reader, "SaturatedFat"),
                TransFat = GetDecimalNullable(reader, "TransFat"),
                Cholesterol = GetDecimalNullable(reader, "Cholesterol"),
                Sodium = GetDecimalNullable(reader, "Sodium"),
                TotalCarbohydrates = GetDecimalNullable(reader, "TotalCarbohydrates"),
                DietaryFiber = GetDecimalNullable(reader, "DietaryFiber"),
                Sugars = GetDecimalNullable(reader, "Sugars"),
                Protein = GetDecimalNullable(reader, "Protein"),
                VitaminD = GetDecimalNullable(reader, "VitaminD"),
                Calcium = GetDecimalNullable(reader, "Calcium"),
                Iron = GetDecimalNullable(reader, "Iron"),
                Potassium = GetDecimalNullable(reader, "Potassium"),
                AdditionalNutrients = GetString(reader, "AdditionalNutrients")
            },
            CreateParameter("@MenuItemId", menuItemId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddOrUpdateMenuItemNutritionAsync(Guid menuItemId, MenuItemNutritionDto nutrition, Guid createdBy)
    {
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
                AdditionalNutrients = @AdditionalNutrients,
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
            CreateParameter("@AdditionalNutrients", nutrition.AdditionalNutrients),
            CreateParameter("@UpdatedBy", createdBy));

        if (rowsAffected > 0)
        {
            return Guid.Empty;
        }

        const string insertSql = @"
            INSERT INTO MenuItemNutrition (
                Id, MenuItemId, Calories, TotalFat, SaturatedFat, TransFat,
                Cholesterol, Sodium, TotalCarbohydrates, DietaryFiber,
                Sugars, Protein, VitaminD, Calcium, Iron, Potassium,
                AdditionalNutrients, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @MenuItemId, @Calories, @TotalFat, @SaturatedFat, @TransFat,
                @Cholesterol, @Sodium, @TotalCarbohydrates, @DietaryFiber,
                @Sugars, @Protein, @VitaminD, @Calcium, @Iron, @Potassium,
                @AdditionalNutrients, @CreatedBy, GETUTCDATE()
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
            CreateParameter("@AdditionalNutrients", nutrition.AdditionalNutrients),
            CreateParameter("@CreatedBy", createdBy));

        return id;
    }

    #endregion

    #region User Ratings

    public async Task<List<UserMenuItemRatingDto>> GetMenuItemRatingsAsync(Guid menuItemId)
    {
        const string sql = @"
            SELECT Id, UserId, MenuItemId, Rating, Review, WouldOrderAgain, CreatedAt, UpdatedAt
            FROM UserMenuItemRating
            WHERE MenuItemId = @MenuItemId AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserMenuItemRatingDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                MenuItemId = GetGuid(reader, "MenuItemId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                Review = GetString(reader, "Review"),
                WouldOrderAgain = GetBool(reader, "WouldOrderAgain"),
                CreatedAt = GetNullableDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@MenuItemId", menuItemId));
    }

    public async Task<UserMenuItemRatingDto?> GetUserRatingAsync(Guid menuItemId, Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, MenuItemId, Rating, Review, WouldOrderAgain, CreatedAt, UpdatedAt
            FROM UserMenuItemRating
            WHERE MenuItemId = @MenuItemId AND UserId = @UserId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserMenuItemRatingDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                MenuItemId = GetGuid(reader, "MenuItemId"),
                Rating = GetInt(reader, "Rating") ?? 0,
                Review = GetString(reader, "Review"),
                WouldOrderAgain = GetBool(reader, "WouldOrderAgain"),
                CreatedAt = GetNullableDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@UserId", userId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddOrUpdateRatingAsync(Guid menuItemId, Guid userId, RateMenuItemRequest request)
    {
        const string updateSql = @"
            UPDATE UserMenuItemRating
            SET Rating = @Rating,
                Review = @Review,
                WouldOrderAgain = @WouldOrderAgain,
                UpdatedAt = GETUTCDATE()
            WHERE MenuItemId = @MenuItemId AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            updateSql,
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@UserId", userId),
            CreateParameter("@Rating", request.Rating),
            CreateParameter("@Review", request.Review),
            CreateParameter("@WouldOrderAgain", request.WouldOrderAgain));

        if (rowsAffected > 0)
        {
            await RecalculateAverageRatingAsync(menuItemId);
            return Guid.Empty;
        }

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

        await RecalculateAverageRatingAsync(menuItemId);

        return id;
    }

    public async Task<bool> DeleteRatingAsync(Guid menuItemId, Guid userId)
    {
        const string sql = @"
            UPDATE UserMenuItemRating
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedAt = GETUTCDATE()
            WHERE MenuItemId = @MenuItemId AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@MenuItemId", menuItemId),
            CreateParameter("@UserId", userId));

        if (rowsAffected > 0)
        {
            await RecalculateAverageRatingAsync(menuItemId);
        }

        return rowsAffected > 0;
    }

    private async Task RecalculateAverageRatingAsync(Guid menuItemId)
    {
        const string sql = @"
            UPDATE MenuItem
            SET AverageRating = (
                    SELECT AVG(CAST(Rating AS DECIMAL(3,2)))
                    FROM UserMenuItemRating
                    WHERE MenuItemId = @MenuItemId AND IsDeleted = 0
                ),
                RatingCount = (
                    SELECT COUNT(*)
                    FROM UserMenuItemRating
                    WHERE MenuItemId = @MenuItemId AND IsDeleted = 0
                ),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @MenuItemId";

        await ExecuteNonQueryAsync(sql, CreateParameter("@MenuItemId", menuItemId));
    }

    #endregion

    private MenuItemDto MapMenuItemDto(IDataReader reader)
    {
        return new MenuItemDto
        {
            Id = GetGuid(reader, "Id"),
            RestaurantId = GetGuid(reader, "RestaurantId"),
            RestaurantName = string.Empty,
            Name = GetString(reader, "Name") ?? string.Empty,
            Description = GetString(reader, "Description"),
            Category = GetString(reader, "Category"),
            Price = GetDecimalNullable(reader, "Price"),
            Currency = GetString(reader, "Currency") ?? "USD",
            ServingSize = GetNullableString(reader, "ServingSize"),
            ServingUnit = GetString(reader, "ServingUnit"),
            IsAvailable = GetBool(reader, "IsAvailable") ?? true,
            IsSeasonalItem = GetBool(reader, "IsSeasonalItem") ?? false,
            ApprovalStatus = GetString(reader, "ApprovalStatus") ?? "Pending",
            AverageRating = GetDecimalNullable(reader, "AverageRating"),
            RatingCount = GetInt(reader, "RatingCount") ?? 0,
            CreatedAt = GetNullableDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
        };
    }

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM UserMenuItemRating WHERE UserId = @UserId;";
        await ExecuteNonQueryAsync(sql, ct, CreateParameter("@UserId", userId));
    }
}
