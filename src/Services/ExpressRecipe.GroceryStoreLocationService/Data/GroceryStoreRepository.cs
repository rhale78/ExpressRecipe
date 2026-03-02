using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace ExpressRecipe.GroceryStoreLocationService.Data;

public class GroceryStoreRepository : SqlHelper, IGroceryStoreRepository
{
    private readonly HybridCacheService? _cache;
    private readonly ILogger<GroceryStoreRepository>? _logger;
    private const string CachePrefix = "grocerystore:";
    private const int BatchSize = 500;

    public GroceryStoreRepository(
        string connectionString,
        HybridCacheService? cache = null,
        ILogger<GroceryStoreRepository>? logger = null)
        : base(connectionString)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<GroceryStoreDto?> GetByIdAsync(Guid id)
    {
        if (_cache != null)
        {
            var cacheKey = $"{CachePrefix}id:{id}";
            return await _cache.GetOrSetAsync(
                cacheKey,
                async () => await GetByIdFromDbAsync(id),
                memoryExpiry: TimeSpan.FromMinutes(15),
                distributedExpiry: TimeSpan.FromHours(1));
        }

        return await GetByIdFromDbAsync(id);
    }

    private async Task<GroceryStoreDto?> GetByIdFromDbAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Chain, StoreType, Address, City, State, ZipCode, County,
                   CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                   PhoneNumber, Website, ExternalId, DataSource,
                   AcceptsSnap, IsActive, OpeningHours, CreatedAt, UpdatedAt
            FROM GroceryStore
            WHERE Id = @Id AND IsActive = 1";

