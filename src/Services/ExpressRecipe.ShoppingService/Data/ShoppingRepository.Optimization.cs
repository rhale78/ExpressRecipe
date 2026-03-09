using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for optimization, preferences, and aisle sorting
public partial class ShoppingRepository
{
    // ── Category preferences ──────────────────────────────────────────────────

    public async Task UpsertStoreCategoryPreferenceAsync(UserStoreCategoryPreferenceRecord pref, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE UserStoreCategoryPreference AS target
            USING (SELECT @UserId AS UserId, @Category AS Category, @RankOrder AS RankOrder) AS src
                ON target.UserId = src.UserId AND target.Category = src.Category AND target.RankOrder = src.RankOrder
            WHEN MATCHED THEN
                UPDATE SET PreferredStoreId = @PreferredStoreId,
                           HouseholdId = @HouseholdId,
                           IsActive = 1
            WHEN NOT MATCHED THEN
                INSERT (UserId, HouseholdId, Category, PreferredStoreId, RankOrder, IsActive)
                VALUES (@UserId, @HouseholdId, @Category, @PreferredStoreId, @RankOrder, 1);";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", pref.UserId);
        command.Parameters.AddWithValue("@HouseholdId", pref.HouseholdId.HasValue ? pref.HouseholdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Category", pref.Category);
        command.Parameters.AddWithValue("@PreferredStoreId", pref.PreferredStoreId);
        command.Parameters.AddWithValue("@RankOrder", (byte)pref.RankOrder);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<UserStoreCategoryPreferenceDto>> GetUserCategoryPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT p.Id, p.UserId, p.HouseholdId, p.Category, p.PreferredStoreId, s.Name AS StoreName, p.RankOrder, p.IsActive
            FROM UserStoreCategoryPreference p
            LEFT JOIN Store s ON p.PreferredStoreId = s.Id
            WHERE p.UserId = @UserId AND p.IsActive = 1
            ORDER BY p.Category, p.RankOrder";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        List<UserStoreCategoryPreferenceDto> results = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new UserStoreCategoryPreferenceDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                Category = reader.GetString(3),
                PreferredStoreId = reader.GetGuid(4),
                PreferredStoreName = reader.IsDBNull(5) ? null : reader.GetString(5),
                RankOrder = reader.GetByte(6),
                IsActive = reader.GetBoolean(7)
            });
        }
        return results;
    }

    public async Task DeleteStoreCategoryPreferenceAsync(Guid preferenceId, Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE UserStoreCategoryPreference
            SET IsActive = 0
            WHERE Id = @Id AND UserId = @UserId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@Id", preferenceId);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.ExecuteNonQueryAsync(ct);
    }

    // ── Optimization result ───────────────────────────────────────────────────

    public async Task SaveOptimizationResultAsync(Guid listId, string strategy, string resultJson, decimal total, decimal totalWithDeals, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE ShoppingListOptimization AS target
            USING (SELECT @ShoppingListId AS ShoppingListId) AS src
                ON target.ShoppingListId = src.ShoppingListId
            WHEN MATCHED THEN
                UPDATE SET Strategy = @Strategy, OptimizedAt = GETUTCDATE(),
                           TotalEstimate = @Total, TotalWithDeals = @TotalWithDeals,
                           StoreCount = @StoreCount, ResultJson = @ResultJson
            WHEN NOT MATCHED THEN
                INSERT (ShoppingListId, Strategy, OptimizedAt, TotalEstimate, TotalWithDeals, StoreCount, ResultJson)
                VALUES (@ShoppingListId, @Strategy, GETUTCDATE(), @Total, @TotalWithDeals, @StoreCount, @ResultJson);

            UPDATE ShoppingList SET OptimizedAt = GETUTCDATE() WHERE Id = @ShoppingListId;";

        // Derive StoreCount from resultJson quickly without full deserialization
        int storeCount = CountStoreGroupsFromJson(resultJson);

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@ShoppingListId", listId);
        command.Parameters.AddWithValue("@Strategy", strategy);
        command.Parameters.AddWithValue("@Total", total);
        command.Parameters.AddWithValue("@TotalWithDeals", totalWithDeals);
        command.Parameters.AddWithValue("@StoreCount", storeCount);
        command.Parameters.AddWithValue("@ResultJson", resultJson);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<ShoppingListOptimizationDto?> GetOptimizationResultAsync(Guid listId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, ShoppingListId, Strategy, OptimizedAt, TotalEstimate, TotalWithDeals, StoreCount, ResultJson
            FROM ShoppingListOptimization
            WHERE ShoppingListId = @ListId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);

        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new ShoppingListOptimizationDto
            {
                Id = reader.GetGuid(0),
                ShoppingListId = reader.GetGuid(1),
                Strategy = reader.GetString(2),
                OptimizedAt = reader.GetDateTime(3),
                TotalEstimate = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                TotalWithDeals = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                StoreCount = reader.GetInt32(6),
                ResultJson = reader.GetString(7)
            };
        }
        return null;
    }

    // ── Price search profile ──────────────────────────────────────────────────

    public async Task UpsertPriceSearchProfileAsync(UserPriceSearchProfileRecord profile, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE UserPriceSearchProfile AS target
            USING (SELECT @UserId AS UserId) AS src ON target.UserId = src.UserId
            WHEN MATCHED THEN
                UPDATE SET StrategyPriority = @StrategyPriority,
                           MaxStoreDistanceMiles = @MaxStoreDistanceMiles,
                           OnlineAllowed = @OnlineAllowed,
                           DeliveryAllowed = @DeliveryAllowed,
                           PreferredBrandIds = @PreferredBrandIds,
                           MinRating = @MinRating,
                           TryNewBrandsEnabled = @TryNewBrandsEnabled,
                           UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, StrategyPriority, MaxStoreDistanceMiles, OnlineAllowed, DeliveryAllowed,
                        PreferredBrandIds, MinRating, TryNewBrandsEnabled, UpdatedAt)
                VALUES (@UserId, @StrategyPriority, @MaxStoreDistanceMiles, @OnlineAllowed, @DeliveryAllowed,
                        @PreferredBrandIds, @MinRating, @TryNewBrandsEnabled, GETUTCDATE());";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", profile.UserId);
        command.Parameters.AddWithValue("@StrategyPriority", profile.StrategyPriority);
        command.Parameters.AddWithValue("@MaxStoreDistanceMiles", profile.MaxStoreDistanceMiles);
        command.Parameters.AddWithValue("@OnlineAllowed", profile.OnlineAllowed);
        command.Parameters.AddWithValue("@DeliveryAllowed", profile.DeliveryAllowed);
        command.Parameters.AddWithValue("@PreferredBrandIds", profile.PreferredBrandIds ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@MinRating", profile.MinRating.HasValue ? profile.MinRating.Value : DBNull.Value);
        command.Parameters.AddWithValue("@TryNewBrandsEnabled", profile.TryNewBrandsEnabled);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<UserPriceSearchProfileDto?> GetPriceSearchProfileAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, StrategyPriority, MaxStoreDistanceMiles, OnlineAllowed, DeliveryAllowed,
                   PreferredBrandIds, MinRating, TryNewBrandsEnabled, UpdatedAt
            FROM UserPriceSearchProfile
            WHERE UserId = @UserId";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new UserPriceSearchProfileDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                StrategyPriority = reader.GetString(2),
                MaxStoreDistanceMiles = reader.GetInt32(3),
                OnlineAllowed = reader.GetBoolean(4),
                DeliveryAllowed = reader.GetBoolean(5),
                PreferredBrandIds = reader.IsDBNull(6) ? null : reader.GetString(6),
                MinRating = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                TryNewBrandsEnabled = reader.GetBoolean(8),
                UpdatedAt = reader.GetDateTime(9)
            };
        }
        return null;
    }

    // ── Aisle-sorted items ────────────────────────────────────────────────────

    public async Task<List<OptimizedShoppingItem>> GetItemsSortedByAisleAsync(Guid listId, Guid storeId, string sortMode, CancellationToken ct = default)
    {
        // Load items with store layout data.
        // Product names come from CustomName on the ShoppingListItem; cross-service product
        // lookups (by ProductId) are handled at the application/service layer.
        const string sql = @"
            SELECT
                i.Id, COALESCE(i.CustomName, '') AS Name,
                i.Quantity, i.Unit,
                sl.Aisle, COALESCE(sl.OrderIndex, 9999) AS AisleOrder,
                sl.ZoneType,
                i.EstimatedPrice, i.HasDeal, i.DealDescription
            FROM ShoppingListItem i
            LEFT JOIN StoreLayout sl ON sl.StoreId = @StoreId
                AND sl.CategoryName = i.Category
            WHERE i.ShoppingListId = @ListId AND i.IsDeleted = 0 AND i.IsChecked = 0
            ORDER BY COALESCE(sl.OrderIndex, 9999) ASC, i.AddedAt ASC";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);
        command.Parameters.AddWithValue("@StoreId", storeId);

        List<(OptimizedShoppingItem Item, string? ZoneType, int AisleOrder)> raw = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            string? zoneType = reader.IsDBNull(6) ? null : reader.GetString(6);
            int aisleOrder = reader.GetInt32(5);
            raw.Add((new OptimizedShoppingItem
            {
                ShoppingListItemId = reader.GetGuid(0),
                Name = reader.GetString(1),
                Quantity = reader.GetDecimal(2),
                Unit = reader.IsDBNull(3) ? null : reader.GetString(3),
                Aisle = reader.IsDBNull(4) ? null : reader.GetString(4),
                AisleOrder = aisleOrder,
                Price = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                HasDeal = reader.GetBoolean(8),
                DealDescription = reader.IsDBNull(9) ? null : reader.GetString(9)
            }, zoneType, aisleOrder));
        }

        return ApplyAisleSortMode(raw, sortMode);
    }

    private static List<OptimizedShoppingItem> ApplyAisleSortMode(
        List<(OptimizedShoppingItem Item, string? ZoneType, int AisleOrder)> raw, string sortMode)
    {
        IEnumerable<(OptimizedShoppingItem Item, string? ZoneType, int AisleOrder)> sorted = sortMode switch
        {
            "ColdLast" => raw.OrderBy(x => IsColdZone(x.ZoneType) ? 1 : 0).ThenBy(x => x.AisleOrder),
            "BackToFront" => raw.OrderByDescending(x => x.AisleOrder),
            "Category" => raw.OrderBy(x => x.Item.Aisle ?? "ZZZ").ThenBy(x => x.AisleOrder),
            _ => raw.OrderBy(x => x.AisleOrder) // default: Aisle
        };
        return sorted.Select(x => x.Item).ToList();
    }

    private static bool IsColdZone(string? zoneType) =>
        zoneType is "Dairy" or "Frozen" or "Meat";

    // ── Complete shopping session ─────────────────────────────────────────────

    public async Task<ShoppingSessionSummaryDto> CompleteShoppingSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        // Load session details
        const string sessionSql = @"
            SELECT ss.Id, ss.UserId, ss.ShoppingListId, ss.TotalSpent
            FROM ShoppingScanSession ss
            WHERE ss.Id = @SessionId";

        Guid userId;
        Guid shoppingListId;
        decimal? totalSpent;

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await using (SqlCommand cmd = new(sessionSql, connection))
        {
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
            {
                throw new InvalidOperationException($"Session {sessionId} not found.");
            }
            userId = r.GetGuid(1);
            shoppingListId = r.GetGuid(2);
            totalSpent = r.IsDBNull(3) ? null : r.GetDecimal(3);
        }

        // Get checked items (including those that should add to inventory)
        const string itemsSql = @"
            SELECT Id, ProductId, CustomName, Quantity, Unit, ActualPrice, AddToInventoryOnPurchase
            FROM ShoppingListItem
            WHERE ShoppingListId = @ListId AND IsChecked = 1 AND IsDeleted = 0";

        List<(Guid ItemId, Guid? ProductId, string? Name, decimal Qty, string? Unit, decimal? Price, bool AddToInventory)> checkedItems = new();
        await using (SqlCommand cmd = new(itemsSql, connection))
        {
            cmd.Parameters.AddWithValue("@ListId", shoppingListId);
            await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                checkedItems.Add((
                    r.GetGuid(0),
                    r.IsDBNull(1) ? null : r.GetGuid(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.GetDecimal(3),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetDecimal(5),
                    r.GetBoolean(6)
                ));
            }
        }

        // Mark session as ended
        const string endSql = @"
            UPDATE ShoppingScanSession
            SET EndedAt = GETUTCDATE(), IsActive = 0
            WHERE Id = @SessionId";
        await using (SqlCommand cmd = new(endSql, connection))
        {
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Note: actual HTTP calls to InventoryService are handled by ShoppingOptimizationService
        // Here we just return the summary; the controller calls InventoryService for each item
        _logger.LogInformation(
            "Completed session {SessionId}: {Count} checked items, totalSpent={Total}",
            sessionId, checkedItems.Count, totalSpent);

        return new ShoppingSessionSummaryDto
        {
            SessionId = sessionId,
            ItemsChecked = checkedItems.Count,
            InventoryEventsPublished = 0, // will be updated by service layer
            TotalSpent = totalSpent,
            CompletedAt = DateTime.UtcNow
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountStoreGroupsFromJson(string resultJson)
    {
        // Quick count of StoreId occurrences without full deserialization
        int count = 0;
        int idx = 0;
        const string marker = "\"storeId\"";
        while ((idx = resultJson.IndexOf(marker, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += marker.Length;
        }
        return Math.Max(count, 1);
    }
}
