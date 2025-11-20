using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Data;

public interface IBaseIngredientRepository
{
    Task<List<BaseIngredientDto>> SearchAsync(BaseIngredientSearchRequest request);
    Task<BaseIngredientDto?> GetByIdAsync(Guid id);
    Task<BaseIngredientDto?> FindByNameAsync(string name);
    Task<List<BaseIngredientDto>> GetByCategoryAsync(string category);
    Task<List<BaseIngredientDto>> GetAllergensAsync();
    Task<List<BaseIngredientDto>> GetAdditivesAsync();
    Task<Guid> CreateAsync(CreateBaseIngredientRequest request, Guid createdBy);
    Task<bool> UpdateAsync(Guid id, UpdateBaseIngredientRequest request, Guid updatedBy);
    Task<bool> DeleteAsync(Guid id, Guid deletedBy);
    Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null);
    Task<bool> BaseIngredientExistsAsync(Guid id);

    // Ingredient Base Component management
    Task<List<IngredientBaseComponentDto>> GetIngredientBaseComponentsAsync(Guid ingredientId);
    Task<Guid> AddIngredientBaseComponentAsync(Guid ingredientId, AddIngredientBaseComponentRequest request, Guid createdBy);
    Task<bool> UpdateIngredientBaseComponentAsync(Guid componentId, UpdateIngredientBaseComponentRequest request, Guid updatedBy);
    Task<bool> RemoveIngredientBaseComponentAsync(Guid componentId, Guid deletedBy);
}

