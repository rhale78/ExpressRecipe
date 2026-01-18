using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ExpressRecipe.ProductService.Entities;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Adapter that implements IIngredientRepository and delegates simple CRUD to generated HighSpeedDAL DAL.
/// Uses raw ADO.NET for batch and join operations where necessary.
/// </summary>
public class IngredientRepositoryAdapter : IIngredientRepository
{
    private readonly IngredientEntityDal _dal;
    private readonly ProductDatabaseConnection _dbConnection;
    private readonly ILogger<IngredientRepositoryAdapter> _logger;

    public IngredientRepositoryAdapter(IngredientEntityDal dal, ProductDatabaseConnection dbConnection, ILogger<IngredientRepositoryAdapter> logger)
    {
        _dal = dal ?? throw new ArgumentNullException(nameof(dal));
        _dbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<IngredientDto>> GetAllAsync()
    {
        var entities = await _dal.GetAllAsync();
        return entities.Select(MapEntityToDto).ToList();
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id)
    {
        var e = await _dal.GetByIdAsync(id);
        return e is null ? null : MapEntityToDto(e);
    }

    public async Task<List<IngredientDto>> SearchByNameAsync(string searchTerm)
    {
        const string sql = @"SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen FROM Ingredient WHERE (Name LIKE @SearchTerm OR AlternativeNames LIKE @SearchTerm) AND IsDeleted = 0 ORDER BY Name";
        var list = new List<IngredientDto>();
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");
        cmd.CommandTimeout = 120;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new IngredientDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                AlternativeNames = reader.IsDBNull(reader.GetOrdinal("AlternativeNames")) ? null : reader.GetString(reader.GetOrdinal("AlternativeNames")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                IsCommonAllergen = reader.IsDBNull(reader.GetOrdinal("IsCommonAllergen")) ? false : reader.GetBoolean(reader.GetOrdinal("IsCommonAllergen"))
            });
        }

