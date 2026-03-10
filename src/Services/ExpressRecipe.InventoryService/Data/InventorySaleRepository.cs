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
            // When selling from a specific inventory item, atomically deduct the quantity.
            // The UPDATE uses UPDLOCK + ROWLOCK hints and a WHERE Quantity >= @Qty predicate to prevent
            // over-selling under concurrent requests. If zero rows are affected, either the item no
            // longer exists, belongs to a different household, or has insufficient stock.
            // InventoryItemId is set to NULL in the sale record when the item is auto-removed at zero
            // so the FK correctly signals "item was fully consumed/removed".
            bool itemWasAutoRemoved = false;

            if (inventoryItemId.HasValue)
            {
                const string updateSql = @"
                    UPDATE InventoryItem WITH (UPDLOCK, ROWLOCK)
                    SET Quantity    = Quantity - @Qty,
                        IsDeleted   = CASE
                                          WHEN @AutoRemoveOnZero = 1 AND (Quantity - @Qty) = 0
                                          THEN 1
                                          ELSE IsDeleted
                                      END,
                        UpdatedAt   = GETUTCDATE()
                    OUTPUT INSERTED.Quantity AS NewQuantity
                    WHERE Id          = @ItemId
                      AND HouseholdId = @HouseholdId
                      AND IsDeleted   = 0
                      AND Quantity   >= @Qty";

                await using (SqlCommand updateCmd = new SqlCommand(updateSql, connection, tx))
                {
                    updateCmd.Parameters.AddWithValue("@ItemId", inventoryItemId.Value);
                    updateCmd.Parameters.AddWithValue("@HouseholdId", householdId);
                    updateCmd.Parameters.AddWithValue("@Qty", quantity);
                    updateCmd.Parameters.AddWithValue("@AutoRemoveOnZero", autoRemoveOnZero ? 1 : 0);

                    object? newQtyResult = await updateCmd.ExecuteScalarAsync();

                    if (newQtyResult == null || newQtyResult == DBNull.Value)
                    {
                        // 0 rows affected — item not found, wrong household, or insufficient qty
                        throw new InvalidOperationException(
                            $"Insufficient quantity or item not found. " +
                            $"Requested: {quantity}. " +
                            "Ensure the item exists, belongs to this household, and has enough stock.");
                    }

                    decimal newQty = (decimal)newQtyResult;
                    itemWasAutoRemoved = autoRemoveOnZero && newQty == 0m;
                }
            }

            // Insert sale record.
            // When the item was just auto-removed (qty → 0 + soft-deleted), store NULL so the FK
            // correctly conveys "item no longer exists" as described in the schema comment.
            Guid? saleInventoryItemId = itemWasAutoRemoved ? null : inventoryItemId;

            const string insertSql = @"
                INSERT INTO InventorySale
                    (HouseholdId, InventoryItemId, ProductName, Quantity, Unit, SaleDate, Buyer, Notes, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@HouseholdId, @InventoryItemId, @ProductName, @Quantity, @Unit, @SaleDate, @Buyer, @Notes, GETUTCDATE())";

            await using SqlCommand insertCmd = new SqlCommand(insertSql, connection, tx);
            insertCmd.Parameters.AddWithValue("@HouseholdId", householdId);
            insertCmd.Parameters.AddWithValue("@InventoryItemId",
                saleInventoryItemId.HasValue ? (object)saleInventoryItemId.Value : DBNull.Value);
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
    public async Task<List<InventorySaleDto>> GetSalesByItemAsync(Guid householdId, Guid inventoryItemId)
    {
        const string sql = @"
            SELECT Id, HouseholdId, InventoryItemId, ProductName, Quantity, Unit, SaleDate, Buyer, Notes, CreatedAt
            FROM InventorySale
            WHERE InventoryItemId = @ItemId
              AND HouseholdId = @HouseholdId
            ORDER BY SaleDate DESC, CreatedAt DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", inventoryItemId);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

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
