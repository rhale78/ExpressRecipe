using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace ExpressRecipe.UserService.Data;

public interface IHealthGoalRepository
{
    Task<List<HealthGoalDto>> GetAllAsync();
    Task<HealthGoalDto?> GetByIdAsync(Guid id);
    Task<List<HealthGoalDto>> GetByCategoryAsync(string category);
    Task<List<HealthGoalDto>> SearchByNameAsync(string name);
    Task<(List<HealthGoalDto> Items, int Total)> GetPagedAsync(int page, int pageSize, string? category = null);
}

public class HealthGoalRepository : SqlHelper, IHealthGoalRepository
{
    public HealthGoalRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<HealthGoalDto>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, Name, Description, Category
            FROM HealthGoal
            WHERE IsDeleted = 0
            ORDER BY Category, Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new HealthGoalDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category")
            });
    }

    public async Task<HealthGoalDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category
            FROM HealthGoal
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new HealthGoalDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<List<HealthGoalDto>> GetByCategoryAsync(string category)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category
            FROM HealthGoal
            WHERE Category = @Category AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new HealthGoalDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category")
            },
            CreateParameter("@Category", category));
    }

    public async Task<List<HealthGoalDto>> SearchByNameAsync(string name)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category
            FROM HealthGoal
            WHERE Name LIKE @SearchTerm AND IsDeleted = 0
            ORDER BY Category, Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new HealthGoalDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category")
            },
            CreateParameter("@SearchTerm", $"%{name}%"));
    }

    public async Task<(List<HealthGoalDto> Items, int Total)> GetPagedAsync(int page, int pageSize, string? category = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        int offset = (page - 1) * pageSize;
        string whereClause = "WHERE IsDeleted = 0" + (category != null ? " AND Category = @Category" : "");

        string countSql = $"SELECT COUNT(*) FROM HealthGoal {whereClause}";
        string sql = $@"
            SELECT Id, Name, Description, Category
            FROM HealthGoal
            {whereClause}
            ORDER BY Category, Name
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var countParams = category != null
            ? new[] { CreateParameter("@Category", category) }
            : Array.Empty<DbParameter>();

        int total = await ExecuteScalarAsync<int>(countSql, countParams);

        var dataParams = new List<DbParameter>
        {
            CreateParameter("@Offset", offset),
            CreateParameter("@PageSize", pageSize)
        };
        if (category != null) dataParams.Insert(0, CreateParameter("@Category", category));

        var items = await ExecuteReaderAsync(
            sql,
            reader => new HealthGoalDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category")
            },
            dataParams.ToArray());

        return (items, total);
    }
}
