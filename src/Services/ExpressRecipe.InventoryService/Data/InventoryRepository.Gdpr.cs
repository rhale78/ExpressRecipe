using Microsoft.Data.SqlClient;

namespace ExpressRecipe.InventoryService.Data;

public partial class InventoryRepository
{
    /// <summary>
    /// Hard-deletes all inventory data belonging to the user (GDPR Article 17).
    /// </summary>
    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM ExpirationAlert   WHERE UserId = @UserId;
DELETE FROM UsagePrediction   WHERE UserId = @UserId;
DELETE FROM InventoryHistory  WHERE UserId = @UserId;
DELETE FROM InventoryItem     WHERE UserId = @UserId;
DELETE FROM StorageLocation   WHERE UserId = @UserId;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.ExecuteNonQueryAsync(ct);
    }
}
