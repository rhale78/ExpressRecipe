using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IHealthGoalRepository
{
    Task<List<HealthGoalDto>> GetAllAsync();
    Task<HealthGoalDto?> GetByIdAsync(Guid id);
    Task<List<HealthGoalDto>> GetByCategoryAsync(string category);
    Task<List<HealthGoalDto>> SearchByNameAsync(string name);
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
            WHERE Id = @Id";

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
            WHERE Category = @Category
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
            WHERE Name LIKE @SearchTerm
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
}
