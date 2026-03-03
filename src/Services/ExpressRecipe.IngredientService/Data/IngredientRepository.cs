using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ExpressRecipe.IngredientService.Data;

public interface IIngredientRepository
{
    Task<IngredientDto?> GetIngredientByIdAsync(Guid id);
    Task<IngredientDto?> GetIngredientByNameAsync(string name);
    Task<List<IngredientDto>> GetAllIngredientsAsync(int limit = 100, int offset = 0);
    Task<Guid> CreateIngredientAsync(CreateIngredientRequest request, Guid? createdBy = null);
    Task<bool> UpdateIngredientAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null);
    Task<bool> DeleteIngredientAsync(Guid id, Guid? deletedBy = null);
    
    // High-speed bulk operations
    Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names);
    Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null);
}

public class IngredientRepository : SqlHelper, IIngredientRepository
{
    private const int MaxIngredientNameLength = 200;
    private readonly ILogger<IngredientRepository>? _logger;

    public IngredientRepository(string connectionString, ILogger<IngredientRepository>? logger = null) : base(connectionString)
    {
        _logger = logger;
    }

    public async Task<IngredientDto?> GetIngredientByIdAsync(Guid id)
    {
        const string sql = "SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString FROM Ingredient WHERE Id = @Id AND IsDeleted = 0";
        var results = await ExecuteReaderAsync(sql, MapIngredient, new SqlParameter("@Id", id));
        return results.FirstOrDefault();
    }

    public async Task<IngredientDto?> GetIngredientByNameAsync(string name)
    {
        const string sql = "SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString FROM Ingredient WHERE Name = @Name AND IsDeleted = 0";
        var results = await ExecuteReaderAsync(sql, MapIngredient, new SqlParameter("@Name", name));
        return results.FirstOrDefault();
    }

    public async Task<List<IngredientDto>> GetAllIngredientsAsync(int limit = 100, int offset = 0)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString 
            FROM Ingredient 
            WHERE IsDeleted = 0 
            ORDER BY Name
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";
        
        return await ExecuteReaderAsync(sql, MapIngredient, 
            new SqlParameter("@Limit", limit),
            new SqlParameter("@Offset", offset));
    }

    public async Task<Guid> CreateIngredientAsync(CreateIngredientRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO Ingredient (Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString, CreatedBy, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Id, @Name, @AltNames, @Desc, @Category, @Allergen, @IngList, @CreatedBy, GETUTCDATE())";

        var safeName = (request.Name ?? string.Empty).Trim();
        if (safeName.Length > MaxIngredientNameLength)
        {
            safeName = safeName[..MaxIngredientNameLength];
        }

        return await ExecuteScalarAsync<Guid>(sql,
            new SqlParameter("@Id", Guid.NewGuid()),
            new SqlParameter("@Name", safeName),
            new SqlParameter("@AltNames", (object?)request.AlternativeNames ?? DBNull.Value),
            new SqlParameter("@Desc", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@Category", (object?)request.Category ?? DBNull.Value),
            new SqlParameter("@Allergen", request.IsCommonAllergen),
            new SqlParameter("@IngList", (object?)request.IngredientListString ?? DBNull.Value),
            new SqlParameter("@CreatedBy", (object?)createdBy ?? DBNull.Value));
    }

    public async Task<bool> UpdateIngredientAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE Ingredient
            SET Name = @Name, AlternativeNames = @AltNames, Description = @Desc, 
                Category = @Category, IsCommonAllergen = @Allergen, IngredientListString = @IngList,
                UpdatedBy = @UpdatedBy, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var safeName = (request.Name ?? string.Empty).Trim();
        if (safeName.Length > MaxIngredientNameLength)
        {
            safeName = safeName[..MaxIngredientNameLength];
        }

        var rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Name", safeName),
            new SqlParameter("@AltNames", (object?)request.AlternativeNames ?? DBNull.Value),
            new SqlParameter("@Desc", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@Category", (object?)request.Category ?? DBNull.Value),
            new SqlParameter("@Allergen", request.IsCommonAllergen),
            new SqlParameter("@IngList", (object?)request.IngredientListString ?? DBNull.Value),
            new SqlParameter("@UpdatedBy", (object?)updatedBy ?? DBNull.Value));

        return rows > 0;
    }

    public async Task<bool> DeleteIngredientAsync(Guid id, Guid? deletedBy = null)
    {
        const string sql = "UPDATE Ingredient SET IsDeleted = 1, DeletedAt = GETUTCDATE(), UpdatedBy = @DeletedBy WHERE Id = @Id";
        var rows = await ExecuteNonQueryAsync(sql, new SqlParameter("@Id", id), new SqlParameter("@DeletedBy", (object?)deletedBy ?? DBNull.Value));
        return rows > 0;
    }

    public async Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names)
    {
        var nameList = NormalizeIngredientNames(names);
        if (!nameList.Any()) return new Dictionary<string, Guid>();

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        await ExecuteTransactionAsync(async (connection, transaction) =>
        {
            using (var cmd = new SqlCommand("DROP TABLE IF EXISTS #CheckNames; CREATE TABLE #CheckNames (Name NVARCHAR(200))", connection, transaction))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#CheckNames";
                var dt = new DataTable();
                dt.Columns.Add("Name", typeof(string));
                foreach (var name in nameList) dt.Rows.Add(name);
                await bulkCopy.WriteToServerAsync(dt);
            }

            const string sql = @"
                SELECT DISTINCT i.Name, i.Id 
                FROM Ingredient i
                INNER JOIN #CheckNames c ON i.Name = c.Name
                WHERE i.IsDeleted = 0";

            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result[reader.GetString(0)] = reader.GetGuid(1);
                    }
                }
            }
        });

        return result;
    }

    public async Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null)
    {
        var namesList = NormalizeIngredientNames(names);
        if (!namesList.Any()) return 0;

        int createdCount = 0;
        foreach (var batch in namesList.Chunk(100))
        {
            var sourceRows = batch.Select((n, i) => $"SELECT @Name{i} as Name").ToList();
            var parameters = batch.Select((n, i) => new SqlParameter($"@Name{i}", n)).ToList();
            parameters.Add(new SqlParameter("@CreatedBy", (object?)createdBy ?? DBNull.Value));

            var sql = $@"
                MERGE Ingredient WITH (HOLDLOCK) AS target
                USING ({string.Join(" UNION ALL ", sourceRows)}) AS source
                ON (target.Name = source.Name)
                WHEN NOT MATCHED THEN
                    INSERT (Id, Name, Category, CreatedBy, CreatedAt)
                    VALUES (NEWID(), source.Name, 'General', @CreatedBy, GETUTCDATE());";

            createdCount += await ExecuteNonQueryAsync(sql, parameters.ToArray());
        }
        return createdCount;
    }

    private List<string> NormalizeIngredientNames(IEnumerable<string> names)
    {
        var list = new List<string>();
        var skippedTooLong = 0;

        foreach (var raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var name = raw.Trim();
            if (name.Length > MaxIngredientNameLength)
            {
                skippedTooLong++;
                continue;
            }

            list.Add(name);
        }

        if (skippedTooLong > 0)
        {
            _logger?.LogWarning("Skipped {Count} ingredient names longer than {MaxLength} characters", skippedTooLong, MaxIngredientNameLength);
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IngredientDto MapIngredient(SqlDataReader reader)
    {
        return new IngredientDto
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            AlternativeNames = reader.IsDBNull(2) ? null : reader.GetString(2),
            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            Category = reader.IsDBNull(4) ? null : reader.GetString(4),
            IsCommonAllergen = reader.GetBoolean(5),
            IngredientListString = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }
}
