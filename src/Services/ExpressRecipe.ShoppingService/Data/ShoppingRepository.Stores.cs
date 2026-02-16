using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for store management and price comparison
public partial class ShoppingRepository
{
    public async Task<Guid> CreateStoreAsync(Guid userId, string name, string? chain, string? address, string? city, string? state, 
        string? zipCode, decimal? latitude, decimal? longitude)
    {
        const string sql = @"
            INSERT INTO Store (Name, Chain, Address, City, State, ZipCode, Latitude, Longitude, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Chain, @Address, @City, @State, @ZipCode, @Latitude, @Longitude, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Chain", chain ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Address", address ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@City", city ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@State", state ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ZipCode", zipCode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Latitude", latitude.HasValue ? latitude.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Longitude", longitude.HasValue ? longitude.Value : DBNull.Value);

        var storeId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Created store {StoreId}: {Name}", storeId, name);
        return storeId;
    }

    public async Task<List<StoreDto>> GetNearbyStoresAsync(decimal latitude, decimal longitude, double maxDistanceKm = 10.0)
    {
        // Haversine formula for distance calculation (same as Inventory Service)
        const string sql = @"
            SELECT 
                s.Id, s.Name, s.Chain, s.Address, s.City, s.State, s.ZipCode,
                s.Latitude, s.Longitude, s.Phone, s.IsPreferred, s.CreatedAt,
                (6371 * ACOS(
                    COS(RADIANS(@Latitude)) * COS(RADIANS(s.Latitude)) * 
                    COS(RADIANS(s.Longitude) - RADIANS(@Longitude)) + 
                    SIN(RADIANS(@Latitude)) * SIN(RADIANS(s.Latitude))
                )) AS DistanceKm
            FROM Store s
            WHERE s.IsDeleted = 0
              AND s.Latitude IS NOT NULL 
              AND s.Longitude IS NOT NULL
            HAVING (6371 * ACOS(
                    COS(RADIANS(@Latitude)) * COS(RADIANS(s.Latitude)) * 
                    COS(RADIANS(s.Longitude) - RADIANS(@Longitude)) + 
                    SIN(RADIANS(@Latitude)) * SIN(RADIANS(s.Latitude))
                )) <= @MaxDistanceKm
            ORDER BY DistanceKm";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Latitude", latitude);
        command.Parameters.AddWithValue("@Longitude", longitude);
        command.Parameters.AddWithValue("@MaxDistanceKm", maxDistanceKm);

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
                CreatedAt = reader.GetDateTime(11),
                DistanceKm = reader.GetDouble(12)
            });
        }

        return stores;
    }

    public async Task<StoreDto?> GetStoreByIdAsync(Guid storeId)
    {
        const string sql = @"
            SELECT Id, Name, Chain, Address, City, State, ZipCode, Latitude, Longitude, Phone, IsPreferred, CreatedAt
            FROM Store
            WHERE Id = @StoreId AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@StoreId", storeId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new StoreDto
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
            };
        }

        return null;
    }

    public async Task UpdateStoreAsync(Guid storeId, string name, string? address, decimal? latitude, decimal? longitude)
    {
        const string sql = @"
            UPDATE Store
            SET Name = @Name, Address = @Address, Latitude = @Latitude, Longitude = @Longitude, UpdatedAt = GETUTCDATE()
            WHERE Id = @StoreId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@StoreId", storeId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Address", address ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Latitude", latitude.HasValue ? latitude.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Longitude", longitude.HasValue ? longitude.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task SetPreferredStoreAsync(Guid userId, Guid storeId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        try
        {
            // Unset all preferred stores
            const string unsetSql = "UPDATE Store SET IsPreferred = 0";
            await using (var command = new SqlCommand(unsetSql, connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            // Set new preferred store
            const string setSql = "UPDATE Store SET IsPreferred = 1 WHERE Id = @StoreId";
            await using (var command = new SqlCommand(setSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@StoreId", storeId);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Set preferred store {StoreId} for user {UserId}", storeId, userId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Guid> CreateStoreLayoutAsync(Guid userId, Guid storeId, string categoryName, string? aisle, int orderIndex)
    {
        const string sql = @"
            INSERT INTO StoreLayout (UserId, StoreId, Category, Aisle, OrderIndex)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @StoreId, @Category, @Aisle, @OrderIndex)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@StoreId", storeId);
        command.Parameters.AddWithValue("@Category", categoryName);
        command.Parameters.AddWithValue("@Aisle", aisle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@OrderIndex", orderIndex);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<StoreLayoutDto>> GetStoreLayoutAsync(Guid storeId)
    {
        const string sql = @"
            SELECT 
                sl.Id, sl.StoreId, s.Name AS StoreName, sl.Category, sl.Aisle, sl.OrderIndex
            FROM StoreLayout sl
            INNER JOIN Store s ON sl.StoreId = s.Id
            WHERE sl.StoreId = @StoreId
            ORDER BY sl.OrderIndex, sl.Category";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@StoreId", storeId);

        var layouts = new List<StoreLayoutDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            layouts.Add(new StoreLayoutDto
            {
                Id = reader.GetGuid(0),
                StoreId = reader.GetGuid(1),
                StoreName = reader.GetString(2),
                CategoryName = reader.GetString(3),
                Aisle = reader.IsDBNull(4) ? null : reader.GetString(4),
                OrderIndex = reader.GetInt32(5)
            });
        }

        return layouts;
    }

    public async Task UpdateStoreLayoutAsync(Guid layoutId, string? aisle, int orderIndex)
    {
        const string sql = @"
            UPDATE StoreLayout
            SET Aisle = @Aisle, OrderIndex = @OrderIndex
            WHERE Id = @LayoutId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LayoutId", layoutId);
        command.Parameters.AddWithValue("@Aisle", aisle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@OrderIndex", orderIndex);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> RecordPriceComparisonAsync(Guid shoppingListItemId, Guid? productId, Guid storeId, decimal price, 
        decimal? unitPrice, decimal? size, string? unit, bool hasDeal, string? dealType, DateTime? dealEndDate)
    {
        const string sql = @"
            INSERT INTO PriceComparison 
            (ShoppingListItemId, ProductId, StoreId, Price, UnitPrice, Size, Unit, HasDeal, DealType, DealEndDate, LastChecked)
            OUTPUT INSERTED.Id
            VALUES (@ItemId, @ProductId, @StoreId, @Price, @UnitPrice, @Size, @Unit, @HasDeal, @DealType, @DealEndDate, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", shoppingListItemId);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@StoreId", storeId);
        command.Parameters.AddWithValue("@Price", price);
        command.Parameters.AddWithValue("@UnitPrice", unitPrice.HasValue ? unitPrice.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Size", size.HasValue ? size.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Unit", unit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@HasDeal", hasDeal);
        command.Parameters.AddWithValue("@DealType", dealType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DealEndDate", dealEndDate.HasValue ? dealEndDate.Value : DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<PriceComparisonDto>> GetPriceComparisonsAsync(Guid shoppingListItemId)
    {
        const string sql = @"
            SELECT 
                pc.Id, pc.ShoppingListItemId, pc.ProductId, pc.StoreId, s.Name AS StoreName,
                pc.Price, pc.UnitPrice, pc.Size, pc.Unit, pc.HasDeal, pc.DealType, pc.DealEndDate,
                pc.IsAvailable, pc.LastChecked
            FROM PriceComparison pc
            INNER JOIN Store s ON pc.StoreId = s.Id
            WHERE pc.ShoppingListItemId = @ItemId
            ORDER BY pc.Price ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", shoppingListItemId);

        var comparisons = new List<PriceComparisonDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            comparisons.Add(new PriceComparisonDto
            {
                Id = reader.GetGuid(0),
                ShoppingListItemId = reader.GetGuid(1),
                ProductId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                StoreId = reader.GetGuid(3),
                StoreName = reader.GetString(4),
                Price = reader.GetDecimal(5),
                UnitPrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                Size = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                Unit = reader.IsDBNull(8) ? null : reader.GetString(8),
                HasDeal = reader.GetBoolean(9),
                DealType = reader.IsDBNull(10) ? null : reader.GetString(10),
                DealEndDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                IsAvailable = reader.GetBoolean(12),
                LastChecked = reader.GetDateTime(13)
            });
        }

        return comparisons;
    }

    public async Task<List<PriceComparisonDto>> GetBestPricesAsync(Guid productId, Guid? preferredStoreId = null)
    {
        string sql;
        if (preferredStoreId.HasValue)
        {
            sql = @"
                SELECT TOP 5 
                    pc.Id, pc.ShoppingListItemId, pc.ProductId, pc.StoreId, s.Name AS StoreName,
                    pc.Price, pc.UnitPrice, pc.Size, pc.Unit, pc.HasDeal, pc.DealType, pc.DealEndDate,
                    pc.IsAvailable, pc.LastChecked
                FROM PriceComparison pc
                INNER JOIN Store s ON pc.StoreId = s.Id
                WHERE pc.ProductId = @ProductId AND pc.IsAvailable = 1
                ORDER BY 
                    CASE WHEN pc.StoreId = @PreferredStoreId THEN 0 ELSE 1 END,
                    pc.Price ASC";
        }
        else
        {
            sql = @"
                SELECT TOP 5 
                    pc.Id, pc.ShoppingListItemId, pc.ProductId, pc.StoreId, s.Name AS StoreName,
                    pc.Price, pc.UnitPrice, pc.Size, pc.Unit, pc.HasDeal, pc.DealType, pc.DealEndDate,
                    pc.IsAvailable, pc.LastChecked
                FROM PriceComparison pc
                INNER JOIN Store s ON pc.StoreId = s.Id
                WHERE pc.ProductId = @ProductId AND pc.IsAvailable = 1
                ORDER BY pc.Price ASC";
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProductId", productId);
        if (preferredStoreId.HasValue)
            command.Parameters.AddWithValue("@PreferredStoreId", preferredStoreId.Value);

        var comparisons = new List<PriceComparisonDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            comparisons.Add(new PriceComparisonDto
            {
                Id = reader.GetGuid(0),
                ShoppingListItemId = reader.GetGuid(1),
                ProductId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                StoreId = reader.GetGuid(3),
                StoreName = reader.GetString(4),
                Price = reader.GetDecimal(5),
                UnitPrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                Size = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                Unit = reader.IsDBNull(8) ? null : reader.GetString(8),
                HasDeal = reader.GetBoolean(9),
                DealType = reader.IsDBNull(10) ? null : reader.GetString(10),
                DealEndDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                IsAvailable = reader.GetBoolean(12),
                LastChecked = reader.GetDateTime(13)
            });
        }

        return comparisons;
    }
}
