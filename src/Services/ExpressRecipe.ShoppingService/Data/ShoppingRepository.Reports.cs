using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for reports and analytics
public partial class ShoppingRepository
{
    public async Task<ShoppingReportDto> GetShoppingReportAsync(Guid userId, Guid? householdId = null)
    {
        const string sql = @"
            SELECT
                COUNT(DISTINCT sl.Id) AS TotalLists,
                SUM(CASE WHEN sl.Status = 'Active' THEN 1 ELSE 0 END) AS ActiveLists,
                SUM(CASE WHEN sl.Status = 'Completed' AND sl.CompletedAt >= DATEADD(month, -1, GETUTCDATE()) THEN 1 ELSE 0 END) AS CompletedThisMonth,
                SUM(CASE WHEN sl.CompletedAt >= DATEADD(month, -1, GETUTCDATE()) THEN ISNULL(sl.ActualCost, 0) ELSE 0 END) AS TotalSpentThisMonth,
                (SELECT COUNT(*) FROM ShoppingListItem sli INNER JOIN ShoppingList sl2 ON sli.ShoppingListId = sl2.Id WHERE sl2.UserId = @UserId AND sli.IsChecked = 1) AS TotalItemsPurchased
            FROM ShoppingList sl
            WHERE (@HouseholdId IS NULL AND sl.UserId = @UserId OR sl.HouseholdId = @HouseholdId)
              AND sl.IsDeleted = 0";

        const string categorySql = @"
            SELECT sli.Category, COUNT(*) AS ItemCount
            FROM ShoppingListItem sli
            INNER JOIN ShoppingList sl ON sli.ShoppingListId = sl.Id
            WHERE (@HouseholdId IS NULL AND sl.UserId = @UserId OR sl.HouseholdId = @HouseholdId)
              AND sli.Category IS NOT NULL
            GROUP BY sli.Category
            ORDER BY ItemCount DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var report = new ShoppingReportDto();

        // Get totals
        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                report.TotalLists = reader.GetInt32(0);
                report.ActiveLists = reader.GetInt32(1);
                report.CompletedListsThisMonth = reader.GetInt32(2);
                report.TotalSpentThisMonth = reader.GetDecimal(3);
                report.TotalItemsPurchased = reader.GetInt32(4);
            }
        }

        // Get items by category
        await using (var command = new SqlCommand(categorySql, connection))
        {
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                report.ItemsByCategory[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        // Get top favorites
        report.TopFavorites = householdId.HasValue
            ? await GetHouseholdFavoritesAsync(householdId.Value)
            : await GetUserFavoritesAsync(userId);

        if (report.TopFavorites.Count > 10)
            report.TopFavorites = report.TopFavorites.Take(10).ToList();

        // Most-used store tracking: add a StoreId column to ShoppingSession and aggregate here.

        return report;
    }

    public async Task<List<StoreDto>> GetUserStoresAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, Name, Chain, Address, City, State, ZipCode, Latitude, Longitude, Phone, IsPreferred, CreatedAt
            FROM Store
            WHERE IsDeleted = 0
            ORDER BY IsPreferred DESC, Name";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);

        var stores = new List<StoreDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            stores.Add(new StoreDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Chain = reader.IsDBNull(2) ? null : reader.GetString(2),
                Address = reader.IsDBNull(3) ? null : reader.GetString(3),
                City = reader.IsDBNull(4) ? null : reader.GetString(4),
                State = reader.IsDBNull(5) ? null : reader.GetString(5),
                ZipCode = reader.IsDBNull(6) ? null : reader.GetString(6),
                Latitude = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                Longitude = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                Phone = reader.IsDBNull(9) ? null : reader.GetString(9),
                IsPreferred = reader.GetBoolean(10),
                CreatedAt = reader.GetDateTime(11)
            });
        }

        return stores;
    }
}
