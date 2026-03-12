using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface ICuisineRepository
{
    Task<List<CuisineDto>> GetAllAsync();
    Task<CuisineDto?> GetByIdAsync(Guid id);
    Task<List<CuisineDto>> SearchByNameAsync(string name);
    Task<(List<CuisineDto> Items, int Total)> GetPagedAsync(int page, int pageSize);
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
            WHERE IsDeleted = 0
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
            WHERE Id = @Id AND IsDeleted = 0";

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
            WHERE Name LIKE @SearchTerm AND IsDeleted = 0
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

    public async Task<(List<CuisineDto> Items, int Total)> GetPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        int offset = (page - 1) * pageSize;

        const string countSql = "SELECT COUNT(*) FROM Cuisine WHERE IsDeleted = 0";
        const string sql = @"
            SELECT Id, Name, Description
            FROM Cuisine
            WHERE IsDeleted = 0
            ORDER BY Name
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        int total = await ExecuteScalarAsync<int>(countSql);

        var items = await ExecuteReaderAsync(
            sql,
            reader => new CuisineDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description")
            },
            CreateParameter("@Offset", offset),
            CreateParameter("@PageSize", pageSize));

        return (items, total);
    }
}
