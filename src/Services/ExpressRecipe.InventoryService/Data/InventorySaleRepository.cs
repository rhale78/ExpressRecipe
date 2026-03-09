using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

/// <summary>
/// Repository for recording and querying inventory sales.
/// </summary>
public class InventorySaleRepository : IInventorySaleRepository
{
    private readonly string _connectionString;
    private readonly ILogger<InventorySaleRepository> _logger;

    public InventorySaleRepository(string connectionString, ILogger<InventorySaleRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Guid> RecordSaleAsync(Guid householdId, Guid? inventoryItemId, string productName,
        decimal quantity, string unit, DateOnly saleDate, string? buyer, string? notes,
        bool autoRemoveOnZero = true)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlTransaction tx = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            // Verify sufficient quantity when selling from a specific inventory item
            if (inventoryItemId.HasValue)
            {
                const string checkSql = @"
                    SELECT Quantity FROM InventoryItem
                    WHERE Id = @ItemId AND IsDeleted = 0";

                await using (SqlCommand checkCmd = new SqlCommand(checkSql, connection, tx))
                {
                    checkCmd.Parameters.AddWithValue("@ItemId", inventoryItemId.Value);
                    object? result = await checkCmd.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                    {
                        throw new KeyNotFoundException($"Inventory item {inventoryItemId.Value} not found.");
                    }

                    decimal currentQty = (decimal)result;
                    if (currentQty < quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient quantity. Available: {currentQty}, requested: {quantity}.");
                    }

                    decimal newQty = currentQty - quantity;

                    if (autoRemoveOnZero && newQty == 0m)
                    {
                        // Soft-delete the inventory item when quantity reaches zero
                        const string deleteSql = @"
                            UPDATE InventoryItem
                            SET Quantity = 0, IsDeleted = 1, UpdatedAt = GETUTCDATE()
                            WHERE Id = @ItemId";

                        await using SqlCommand deleteCmd = new SqlCommand(deleteSql, connection, tx);
                        deleteCmd.Parameters.AddWithValue("@ItemId", inventoryItemId.Value);
                        await deleteCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        const string updateSql = @"
                            UPDATE InventoryItem
                            SET Quantity = @NewQty, UpdatedAt = GETUTCDATE()
                            WHERE Id = @ItemId";

                        await using SqlCommand updateCmd = new SqlCommand(updateSql, connection, tx);
                        updateCmd.Parameters.AddWithValue("@ItemId", inventoryItemId.Value);
                        updateCmd.Parameters.AddWithValue("@NewQty", newQty);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            // Insert sale record
            const string insertSql = @"
                INSERT INTO InventorySale
                    (HouseholdId, InventoryItemId, ProductName, Quantity, Unit, SaleDate, Buyer, Notes, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@HouseholdId, @InventoryItemId, @ProductName, @Quantity, @Unit, @SaleDate, @Buyer, @Notes, GETUTCDATE())";

            await using SqlCommand insertCmd = new SqlCommand(insertSql, connection, tx);
            insertCmd.Parameters.AddWithValue("@HouseholdId", householdId);
            insertCmd.Parameters.AddWithValue("@InventoryItemId",
                inventoryItemId.HasValue ? (object)inventoryItemId.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@ProductName", productName);
            insertCmd.Parameters.AddWithValue("@Quantity", quantity);
            insertCmd.Parameters.AddWithValue("@Unit", unit);
            insertCmd.Parameters.AddWithValue("@SaleDate", saleDate.ToString("yyyy-MM-dd"));
            insertCmd.Parameters.AddWithValue("@Buyer", buyer ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

            Guid saleId = (Guid)(await insertCmd.ExecuteScalarAsync())!;

            await tx.CommitAsync();

            _logger.LogInformation(
                "Recorded sale {SaleId}: {Quantity} {Unit} of {ProductName} to {Buyer}",
                saleId, quantity, unit, productName, buyer ?? "anonymous");

            return saleId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<InventorySaleDto>> GetSalesAsync(Guid householdId, DateOnly from, DateOnly to)
    {
        const string sql = @"
            SELECT Id, HouseholdId, InventoryItemId, ProductName, Quantity, Unit, SaleDate, Buyer, Notes, CreatedAt
            FROM InventorySale
            WHERE HouseholdId = @HouseholdId
              AND SaleDate >= @From
              AND SaleDate <= @To
            ORDER BY SaleDate DESC, CreatedAt DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);
        command.Parameters.AddWithValue("@From", from.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@To", to.ToString("yyyy-MM-dd"));

        return await ReadSalesAsync(command);
    }

    /// <inheritdoc/>
    public async Task<List<InventorySaleDto>> GetSalesByItemAsync(Guid inventoryItemId)
    {
        const string sql = @"
            SELECT Id, HouseholdId, InventoryItemId, ProductName, Quantity, Unit, SaleDate, Buyer, Notes, CreatedAt
            FROM InventorySale
            WHERE InventoryItemId = @ItemId
            ORDER BY SaleDate DESC, CreatedAt DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", inventoryItemId);

        return await ReadSalesAsync(command);
    }

    private static async Task<List<InventorySaleDto>> ReadSalesAsync(SqlCommand command)
    {
        List<InventorySaleDto> results = new List<InventorySaleDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new InventorySaleDto
            {
                Id              = reader.GetGuid(0),
                HouseholdId     = reader.GetGuid(1),
                InventoryItemId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                ProductName     = reader.GetString(3),
                Quantity        = reader.GetDecimal(4),
                Unit            = reader.GetString(5),
                SaleDate        = DateOnly.FromDateTime(reader.GetDateTime(6)),
                Buyer           = reader.IsDBNull(7) ? null : reader.GetString(7),
                Notes           = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAt       = reader.GetDateTime(9),
            });
        }
        return results;
    }
}
