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
                async (ct) => await GetByIdFromDbAsync(id),
                expiration: TimeSpan.FromHours(1));
        }

        return await GetByIdFromDbAsync(id);
    }

    private async Task<GroceryStoreDto?> GetByIdFromDbAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Chain, NormalizedChain, StoreType, Address, City, State, ZipCode, County,
                   CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                   PhoneNumber, Website, ExternalId, DataSource,
                   OsmId, GersId, SnapStoreId, HifldId,
                   AcceptsSnap, IsActive, IsOnline, DeliveryAvailable, PickupAvailable,
                   BaseDeliveryFee, FreeDeliveryMin, AvgDeliveryDays,
                   IsVerified, VerifiedAt, VerifiedSource,
                   OpeningHours, CreatedAt, UpdatedAt
            FROM GroceryStore
            WHERE Id = @Id AND IsActive = 1";

        var results = await ExecuteReaderAsync(sql, MapStore, CreateParameter("@Id", id));
        return results.FirstOrDefault();
    }

    public async Task<GroceryStoreDto?> GetByExternalIdAsync(string externalId, string dataSource)
    {
        const string sql = @"
            SELECT Id, Name, Chain, NormalizedChain, StoreType, Address, City, State, ZipCode, County,
                   CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                   PhoneNumber, Website, ExternalId, DataSource,
                   OsmId, GersId, SnapStoreId, HifldId,
                   AcceptsSnap, IsActive, IsOnline, DeliveryAvailable, PickupAvailable,
                   BaseDeliveryFee, FreeDeliveryMin, AvgDeliveryDays,
                   IsVerified, VerifiedAt, VerifiedSource,
                   OpeningHours, CreatedAt, UpdatedAt
            FROM GroceryStore
            WHERE ExternalId = @ExternalId AND DataSource = @DataSource";

        var results = await ExecuteReaderAsync(sql, MapStore,
            CreateParameter("@ExternalId", externalId),
            CreateParameter("@DataSource", dataSource));
        return results.FirstOrDefault();
    }

    public async Task<GroceryStoreDto?> GetByOsmIdAsync(long osmId)
    {
        const string sql = @"
            SELECT Id, Name, Chain, NormalizedChain, StoreType, Address, City, State, ZipCode, County,
                   CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                   PhoneNumber, Website, ExternalId, DataSource,
                   OsmId, GersId, SnapStoreId, HifldId,
                   AcceptsSnap, IsActive, IsOnline, DeliveryAvailable, PickupAvailable,
                   BaseDeliveryFee, FreeDeliveryMin, AvgDeliveryDays,
                   IsVerified, VerifiedAt, VerifiedSource,
                   OpeningHours, CreatedAt, UpdatedAt
            FROM GroceryStore
            WHERE OsmId = @OsmId";

        var results = await ExecuteReaderAsync(sql, MapStore, CreateParameter("@OsmId", osmId));
        return results.FirstOrDefault();
    }

    public async Task<GroceryStoreDto?> GetByGersIdAsync(string gersId)
    {
        const string sql = @"
            SELECT Id, Name, Chain, NormalizedChain, StoreType, Address, City, State, ZipCode, County,
                   CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                   PhoneNumber, Website, ExternalId, DataSource,
                   OsmId, GersId, SnapStoreId, HifldId,
                   AcceptsSnap, IsActive, IsOnline, DeliveryAvailable, PickupAvailable,
                   BaseDeliveryFee, FreeDeliveryMin, AvgDeliveryDays,
                   IsVerified, VerifiedAt, VerifiedSource,
                   OpeningHours, CreatedAt, UpdatedAt
            FROM GroceryStore
            WHERE GersId = @GersId";

        var results = await ExecuteReaderAsync(sql, MapStore, CreateParameter("@GersId", gersId));
        return results.FirstOrDefault();
    }

    public async Task<List<GroceryStoreDto>> SearchAsync(GroceryStoreSearchRequest request)
    {
        if (_cache != null)
        {
            var cacheKey = $"{CachePrefix}search:{BuildSearchCacheKey(request)}";
            return await _cache.GetOrSetAsync(
                cacheKey,
                async (ct) => await SearchFromDbAsync(request),
                expiration: TimeSpan.FromMinutes(30)) ?? new List<GroceryStoreDto>();
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

        if (!string.IsNullOrWhiteSpace(request.NormalizedChain))
        {
            where.Add("NormalizedChain = @NormalizedChain");
            parameters.Add(new SqlParameter("@NormalizedChain", request.NormalizedChain));
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

        if (request.IsVerified.HasValue)
        {
            where.Add("IsVerified = @IsVerified");
            parameters.Add(new SqlParameter("@IsVerified", request.IsVerified.Value));
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
                SELECT Id, Name, Chain, NormalizedChain, StoreType, Address, City, State, ZipCode, County,
                       CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                       PhoneNumber, Website, ExternalId, DataSource,
                       OsmId, GersId, SnapStoreId, HifldId,
                       AcceptsSnap, IsActive, IsOnline, DeliveryAvailable, PickupAvailable,
                       BaseDeliveryFee, FreeDeliveryMin, AvgDeliveryDays,
                       IsVerified, VerifiedAt, VerifiedSource,
                       OpeningHours, CreatedAt, UpdatedAt
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
            SELECT TOP (@Limit) gs.Id, gs.Name, gs.Chain, gs.NormalizedChain, gs.StoreType,
                   gs.Address, gs.City, gs.State, gs.ZipCode, gs.County,
                   CAST(gs.Latitude AS FLOAT) AS Latitude, CAST(gs.Longitude AS FLOAT) AS Longitude,
                   gs.PhoneNumber, gs.Website, gs.ExternalId, gs.DataSource,
                   gs.OsmId, gs.GersId, gs.SnapStoreId, gs.HifldId,
                   gs.AcceptsSnap, gs.IsActive, gs.IsOnline, gs.DeliveryAvailable, gs.PickupAvailable,
                   gs.BaseDeliveryFee, gs.FreeDeliveryMin, gs.AvgDeliveryDays,
                   gs.IsVerified, gs.VerifiedAt, gs.VerifiedSource,
                   gs.OpeningHours, gs.CreatedAt, gs.UpdatedAt,
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

    public async Task<List<GroceryStoreDto>> GetByChainAsync(string normalizedChain, int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, Name, Chain, NormalizedChain, StoreType, Address, City, State, ZipCode, County,
                   CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
                   PhoneNumber, Website, ExternalId, DataSource,
                   OsmId, GersId, SnapStoreId, HifldId,
                   AcceptsSnap, IsActive, IsOnline, DeliveryAvailable, PickupAvailable,
                   BaseDeliveryFee, FreeDeliveryMin, AvgDeliveryDays,
                   IsVerified, VerifiedAt, VerifiedSource,
                   OpeningHours, CreatedAt, UpdatedAt
            FROM GroceryStore
            WHERE NormalizedChain = @NormalizedChain AND IsActive = 1
            ORDER BY State, City, Name";

        return await ExecuteReaderAsync(sql, MapStore,
            new SqlParameter("@NormalizedChain", normalizedChain),
            new SqlParameter("@Limit", limit));
    }

    public async Task<Guid> UpsertAsync(UpsertGroceryStoreRequest request)
    {
        // Priority dedup strategy:
        // 1. GersId (Overture) - highest fidelity
        // 2. OsmId
        // 3. ExternalId + DataSource
        // 4. Address + ZipCode (cross-source match)
        const string sql = @"
            MERGE GroceryStore AS target
            USING (SELECT
                @ExternalId AS ExternalId,
                @DataSource AS DataSource,
                @GersId     AS GersId,
                @OsmId      AS OsmId,
                @Address    AS Address,
                @ZipCode    AS ZipCode
            ) AS source
            ON (
                (source.GersId IS NOT NULL AND target.GersId = source.GersId)
                OR (source.GersId IS NULL AND source.OsmId IS NOT NULL AND target.OsmId = source.OsmId)
                OR (source.GersId IS NULL AND source.OsmId IS NULL
                    AND target.ExternalId = source.ExternalId AND target.DataSource = source.DataSource)
                OR (source.GersId IS NULL AND source.OsmId IS NULL
                    AND source.Address IS NOT NULL AND source.ZipCode IS NOT NULL
                    AND target.Address = source.Address AND target.ZipCode = source.ZipCode)
            )
            WHEN MATCHED THEN
                UPDATE SET
                    Name              = @Name,
                    Chain             = @Chain,
                    NormalizedChain   = @NormalizedChain,
                    StoreType         = @StoreType,
                    Address           = @Address,
                    City              = @City,
                    State             = @State,
                    ZipCode           = @ZipCode,
                    County            = @County,
                    Latitude          = @Latitude,
                    Longitude         = @Longitude,
                    PhoneNumber       = COALESCE(@PhoneNumber, target.PhoneNumber),
                    Website           = COALESCE(@Website, target.Website),
                    ExternalId        = CASE WHEN target.DataSource = @DataSource THEN @ExternalId ELSE target.ExternalId END,
                    DataSource        = CASE WHEN target.DataSource = @DataSource THEN @DataSource ELSE target.DataSource END,
                    OsmId             = COALESCE(@OsmId, target.OsmId),
                    GersId            = COALESCE(@GersId, target.GersId),
                    SnapStoreId       = COALESCE(@SnapStoreId, target.SnapStoreId),
                    HifldId           = COALESCE(@HifldId, target.HifldId),
                    AcceptsSnap       = CASE WHEN @AcceptsSnap = 1 THEN 1 ELSE target.AcceptsSnap END,
                    IsActive          = @IsActive,
                    IsOnline          = @IsOnline,
                    DeliveryAvailable = @DeliveryAvailable,
                    PickupAvailable   = @PickupAvailable,
                    OpeningHours      = COALESCE(@OpeningHours, target.OpeningHours),
                    UpdatedAt         = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Name, Chain, NormalizedChain, StoreType, Address, City, State, ZipCode, County,
                        Latitude, Longitude, PhoneNumber, Website, ExternalId, DataSource,
                        OsmId, GersId, SnapStoreId, HifldId,
                        AcceptsSnap, IsActive, IsOnline, DeliveryAvailable, PickupAvailable,
                        OpeningHours, CreatedAt)
                VALUES (@Name, @Chain, @NormalizedChain, @StoreType, @Address, @City, @State, @ZipCode, @County,
                        @Latitude, @Longitude, @PhoneNumber, @Website, @ExternalId, @DataSource,
                        @OsmId, @GersId, @SnapStoreId, @HifldId,
                        @AcceptsSnap, @IsActive, @IsOnline, @DeliveryAvailable, @PickupAvailable,
                        @OpeningHours, GETUTCDATE())
            OUTPUT INSERTED.Id;";

        return await ExecuteScalarAsync<Guid>(sql, BuildUpsertParameters(request).ToArray());
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

            if (totalImported % 1000 < BatchSize || i + BatchSize >= storeList.Count)
            {
                _logger?.LogInformation("Bulk upsert progress: {Done}/{Total} stores processed",
                    Math.Min(i + BatchSize, storeList.Count), storeList.Count);
            }
        }

        return totalImported;
    }

    public async Task<int> MarkVerifiedAsync(Guid storeId, string verifiedSource)
    {
        const string sql = @"
            UPDATE GroceryStore
            SET IsVerified = 1, VerifiedAt = GETUTCDATE(), VerifiedSource = @VerifiedSource, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        return await ExecuteNonQueryAsync(sql,
            CreateParameter("@Id", storeId),
            CreateParameter("@VerifiedSource", verifiedSource));
    }

    public async Task<List<StoreHoursDto>> GetStoreHoursAsync(Guid storeId)
    {
        const string sql = @"
            SELECT Id, StoreId, DayOfWeek, OpenTime, CloseTime, IsClosed, IsHoliday, HolidayDate
            FROM StoreHours
            WHERE StoreId = @StoreId
            ORDER BY DayOfWeek";

        return await ExecuteReaderAsync(sql, reader => new StoreHoursDto
        {
            Id = GetGuid(reader, "Id"),
            StoreId = GetGuid(reader, "StoreId"),
            DayOfWeek = (byte)GetInt32(reader, "DayOfWeek"),
            OpenTime = GetNullableTimeSpan(reader, "OpenTime"),
            CloseTime = GetNullableTimeSpan(reader, "CloseTime"),
            IsClosed = GetBoolean(reader, "IsClosed"),
            IsHoliday = GetBoolean(reader, "IsHoliday"),
            HolidayDate = GetNullableDateTime(reader, "HolidayDate")
        }, CreateParameter("@StoreId", storeId));
    }

    public async Task<int> UpsertStoreHoursAsync(Guid storeId, IEnumerable<StoreHoursRequest> hours)
    {
        var hoursList = hours.ToList();
        var total = 0;

        foreach (var hour in hoursList)
        {
            const string sql = @"
                MERGE StoreHours AS target
                USING (SELECT @StoreId AS StoreId, @DayOfWeek AS DayOfWeek) AS source
                ON target.StoreId = source.StoreId AND target.DayOfWeek = source.DayOfWeek
                WHEN MATCHED THEN
                    UPDATE SET OpenTime = @OpenTime, CloseTime = @CloseTime,
                               IsClosed = @IsClosed, IsHoliday = @IsHoliday, HolidayDate = @HolidayDate
                WHEN NOT MATCHED THEN
                    INSERT (StoreId, DayOfWeek, OpenTime, CloseTime, IsClosed, IsHoliday, HolidayDate)
                    VALUES (@StoreId, @DayOfWeek, @OpenTime, @CloseTime, @IsClosed, @IsHoliday, @HolidayDate);";

            total += await ExecuteNonQueryAsync(sql,
                CreateParameter("@StoreId", storeId),
                CreateParameter("@DayOfWeek", hour.DayOfWeek),
                new SqlParameter("@OpenTime", hour.OpenTime.HasValue ? (object)hour.OpenTime.Value : DBNull.Value),
                new SqlParameter("@CloseTime", hour.CloseTime.HasValue ? (object)hour.CloseTime.Value : DBNull.Value),
                CreateParameter("@IsClosed", hour.IsClosed),
                CreateParameter("@IsHoliday", hour.IsHoliday),
                new SqlParameter("@HolidayDate", hour.HolidayDate.HasValue ? (object)hour.HolidayDate.Value.Date : DBNull.Value));
        }

        return total;
    }

    public async Task<List<StoreChainDto>> GetAllChainsAsync()
    {
        const string sql = @"
            SELECT Id, CanonicalName, Aliases, LogoUrl, Website, IsNational, IsOnlineOnly
            FROM StoreChain
            ORDER BY CanonicalName";

        return await ExecuteReaderAsync(sql, reader => new StoreChainDto
        {
            Id = GetGuid(reader, "Id"),
            CanonicalName = GetString(reader, "CanonicalName") ?? string.Empty,
            Aliases = GetNullableString(reader, "Aliases"),
            LogoUrl = GetNullableString(reader, "LogoUrl"),
            Website = GetNullableString(reader, "Website"),
            IsNational = GetBoolean(reader, "IsNational"),
            IsOnlineOnly = GetBoolean(reader, "IsOnlineOnly")
        });
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

        long? osmId = null;
        string? gersId = null;
        string? snapStoreId = null;
        string? hifldId = null;
        bool isOnline = false;
        bool deliveryAvailable = false;
        bool pickupAvailable = false;
        decimal? baseDeliveryFee = null;
        decimal? freeDeliveryMin = null;
        decimal? avgDeliveryDays = null;
        bool isVerified = false;
        DateTime? verifiedAt = null;
        string? verifiedSource = null;
        string? normalizedChain = null;

        if (TryGetOrdinal(reader, "OsmId", out var osmOrdinal))
        {
            osmId = reader.IsDBNull(osmOrdinal) ? null : reader.GetInt64(osmOrdinal);
        }
        if (TryGetOrdinal(reader, "GersId", out var gersOrdinal))
        {
            gersId = reader.IsDBNull(gersOrdinal) ? null : reader.GetString(gersOrdinal);
        }
        if (TryGetOrdinal(reader, "SnapStoreId", out var snapOrdinal))
        {
            snapStoreId = reader.IsDBNull(snapOrdinal) ? null : reader.GetString(snapOrdinal);
        }
        if (TryGetOrdinal(reader, "HifldId", out var hifldOrdinal))
        {
            hifldId = reader.IsDBNull(hifldOrdinal) ? null : reader.GetString(hifldOrdinal);
        }
        if (TryGetOrdinal(reader, "IsOnline", out var isOnlineOrdinal))
        {
            isOnline = !reader.IsDBNull(isOnlineOrdinal) && reader.GetBoolean(isOnlineOrdinal);
        }
        if (TryGetOrdinal(reader, "DeliveryAvailable", out var delOrdinal))
        {
            deliveryAvailable = !reader.IsDBNull(delOrdinal) && reader.GetBoolean(delOrdinal);
        }
        if (TryGetOrdinal(reader, "PickupAvailable", out var pickupOrdinal))
        {
            pickupAvailable = !reader.IsDBNull(pickupOrdinal) && reader.GetBoolean(pickupOrdinal);
        }
        if (TryGetOrdinal(reader, "BaseDeliveryFee", out var feeOrdinal))
        {
            baseDeliveryFee = reader.IsDBNull(feeOrdinal) ? null : reader.GetDecimal(feeOrdinal);
        }
        if (TryGetOrdinal(reader, "FreeDeliveryMin", out var freeMinOrdinal))
        {
            freeDeliveryMin = reader.IsDBNull(freeMinOrdinal) ? null : reader.GetDecimal(freeMinOrdinal);
        }
        if (TryGetOrdinal(reader, "AvgDeliveryDays", out var avgDaysOrdinal))
        {
            avgDeliveryDays = reader.IsDBNull(avgDaysOrdinal) ? null : reader.GetDecimal(avgDaysOrdinal);
        }
        if (TryGetOrdinal(reader, "IsVerified", out var isVerifiedOrdinal))
        {
            isVerified = !reader.IsDBNull(isVerifiedOrdinal) && reader.GetBoolean(isVerifiedOrdinal);
        }
        if (TryGetOrdinal(reader, "VerifiedAt", out var verifiedAtOrdinal))
        {
            verifiedAt = reader.IsDBNull(verifiedAtOrdinal) ? null : reader.GetDateTime(verifiedAtOrdinal);
        }
        if (TryGetOrdinal(reader, "VerifiedSource", out var verifiedSourceOrdinal))
        {
            verifiedSource = reader.IsDBNull(verifiedSourceOrdinal) ? null : reader.GetString(verifiedSourceOrdinal);
        }
        if (TryGetOrdinal(reader, "NormalizedChain", out var normalizedChainOrdinal))
        {
            normalizedChain = reader.IsDBNull(normalizedChainOrdinal) ? null : reader.GetString(normalizedChainOrdinal);
        }

        return new GroceryStoreDto
        {
            Id = GetGuid(reader, "Id"),
            Name = GetString(reader, "Name") ?? string.Empty,
            Chain = GetNullableString(reader, "Chain"),
            NormalizedChain = normalizedChain,
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
            OsmId = osmId,
            GersId = gersId,
            SnapStoreId = snapStoreId,
            HifldId = hifldId,
            AcceptsSnap = GetBoolean(reader, "AcceptsSnap"),
            IsActive = GetBoolean(reader, "IsActive"),
            IsOnline = isOnline,
            DeliveryAvailable = deliveryAvailable,
            PickupAvailable = pickupAvailable,
            BaseDeliveryFee = baseDeliveryFee,
            FreeDeliveryMin = freeDeliveryMin,
            AvgDeliveryDays = avgDeliveryDays,
            IsVerified = isVerified,
            VerifiedAt = verifiedAt,
            VerifiedSource = verifiedSource,
            OpeningHours = GetNullableString(reader, "OpeningHours"),
            CreatedAt = GetDateTime(reader, "CreatedAt"),
            UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
        };
    }

    private static bool TryGetOrdinal(System.Data.IDataRecord reader, string columnName, out int ordinal)
    {
        try
        {
            ordinal = reader.GetOrdinal(columnName);
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            ordinal = -1;
            return false;
        }
    }

    private static TimeSpan? GetNullableTimeSpan(System.Data.IDataRecord reader, string columnName)
    {
        if (!TryGetOrdinal(reader, columnName, out var ordinal)) return null;
        if (reader.IsDBNull(ordinal)) return null;
        return (TimeSpan)reader.GetValue(ordinal);
    }

    private static List<SqlParameter> BuildUpsertParameters(UpsertGroceryStoreRequest request)
    {
        return new List<SqlParameter>
        {
            new("@Name", request.Name),
            new("@Chain", (object?)request.Chain ?? DBNull.Value),
            new("@NormalizedChain", (object?)request.NormalizedChain ?? DBNull.Value),
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
            new("@OsmId", request.OsmId.HasValue ? (object)request.OsmId.Value : DBNull.Value),
            new("@GersId", (object?)request.GersId ?? DBNull.Value),
            new("@SnapStoreId", (object?)request.SnapStoreId ?? DBNull.Value),
            new("@HifldId", (object?)request.HifldId ?? DBNull.Value),
            new("@AcceptsSnap", request.AcceptsSnap),
            new("@IsActive", request.IsActive),
            new("@IsOnline", request.IsOnline),
            new("@DeliveryAvailable", request.DeliveryAvailable),
            new("@PickupAvailable", request.PickupAvailable),
            new("@OpeningHours", (object?)request.OpeningHours ?? DBNull.Value)
        };
    }

    private static string BuildSearchCacheKey(GroceryStoreSearchRequest request)
    {
        return $"{request.Name}|{request.Chain}|{request.NormalizedChain}|{request.City}|{request.State}|{request.ZipCode}|{request.StoreType}|{request.AcceptsSnap}|{request.IsActive}|{request.IsVerified}|{request.Page}|{request.PageSize}";
    }
}