public class BaseIngredientRepository : SqlHelper, IBaseIngredientRepository
{
    public BaseIngredientRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<BaseIngredientDto>> SearchAsync(BaseIngredientSearchRequest request)
    {
        var whereClauses = new List<string> { "IsDeleted = 0" };
        var parameters = new List<SqlParameter>();

        if (request.OnlyApproved)
        {
            whereClauses.Add("IsApproved = 1");
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            whereClauses.Add("(Name LIKE @SearchTerm OR CommonNames LIKE @SearchTerm OR Description LIKE @SearchTerm)");
            parameters.Add(CreateParameter("@SearchTerm", $"%{request.SearchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            whereClauses.Add("Category = @Category");
            parameters.Add(CreateParameter("@Category", request.Category));
        }

        if (request.IsAllergen.HasValue)
        {
            whereClauses.Add("IsAllergen = @IsAllergen");
            parameters.Add(CreateParameter("@IsAllergen", request.IsAllergen.Value));
        }

        if (request.IsAdditive.HasValue)
        {
            whereClauses.Add("IsAdditive = @IsAdditive");
            parameters.Add(CreateParameter("@IsAdditive", request.IsAdditive.Value));
        }

        var sql = $@"
            SELECT Id, Name, ScientificName, Category, Description, Purpose,
                   CommonNames, IsAllergen, AllergenType, IsAdditive, AdditiveCode,
                   NutritionalHighlights, IsApproved, ApprovedBy, ApprovedAt,
                   RejectionReason, SubmittedBy, CreatedAt, UpdatedAt
            FROM BaseIngredient
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY Name
            OFFSET {(request.PageNumber - 1) * request.PageSize} ROWS
            FETCH NEXT {request.PageSize} ROWS ONLY";

        return await ExecuteReaderAsync(
            sql,
            reader => MapBaseIngredientDto(reader),
            parameters.ToArray());
    }

    public async Task<BaseIngredientDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, ScientificName, Category, Description, Purpose,
                   CommonNames, IsAllergen, AllergenType, IsAdditive, AdditiveCode,
                   NutritionalHighlights, IsApproved, ApprovedBy, ApprovedAt,
                   RejectionReason, SubmittedBy, CreatedAt, UpdatedAt
            FROM BaseIngredient
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => MapBaseIngredientDto(reader),
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<BaseIngredientDto?> FindByNameAsync(string name)
    {
        const string sql = @"
            SELECT Id, Name, ScientificName, Category, Description, Purpose,
                   CommonNames, IsAllergen, AllergenType, IsAdditive, AdditiveCode,
                   NutritionalHighlights, IsApproved, ApprovedBy, ApprovedAt,
                   RejectionReason, SubmittedBy, CreatedAt, UpdatedAt
            FROM BaseIngredient
            WHERE Name = @Name AND IsDeleted = 0 AND IsApproved = 1";

        var results = await ExecuteReaderAsync(
            sql,
            reader => MapBaseIngredientDto(reader),
            CreateParameter("@Name", name));

        return results.FirstOrDefault();
    }

    public async Task<List<BaseIngredientDto>> GetByCategoryAsync(string category)
    {
        const string sql = @"
            SELECT Id, Name, ScientificName, Category, Description, Purpose,
                   CommonNames, IsAllergen, AllergenType, IsAdditive, AdditiveCode,
                   NutritionalHighlights, IsApproved, ApprovedBy, ApprovedAt,
                   RejectionReason, SubmittedBy, CreatedAt, UpdatedAt
            FROM BaseIngredient
            WHERE Category = @Category AND IsDeleted = 0 AND IsApproved = 1
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => MapBaseIngredientDto(reader),
            CreateParameter("@Category", category));
    }

    public async Task<List<BaseIngredientDto>> GetAllergensAsync()
    {
        const string sql = @"
            SELECT Id, Name, ScientificName, Category, Description, Purpose,
                   CommonNames, IsAllergen, AllergenType, IsAdditive, AdditiveCode,
                   NutritionalHighlights, IsApproved, ApprovedBy, ApprovedAt,
                   RejectionReason, SubmittedBy, CreatedAt, UpdatedAt
            FROM BaseIngredient
            WHERE IsAllergen = 1 AND IsDeleted = 0 AND IsApproved = 1
            ORDER BY AllergenType, Name";

        return await ExecuteReaderAsync(
            sql,
            reader => MapBaseIngredientDto(reader));
    }

    public async Task<List<BaseIngredientDto>> GetAdditivesAsync()
    {
        const string sql = @"
            SELECT Id, Name, ScientificName, Category, Description, Purpose,
                   CommonNames, IsAllergen, AllergenType, IsAdditive, AdditiveCode,
                   NutritionalHighlights, IsApproved, ApprovedBy, ApprovedAt,
                   RejectionReason, SubmittedBy, CreatedAt, UpdatedAt
            FROM BaseIngredient
            WHERE IsAdditive = 1 AND IsDeleted = 0 AND IsApproved = 1
            ORDER BY AdditiveCode, Name";

        return await ExecuteReaderAsync(
            sql,
            reader => MapBaseIngredientDto(reader));
    }

    public async Task<Guid> CreateAsync(CreateBaseIngredientRequest request, Guid createdBy)
    {
        const string sql = @"
            INSERT INTO BaseIngredient (
                Id, Name, ScientificName, Category, Description, Purpose,
                CommonNames, IsAllergen, AllergenType, IsAdditive, AdditiveCode,
                NutritionalHighlights, SubmittedBy, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @Name, @ScientificName, @Category, @Description, @Purpose,
                @CommonNames, @IsAllergen, @AllergenType, @IsAdditive, @AdditiveCode,
                @NutritionalHighlights, @SubmittedBy, @CreatedBy, GETUTCDATE()
            )";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@ScientificName", request.ScientificName),
            CreateParameter("@Category", request.Category),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Purpose", request.Purpose),
            CreateParameter("@CommonNames", request.CommonNames),
            CreateParameter("@IsAllergen", request.IsAllergen),
            CreateParameter("@AllergenType", request.AllergenType),
            CreateParameter("@IsAdditive", request.IsAdditive),
            CreateParameter("@AdditiveCode", request.AdditiveCode),
            CreateParameter("@NutritionalHighlights", request.NutritionalHighlights),
            CreateParameter("@SubmittedBy", createdBy),
            CreateParameter("@CreatedBy", createdBy));

        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateBaseIngredientRequest request, Guid updatedBy)
    {
        const string sql = @"
            UPDATE BaseIngredient
            SET Name = @Name,
                ScientificName = @ScientificName,
                Category = @Category,
                Description = @Description,
                Purpose = @Purpose,
                CommonNames = @CommonNames,
                IsAllergen = @IsAllergen,
                AllergenType = @AllergenType,
                IsAdditive = @IsAdditive,
                AdditiveCode = @AdditiveCode,
                NutritionalHighlights = @NutritionalHighlights,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@ScientificName", request.ScientificName),
            CreateParameter("@Category", request.Category),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Purpose", request.Purpose),
            CreateParameter("@CommonNames", request.CommonNames),
            CreateParameter("@IsAllergen", request.IsAllergen),
            CreateParameter("@AllergenType", request.AllergenType),
            CreateParameter("@IsAdditive", request.IsAdditive),
            CreateParameter("@AdditiveCode", request.AdditiveCode),
            CreateParameter("@NutritionalHighlights", request.NutritionalHighlights),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deletedBy)
    {
        const string sql = @"
            UPDATE BaseIngredient
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null)
    {
        const string sql = @"
            UPDATE BaseIngredient
            SET IsApproved = @IsApproved,
                ApprovedBy = @ApprovedBy,
                ApprovedAt = GETUTCDATE(),
                RejectionReason = @RejectionReason,
                UpdatedBy = @ApprovedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@IsApproved", approve),
            CreateParameter("@ApprovedBy", approvedBy),
            CreateParameter("@RejectionReason", approve ? null : rejectionReason));

        return rowsAffected > 0;
    }

    public async Task<bool> BaseIngredientExistsAsync(Guid id)
    {
        const string sql = "SELECT COUNT(*) FROM BaseIngredient WHERE Id = @Id AND IsDeleted = 0";

        var count = await ExecuteScalarAsync<int>(
            sql,
            CreateParameter("@Id", id));

        return count > 0;
    }

    #region Ingredient Base Component Management

    public async Task<List<IngredientBaseComponentDto>> GetIngredientBaseComponentsAsync(Guid ingredientId)
    {
        const string sql = @"
            SELECT ibc.Id, ibc.IngredientId, ibc.BaseIngredientId, ibc.OrderIndex,
                   ibc.Percentage, ibc.IsMainComponent, ibc.Notes,
                   bi.Name AS BaseIngredientName, bi.Category AS BaseIngredientCategory
            FROM IngredientBaseComponent ibc
            INNER JOIN BaseIngredient bi ON ibc.BaseIngredientId = bi.Id
            WHERE ibc.IngredientId = @IngredientId AND ibc.IsDeleted = 0
            ORDER BY ibc.OrderIndex";

        return await ExecuteReaderAsync(
            sql,
            reader => new IngredientBaseComponentDto
            {
                Id = GetGuid(reader, "Id"),
                IngredientId = GetGuid(reader, "IngredientId"),
                BaseIngredientId = GetGuid(reader, "BaseIngredientId"),
                BaseIngredientName = GetString(reader, "BaseIngredientName") ?? string.Empty,
                BaseIngredientCategory = GetString(reader, "BaseIngredientCategory"),
                OrderIndex = GetInt(reader, "OrderIndex") ?? 0,
                Percentage = GetDecimal(reader, "Percentage"),
                IsMainComponent = GetBool(reader, "IsMainComponent") ?? false,
                Notes = GetString(reader, "Notes")
            },
            CreateParameter("@IngredientId", ingredientId));
    }

    public async Task<Guid> AddIngredientBaseComponentAsync(Guid ingredientId, AddIngredientBaseComponentRequest request, Guid createdBy)
    {
        const string sql = @"
            INSERT INTO IngredientBaseComponent (
                Id, IngredientId, BaseIngredientId, OrderIndex, Percentage,
                IsMainComponent, Notes, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @IngredientId, @BaseIngredientId, @OrderIndex, @Percentage,
                @IsMainComponent, @Notes, @CreatedBy, GETUTCDATE()
            )";

        var id = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@IngredientId", ingredientId),
            CreateParameter("@BaseIngredientId", request.BaseIngredientId),
            CreateParameter("@OrderIndex", request.OrderIndex),
            CreateParameter("@Percentage", request.Percentage),
            CreateParameter("@IsMainComponent", request.IsMainComponent),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@CreatedBy", createdBy));

        return id;
    }

    public async Task<bool> UpdateIngredientBaseComponentAsync(Guid componentId, UpdateIngredientBaseComponentRequest request, Guid updatedBy)
    {
        const string sql = @"
            UPDATE IngredientBaseComponent
            SET OrderIndex = @OrderIndex,
                Percentage = @Percentage,
                IsMainComponent = @IsMainComponent,
                Notes = @Notes,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", componentId),
            CreateParameter("@OrderIndex", request.OrderIndex),
            CreateParameter("@Percentage", request.Percentage),
            CreateParameter("@IsMainComponent", request.IsMainComponent),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> RemoveIngredientBaseComponentAsync(Guid componentId, Guid deletedBy)
    {
        const string sql = @"
            UPDATE IngredientBaseComponent
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", componentId),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    #endregion

    private BaseIngredientDto MapBaseIngredientDto(IDataReader reader)
    {
        return new BaseIngredientDto
        {
            Id = GetGuid(reader, "Id"),
            Name = GetString(reader, "Name") ?? string.Empty,
            ScientificName = GetString(reader, "ScientificName"),
            Category = GetString(reader, "Category"),
            Description = GetString(reader, "Description"),
            Purpose = GetString(reader, "Purpose"),
            CommonNames = GetString(reader, "CommonNames"),
            IsAllergen = GetBool(reader, "IsAllergen") ?? false,
            AllergenType = GetString(reader, "AllergenType"),
            IsAdditive = GetBool(reader, "IsAdditive") ?? false,
            AdditiveCode = GetString(reader, "AdditiveCode"),
            NutritionalHighlights = GetString(reader, "NutritionalHighlights"),
            IsApproved = GetBool(reader, "IsApproved") ?? false,
            ApprovedBy = GetGuid(reader, "ApprovedBy"),
            ApprovedAt = GetDateTime(reader, "ApprovedAt"),
            RejectionReason = GetString(reader, "RejectionReason"),
            SubmittedBy = GetGuid(reader, "SubmittedBy"),
            CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
            UpdatedAt = GetDateTime(reader, "UpdatedAt")
        };
    }
}
