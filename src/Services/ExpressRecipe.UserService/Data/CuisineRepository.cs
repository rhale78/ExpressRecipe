using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface ICuisineRepository
{
    Task<List<CuisineDto>> GetAllAsync();
    Task<CuisineDto?> GetByIdAsync(Guid id);
    Task<List<CuisineDto>> SearchByNameAsync(string name);
}

public class CuisineRepository : SqlHelper, ICuisineRepository
{
    public CuisineRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<CuisineDto>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, Name, Description
            FROM Cuisine
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new CuisineDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description")
            });
    }

    public async Task<CuisineDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Description
            FROM Cuisine
            WHERE Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new CuisineDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<List<CuisineDto>> SearchByNameAsync(string name)
    {
        const string sql = @"
            SELECT Id, Name, Description
            FROM Cuisine
            WHERE Name LIKE @SearchTerm
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new CuisineDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description")
            },
            CreateParameter("@SearchTerm", $"%{name}%"));
    }
}
