using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

public partial class ShoppingRepository
{
    /// <summary>
    /// Hard-deletes all shopping data belonging to the user (GDPR Article 17).
    /// </summary>
    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM ShoppingListItem WHERE ShoppingListId IN (SELECT Id FROM ShoppingList WHERE UserId = @UserId);
DELETE FROM ListShare        WHERE UserId = @UserId OR SharedWithUserId = @UserId;
DELETE FROM ShoppingList     WHERE UserId = @UserId;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.ExecuteNonQueryAsync(ct);
    }
}
