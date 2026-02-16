using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for template management
public partial class ShoppingRepository
{
    public async Task<Guid> CreateTemplateAsync(Guid userId, Guid? householdId, string name, string? description, string? category)
    {
        const string sql = @"
            INSERT INTO ShoppingListTemplate (UserId, HouseholdId, Name, Description, Category, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @Name, @Description, @Category, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);

        var templateId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Created template {TemplateId} for user {UserId}", templateId, userId);
        return templateId;
    }

    public async Task<List<ShoppingListTemplateDto>> GetUserTemplatesAsync(Guid userId)
    {
        const string sql = @"
            SELECT 
                t.Id, t.UserId, t.HouseholdId, t.Name, t.Description, t.Category, t.UseCount, t.LastUsed, t.CreatedAt,
                (SELECT COUNT(*) FROM ShoppingListTemplateItem WHERE TemplateId = t.Id) AS ItemCount
            FROM ShoppingListTemplate t
            WHERE t.UserId = @UserId AND t.IsDeleted = 0
            ORDER BY t.UseCount DESC, t.LastUsed DESC, t.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return await ReadTemplatesAsync(command);
    }

    public async Task<List<ShoppingListTemplateDto>> GetHouseholdTemplatesAsync(Guid householdId)
    {
        const string sql = @"
            SELECT 
                t.Id, t.UserId, t.HouseholdId, t.Name, t.Description, t.Category, t.UseCount, t.LastUsed, t.CreatedAt,
                (SELECT COUNT(*) FROM ShoppingListTemplateItem WHERE TemplateId = t.Id) AS ItemCount
            FROM ShoppingListTemplate t
            WHERE t.HouseholdId = @HouseholdId AND t.IsDeleted = 0
            ORDER BY t.UseCount DESC, t.LastUsed DESC, t.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

        return await ReadTemplatesAsync(command);
    }

    public async Task<Guid> AddItemToTemplateAsync(Guid templateId, Guid? productId, string? customName, decimal quantity, string? unit, string? category)
    {
        const string sql = @"
            INSERT INTO ShoppingListTemplateItem (TemplateId, ProductId, CustomName, Quantity, Unit, Category, OrderIndex)
            OUTPUT INSERTED.Id
            VALUES (@TemplateId, @ProductId, @CustomName, @Quantity, @Unit, @Category, 
                    (SELECT ISNULL(MAX(OrderIndex), 0) + 1 FROM ShoppingListTemplateItem WHERE TemplateId = @TemplateId))";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TemplateId", templateId);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.Parameters.AddWithValue("@Unit", unit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<TemplateItemDto>> GetTemplateItemsAsync(Guid templateId)
    {
        const string sql = @"
            SELECT 
                ti.Id, ti.TemplateId, ti.ProductId, ti.CustomName, ti.Quantity, ti.Unit, ti.Category, ti.OrderIndex,
                p.Name AS ProductName
            FROM ShoppingListTemplateItem ti
            LEFT JOIN ExpressRecipe.Products.Product p ON ti.ProductId = p.Id
            WHERE ti.TemplateId = @TemplateId
            ORDER BY ti.OrderIndex, ti.Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TemplateId", templateId);

        var items = new List<TemplateItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new TemplateItemDto
            {
                Id = reader.GetGuid(0),
                TemplateId = reader.GetGuid(1),
                ProductId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                CustomName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Quantity = reader.GetDecimal(4),
                Unit = reader.IsDBNull(5) ? null : reader.GetString(5),
                Category = reader.IsDBNull(6) ? null : reader.GetString(6),
                OrderIndex = reader.GetInt32(7),
                ProductName = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return items;
    }

    public async Task<Guid> CreateListFromTemplateAsync(Guid templateId, Guid userId, string listName)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        try
        {
            // Get template info
            const string getTemplateSql = "SELECT UserId, HouseholdId, Name, Description FROM ShoppingListTemplate WHERE Id = @TemplateId";
            Guid? householdId = null;
            string? description = null;

            await using (var command = new SqlCommand(getTemplateSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@TemplateId", templateId);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    householdId = reader.IsDBNull(1) ? null : reader.GetGuid(1);
                    description = $"Created from template: {reader.GetString(2)}";
                }
            }

            // Create new list
            const string createListSql = @"
                INSERT INTO ShoppingList (UserId, HouseholdId, Name, Description, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES (@UserId, @HouseholdId, @Name, @Description, GETUTCDATE())";

            Guid listId;
            await using (var command = new SqlCommand(createListSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@Name", listName);
                command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);

                listId = (Guid)await command.ExecuteScalarAsync()!;
            }

            // Copy template items to list
            const string copyItemsSql = @"
                INSERT INTO ShoppingListItem (ShoppingListId, ProductId, CustomName, Quantity, Unit, Category, OrderIndex)
                SELECT @ListId, ProductId, CustomName, Quantity, Unit, Category, OrderIndex
                FROM ShoppingListTemplateItem
                WHERE TemplateId = @TemplateId";

            await using (var command = new SqlCommand(copyItemsSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@ListId", listId);
                command.Parameters.AddWithValue("@TemplateId", templateId);
                await command.ExecuteNonQueryAsync();
            }

            // Update template usage
            await UpdateTemplateUsageInternalAsync(templateId, connection, transaction);

            await transaction.CommitAsync();
            _logger.LogInformation("Created list {ListId} from template {TemplateId}", listId, templateId);
            return listId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateTemplateUsageAsync(Guid templateId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await UpdateTemplateUsageInternalAsync(templateId, connection, null);
    }

    private async Task UpdateTemplateUsageInternalAsync(Guid templateId, SqlConnection connection, SqlTransaction? transaction)
    {
        const string sql = @"
            UPDATE ShoppingListTemplate
            SET UseCount = UseCount + 1, LastUsed = GETUTCDATE()
            WHERE Id = @TemplateId";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@TemplateId", templateId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteTemplateAsync(Guid templateId)
    {
        const string sql = "UPDATE ShoppingListTemplate SET IsDeleted = 1 WHERE Id = @TemplateId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TemplateId", templateId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Deleted template {TemplateId}", templateId);
    }

    private async Task<List<ShoppingListTemplateDto>> ReadTemplatesAsync(SqlCommand command)
    {
        var templates = new List<ShoppingListTemplateDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            templates.Add(new ShoppingListTemplateDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                Name = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                Category = reader.IsDBNull(5) ? null : reader.GetString(5),
                UseCount = reader.GetInt32(6),
                LastUsed = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                CreatedAt = reader.GetDateTime(8),
                ItemCount = reader.GetInt32(9)
            });
        }

        return templates;
    }
}