        return list;
    }

    public async Task<List<IngredientDto>> GetByCategoryAsync(string category)
    {
        const string sql = @"SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen FROM Ingredient WHERE Category = @Category AND IsDeleted = 0 ORDER BY Name";
        var list = new List<IngredientDto>();
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);
        cmd.CommandTimeout = 120;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new IngredientDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                AlternativeNames = reader.IsDBNull(reader.GetOrdinal("AlternativeNames")) ? null : reader.GetString(reader.GetOrdinal("AlternativeNames")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                IsCommonAllergen = reader.IsDBNull(reader.GetOrdinal("IsCommonAllergen")) ? false : reader.GetBoolean(reader.GetOrdinal("IsCommonAllergen"))
            });
        }

        return list;
    }

    public async Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null)
    {
        var entity = new IngredientEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            IsCommonAllergen = request.IsCommonAllergen,
            // Note: CreatedDate/ModifiedDate are auto-populated by HighSpeedDAL's InsertAsync
            IsDeleted = false
        };
        // Generated DAL InsertAsync requires userName and CancellationToken parameters
        await _dal.InsertAsync(entity, "System", System.Threading.CancellationToken.None);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null)
    {
        var existing = await _dal.GetByIdAsync(id);
        if (existing == null) return false;
        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Category = request.Category;
        existing.IsCommonAllergen = request.IsCommonAllergen;
        existing.ModifiedDate = DateTime.UtcNow;
        // Generated DAL UpdateAsync requires userName and CancellationToken parameters
        await _dal.UpdateAsync(existing, null, System.Threading.CancellationToken.None);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        // If generated DAL doesn't expose a SoftDelete, perform soft-delete via entity update
        var existing = await _dal.GetByIdAsync(id);
        if (existing == null) return false;
        existing.IsDeleted = true;
        existing.ModifiedDate = DateTime.UtcNow;
        await _dal.UpdateAsync(existing, null, System.Threading.CancellationToken.None);
        return true;
    }

    public async Task<List<ProductIngredientDto>> GetProductIngredientsAsync(Guid productId)
    {
        const string sql = @"SELECT pi.Id, pi.ProductId, pi.IngredientId, i.Name as IngredientName, pi.OrderIndex, pi.Quantity, pi.Notes, pi.IngredientListString FROM ProductIngredient pi INNER JOIN Ingredient i ON pi.IngredientId = i.Id WHERE pi.ProductId = @ProductId AND pi.IsDeleted = 0 AND i.IsDeleted = 0 ORDER BY pi.OrderIndex, i.Name";
        var list = new List<ProductIngredientDto>();
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.CommandTimeout = 120;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ProductIngredientDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                ProductId = reader.GetGuid(reader.GetOrdinal("ProductId")),
                IngredientId = reader.GetGuid(reader.GetOrdinal("IngredientId")),
                IngredientName = reader.GetString(reader.GetOrdinal("IngredientName")),
                OrderIndex = reader.GetInt32(reader.GetOrdinal("OrderIndex")),
                Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? null : reader.GetString(reader.GetOrdinal("Quantity")),
                Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                IngredientListString = reader.IsDBNull(reader.GetOrdinal("IngredientListString")) ? null : reader.GetString(reader.GetOrdinal("IngredientListString"))
            });
        }

        return list;
    }

    public async Task<Guid> AddProductIngredientAsync(Guid productId, AddProductIngredientRequest request, Guid? createdBy = null)
    {
        const string checkSql = "SELECT Id FROM ProductIngredient WHERE ProductId = @ProductId AND IngredientId = @IngredientId AND IsDeleted = 0";
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@ProductId", productId);
            checkCmd.Parameters.AddWithValue("@IngredientId", request.IngredientId);
            var existing = await checkCmd.ExecuteScalarAsync();
            if (existing != null && existing != DBNull.Value) return (Guid)existing;
        }

        const string insertSql = "INSERT INTO ProductIngredient (Id, ProductId, IngredientId, OrderIndex, Quantity, Notes, IngredientListString, CreatedDate) VALUES (@Id,@ProductId,@IngredientId,@OrderIndex,@Quantity,@Notes,@IngredientListString,GETUTCDATE())";
        var newId = Guid.NewGuid();
        await using var insertConn = new SqlConnection(_dbConnection.ConnectionString);
        await insertConn.OpenAsync();
        try
        {
            await using var insertCmd = new SqlCommand(insertSql, insertConn);
            insertCmd.Parameters.AddWithValue("@Id", newId);
            insertCmd.Parameters.AddWithValue("@ProductId", productId);
            insertCmd.Parameters.AddWithValue("@IngredientId", request.IngredientId);
            insertCmd.Parameters.AddWithValue("@OrderIndex", request.OrderIndex);
            insertCmd.Parameters.AddWithValue("@Quantity", (object?)request.Quantity ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@Notes", (object?)request.Notes ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@IngredientListString", (object?)request.IngredientListString ?? DBNull.Value);
            insertCmd.CommandTimeout = 120;
            await insertCmd.ExecuteNonQueryAsync();
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            await using var checkCmd2 = new SqlCommand(checkSql, insertConn);
            checkCmd2.Parameters.AddWithValue("@ProductId", productId);
            checkCmd2.Parameters.AddWithValue("@IngredientId", request.IngredientId);
            var existing = await checkCmd2.ExecuteScalarAsync();
            if (existing != null && existing != DBNull.Value) return (Guid)existing;
            throw;
        }

        return newId;
    }

    public async Task<bool> RemoveProductIngredientAsync(Guid productIngredientId, Guid? deletedBy = null)
    {
        const string sql = "UPDATE ProductIngredient SET IsDeleted = 1, DeletedDate = GETUTCDATE(), ModifiedDate = GETUTCDATE() WHERE Id = @Id AND IsDeleted = 0";
        await using var conn = new SqlConnection(_dbConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", productIngredientId);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names)
    {
        var namesList = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!namesList.Any()) return new Dictionary<string, Guid>();
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in namesList.Chunk(1000))
        {
            var parameters = new List<SqlParameter>();
            var conditions = new List<string>();
            for (int i = 0; i < chunk.Length; i++)
            {
                var paramName = $"@p{i}";
                conditions.Add($"Name = {paramName}");
                parameters.Add(new SqlParameter(paramName, chunk[i]));
            }
            // conditions should be combined with OR to match any of the provided names
            var sql = $"SELECT Name, Id FROM Ingredient WHERE ({string.Join(" OR ", conditions)}) AND IsDeleted = 0";
            await using var conn = new SqlConnection(_dbConnection.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters.ToArray());
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(reader.GetOrdinal("Name"));
                var id = reader.GetGuid(reader.GetOrdinal("Id"));
                if (!result.ContainsKey(name)) result[name] = id;
            }
        }
        return result;
    }

    public async Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null)
    {
        var namesList = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!namesList.Any()) return 0;

        // Create entities from names
        var entities = namesList.Select(name => new IngredientEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Category = "General",
            IsCommonAllergen = false
        }).ToList();

        try
        {
            // Uses generated DAL BulkInsertAsync which leverages InMemoryTable for high-speed writes
            return await _dal.BulkInsertAsync(entities, null, System.Threading.CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Some duplicates during BulkCreateIngredientsAsync - this is expected");
            return 0; // Duplicates are expected, caller handles them
        }
    }

    private static IngredientDto MapEntityToDto(IngredientEntity e)
    {
        return new IngredientDto
        {
            Id = e.Id,
            Name = e.Name ?? string.Empty,
            Description = e.Description,
            Category = e.Category,
            IsCommonAllergen = e.IsCommonAllergen,
            IngredientListString = null
        };
    }
}