        var results = await ExecuteReaderAsync(sql, MapStore, CreateParameter("@Id", id));
        return results.FirstOrDefault();
    }

    public async Task<GroceryStoreDto?> GetByExternalIdAsync(string externalId, string dataSource)
    {
        const string sql = @"
            SELECT Id, Name, Chain, StoreType, Address, City, State, ZipCode, County,
                   CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                   PhoneNumber, Website, ExternalId, DataSource,
                   AcceptsSnap, IsActive, OpeningHours, CreatedAt, UpdatedAt
            FROM GroceryStore
            WHERE ExternalId = @ExternalId AND DataSource = @DataSource";

        var results = await ExecuteReaderAsync(sql, MapStore,
            CreateParameter("@ExternalId", externalId),
            CreateParameter("@DataSource", dataSource));
        return results.FirstOrDefault();
    }

    public async Task<List<GroceryStoreDto>> SearchAsync(GroceryStoreSearchRequest request)
    {
        if (_cache != null)
        {
            var cacheKey = $"{CachePrefix}search:{BuildSearchCacheKey(request)}";
            return await _cache.GetOrSetAsync(
                cacheKey,
                async () => await SearchFromDbAsync(request),
                memoryExpiry: TimeSpan.FromMinutes(5),
                distributedExpiry: TimeSpan.FromMinutes(30));
        }

        return await SearchFromDbAsync(request);
    }

    private async Task<List<GroceryStoreDto>> SearchFromDbAsync(GroceryStoreSearchRequest request)
    {
        var (sql, parameters) = BuildSearchQuery(request, countOnly: false);
        return await ExecuteReaderAsync(sql, MapStore, parameters.ToArray());
    }

    public async Task<int> GetSearchCountAsync(GroceryStoreSearchRequest request)
    {
        var (sql, parameters) = BuildSearchQuery(request, countOnly: true);
        var result = await ExecuteScalarAsync<int?>(sql, parameters.ToArray());
        return result ?? 0;
    }

    private (string sql, List<SqlParameter> parameters) BuildSearchQuery(
        GroceryStoreSearchRequest request, bool countOnly)
    {
        var where = new List<string>();
        var parameters = new List<SqlParameter>();

        if (request.IsActive.HasValue)
        {
            where.Add("IsActive = @IsActive");
            parameters.Add(new SqlParameter("@IsActive", request.IsActive.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            where.Add("Name LIKE @Name");
            parameters.Add(new SqlParameter("@Name", $"%{request.Name}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.Chain))
        {
            where.Add("Chain LIKE @Chain");
            parameters.Add(new SqlParameter("@Chain", $"%{request.Chain}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            where.Add("City = @City");
            parameters.Add(new SqlParameter("@City", request.City));
        }

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            where.Add("State = @State");
            parameters.Add(new SqlParameter("@State", request.State));
        }

        if (!string.IsNullOrWhiteSpace(request.ZipCode))
        {
            where.Add("ZipCode = @ZipCode");
            parameters.Add(new SqlParameter("@ZipCode", request.ZipCode));
        }

        if (!string.IsNullOrWhiteSpace(request.StoreType))
        {
            where.Add("StoreType = @StoreType");
            parameters.Add(new SqlParameter("@StoreType", request.StoreType));
        }

        if (request.AcceptsSnap.HasValue)
        {
            where.Add("AcceptsSnap = @AcceptsSnap");
            parameters.Add(new SqlParameter("@AcceptsSnap", request.AcceptsSnap.Value));
        }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;

        string sql;
        if (countOnly)
        {
            sql = $"SELECT COUNT(*) FROM GroceryStore {whereClause}";
        }
        else
        {
            var offset = (request.Page - 1) * request.PageSize;
            parameters.Add(new SqlParameter("@Offset", offset));
            parameters.Add(new SqlParameter("@PageSize", request.PageSize));

            sql = $@"
                SELECT Id, Name, Chain, StoreType, Address, City, State, ZipCode, County,
                       CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                       PhoneNumber, Website, ExternalId, DataSource,
                       AcceptsSnap, IsActive, OpeningHours, CreatedAt, UpdatedAt
                FROM GroceryStore
                {whereClause}
                ORDER BY Name
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        }

        return (sql, parameters);
    }

    public async Task<List<GroceryStoreDto>> GetNearbyAsync(
        double latitude, double longitude, double radiusMiles, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) gs.Id, gs.Name, gs.Chain, gs.StoreType, gs.Address, gs.City, gs.State,
                   gs.ZipCode, gs.County,
                   CAST(gs.Latitude AS FLOAT) AS Latitude, CAST(gs.Longitude AS FLOAT) AS Longitude,
                   gs.PhoneNumber, gs.Website, gs.ExternalId, gs.DataSource,
                   gs.AcceptsSnap, gs.IsActive, gs.OpeningHours, gs.CreatedAt, gs.UpdatedAt,
                   d.DistanceMiles
            FROM GroceryStore gs
            CROSS APPLY (
                SELECT 3959 * ACOS(
                    COS(RADIANS(@Lat)) * COS(RADIANS(gs.Latitude)) * COS(RADIANS(gs.Longitude) - RADIANS(@Lon)) +
                    SIN(RADIANS(@Lat)) * SIN(RADIANS(gs.Latitude))
                ) AS DistanceMiles
            ) d
            WHERE gs.IsActive = 1
              AND gs.Latitude IS NOT NULL
              AND gs.Longitude IS NOT NULL
              AND d.DistanceMiles <= @RadiusMiles
            ORDER BY d.DistanceMiles";

        return await ExecuteReaderAsync(sql, reader =>
        {
            var store = MapStore(reader);
            var distOrdinal = reader.GetOrdinal("DistanceMiles");
            store.DistanceMiles = reader.IsDBNull(distOrdinal) ? null : reader.GetDouble(distOrdinal);
            return store;
        },
        new SqlParameter("@Lat", latitude),
        new SqlParameter("@Lon", longitude),
        new SqlParameter("@RadiusMiles", radiusMiles),
        new SqlParameter("@Limit", limit));
    }

    public async Task<Guid> UpsertAsync(UpsertGroceryStoreRequest request)
    {
        const string sql = @"
            MERGE GroceryStore AS target
            USING (SELECT @ExternalId AS ExternalId, @DataSource AS DataSource) AS source
            ON target.ExternalId = source.ExternalId AND target.DataSource = source.DataSource
            WHEN MATCHED THEN
                UPDATE SET
                    Name = @Name,
                    Chain = @Chain,
                    StoreType = @StoreType,
                    Address = @Address,
                    City = @City,
                    State = @State,
                    ZipCode = @ZipCode,
                    County = @County,
                    Latitude = @Latitude,
                    Longitude = @Longitude,
                    PhoneNumber = @PhoneNumber,
                    Website = @Website,
                    AcceptsSnap = @AcceptsSnap,
                    IsActive = @IsActive,
                    OpeningHours = @OpeningHours,
                    UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Name, Chain, StoreType, Address, City, State, ZipCode, County,
                        Latitude, Longitude, PhoneNumber, Website, ExternalId, DataSource,
                        AcceptsSnap, IsActive, OpeningHours, CreatedAt)
                VALUES (@Name, @Chain, @StoreType, @Address, @City, @State, @ZipCode, @County,
                        @Latitude, @Longitude, @PhoneNumber, @Website, @ExternalId, @DataSource,
                        @AcceptsSnap, @IsActive, @OpeningHours, GETUTCDATE())
            OUTPUT INSERTED.Id;";

        var result = await ExecuteScalarAsync<Guid>(sql, BuildUpsertParameters(request).ToArray());
        return result;
    }

    public async Task<int> BulkUpsertAsync(IEnumerable<UpsertGroceryStoreRequest> stores)
    {
        var storeList = stores.ToList();
        var totalImported = 0;

        for (int i = 0; i < storeList.Count; i += BatchSize)
        {
            var batch = storeList.Skip(i).Take(BatchSize).ToList();
            var batchCount = 0;

            foreach (var store in batch)
            {
                try
                {
                    await UpsertAsync(store);
                    batchCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to upsert store {Name} from {DataSource}", store.Name, store.DataSource);
                }
            }

            totalImported += batchCount;
            _logger?.LogDebug("Bulk upsert batch {BatchNum}: {Count}/{Total} stores processed",
                (i / BatchSize) + 1, totalImported, storeList.Count);
        }

        return totalImported;
    }

    public async Task<StoreImportLogDto> LogImportAsync(StoreImportLogDto log)
    {
        const string sql = @"
            INSERT INTO StoreImportLog
                (DataSource, ImportedAt, RecordsProcessed, RecordsImported, RecordsUpdated,
                 RecordsSkipped, ErrorCount, ErrorMessage, Success)
            OUTPUT INSERTED.Id
            VALUES
                (@DataSource, GETUTCDATE(), @RecordsProcessed, @RecordsImported, @RecordsUpdated,
                 @RecordsSkipped, @ErrorCount, @ErrorMessage, @Success)";

        var id = await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@DataSource", log.DataSource),
            CreateParameter("@RecordsProcessed", log.RecordsProcessed),
            CreateParameter("@RecordsImported", log.RecordsImported),
            CreateParameter("@RecordsUpdated", log.RecordsUpdated),
            CreateParameter("@RecordsSkipped", log.RecordsSkipped),
            CreateParameter("@ErrorCount", log.ErrorCount),
            CreateParameter("@ErrorMessage", (object?)log.ErrorMessage ?? DBNull.Value),
            CreateParameter("@Success", log.Success));

        log.Id = id;
        return log;
    }

    public async Task<StoreImportLogDto?> GetLastImportAsync(string dataSource)
    {
        const string sql = @"
            SELECT TOP 1 Id, DataSource, ImportedAt, RecordsProcessed, RecordsImported,
                   RecordsUpdated, RecordsSkipped, ErrorCount, ErrorMessage, Success
            FROM StoreImportLog
            WHERE DataSource = @DataSource
            ORDER BY ImportedAt DESC";

        var results = await ExecuteReaderAsync(sql, reader => new StoreImportLogDto
        {
            Id = GetGuid(reader, "Id"),
            DataSource = GetString(reader, "DataSource") ?? string.Empty,
            ImportedAt = GetDateTime(reader, "ImportedAt"),
            RecordsProcessed = GetInt32(reader, "RecordsProcessed"),
            RecordsImported = GetInt32(reader, "RecordsImported"),
            RecordsUpdated = GetInt32(reader, "RecordsUpdated"),
            RecordsSkipped = GetInt32(reader, "RecordsSkipped"),
            ErrorCount = GetInt32(reader, "ErrorCount"),
            ErrorMessage = GetNullableString(reader, "ErrorMessage"),
            Success = GetBoolean(reader, "Success")
        }, CreateParameter("@DataSource", dataSource));

        return results.FirstOrDefault();
    }

    public async Task<int> GetStoreCountAsync()
    {
        const string sql = "SELECT COUNT(*) FROM GroceryStore WHERE IsActive = 1";
        return await ExecuteScalarAsync<int?>(sql) ?? 0;
    }

    private static GroceryStoreDto MapStore(System.Data.IDataRecord reader)
    {
        var latOrdinal = reader.GetOrdinal("Latitude");
        var lonOrdinal = reader.GetOrdinal("Longitude");

        return new GroceryStoreDto
        {
            Id = GetGuid(reader, "Id"),
            Name = GetString(reader, "Name") ?? string.Empty,
            Chain = GetNullableString(reader, "Chain"),
            StoreType = GetNullableString(reader, "StoreType"),
            Address = GetNullableString(reader, "Address"),
            City = GetNullableString(reader, "City"),
            State = GetNullableString(reader, "State"),
            ZipCode = GetNullableString(reader, "ZipCode"),
            County = GetNullableString(reader, "County"),
            Latitude = reader.IsDBNull(latOrdinal) ? null : reader.GetDouble(latOrdinal),
            Longitude = reader.IsDBNull(lonOrdinal) ? null : reader.GetDouble(lonOrdinal),
            PhoneNumber = GetNullableString(reader, "PhoneNumber"),
            Website = GetNullableString(reader, "Website"),
            ExternalId = GetNullableString(reader, "ExternalId"),
            DataSource = GetNullableString(reader, "DataSource"),
            AcceptsSnap = GetBoolean(reader, "AcceptsSnap"),
            IsActive = GetBoolean(reader, "IsActive"),
            OpeningHours = GetNullableString(reader, "OpeningHours"),
            CreatedAt = GetDateTime(reader, "CreatedAt"),
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
        };
    }

    private static List<SqlParameter> BuildUpsertParameters(UpsertGroceryStoreRequest request)
    {
        return new List<SqlParameter>
        {
            new("@Name", request.Name),
            new("@Chain", (object?)request.Chain ?? DBNull.Value),
            new("@StoreType", (object?)request.StoreType ?? DBNull.Value),
            new("@Address", (object?)request.Address ?? DBNull.Value),
            new("@City", (object?)request.City ?? DBNull.Value),
            new("@State", (object?)request.State ?? DBNull.Value),
            new("@ZipCode", (object?)request.ZipCode ?? DBNull.Value),
            new("@County", (object?)request.County ?? DBNull.Value),
            new("@Latitude", request.Latitude.HasValue ? (object)request.Latitude.Value : DBNull.Value),
            new("@Longitude", request.Longitude.HasValue ? (object)request.Longitude.Value : DBNull.Value),
            new("@PhoneNumber", (object?)request.PhoneNumber ?? DBNull.Value),
            new("@Website", (object?)request.Website ?? DBNull.Value),
            new("@ExternalId", request.ExternalId),
            new("@DataSource", request.DataSource),
            new("@AcceptsSnap", request.AcceptsSnap),
            new("@IsActive", request.IsActive),
            new("@OpeningHours", (object?)request.OpeningHours ?? DBNull.Value)
        };
    }

    private static string BuildSearchCacheKey(GroceryStoreSearchRequest request)
    {
        return $"{request.Name}|{request.Chain}|{request.City}|{request.State}|{request.ZipCode}|{request.StoreType}|{request.AcceptsSnap}|{request.IsActive}|{request.Page}|{request.PageSize}";
    }
}
