using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace ExpressRecipe.InventoryService.Data;

/// <summary>
/// Repository for livestock/flock management, production logging, and harvest processing.
/// </summary>
public class LivestockRepository : ILivestockRepository
{
    // Freshness days used when auto-creating inventory items from production
    private static readonly Dictionary<string, int?> ProductFreshnessDays = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Eggs",  35 },
        { "Milk",  7 },
        { "Honey", null },
        { "Fiber", null },
    };

    private readonly string _connectionString;
    private readonly ILogger<LivestockRepository> _logger;

    public LivestockRepository(string connectionString, ILogger<LivestockRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Animals / Flocks
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<LivestockAnimalDto>> GetAnimalsAsync(Guid householdId, bool activeOnly = true)
    {
        const string sql = @"
            SELECT Id, HouseholdId, Name, AnimalType, ProductionCategory, IsFlockOrHerd,
                   Count, AcquiredDate, BreedNotes, IsActive, Notes, CreatedAt, UpdatedAt
            FROM LivestockAnimal
            WHERE HouseholdId = @HouseholdId
              AND (@ActiveOnly = 0 OR IsActive = 1)
            ORDER BY Name";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);
        command.Parameters.AddWithValue("@ActiveOnly", activeOnly ? 1 : 0);

        return await ReadAnimalsAsync(command);
    }

    public async Task<LivestockAnimalDto?> GetAnimalByIdAsync(Guid animalId)
    {
        const string sql = @"
            SELECT Id, HouseholdId, Name, AnimalType, ProductionCategory, IsFlockOrHerd,
                   Count, AcquiredDate, BreedNotes, IsActive, Notes, CreatedAt, UpdatedAt
            FROM LivestockAnimal
            WHERE Id = @Id";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", animalId);

        List<LivestockAnimalDto> results = await ReadAnimalsAsync(command);
        return results.FirstOrDefault();
    }

    public async Task<Guid> AddAnimalAsync(Guid householdId, string name, string animalType,
        string productionCategory, bool isFlockOrHerd, int count, DateOnly? acquiredDate,
        string? breedNotes, string? notes)
    {
        const string sql = @"
            INSERT INTO LivestockAnimal
                (HouseholdId, Name, AnimalType, ProductionCategory, IsFlockOrHerd, Count,
                 AcquiredDate, BreedNotes, Notes, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@HouseholdId, @Name, @AnimalType, @ProductionCategory, @IsFlockOrHerd, @Count,
                 @AcquiredDate, @BreedNotes, @Notes, 1, GETUTCDATE())";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@AnimalType", animalType);
        command.Parameters.AddWithValue("@ProductionCategory", productionCategory);
        command.Parameters.AddWithValue("@IsFlockOrHerd", isFlockOrHerd ? 1 : 0);
        command.Parameters.AddWithValue("@Count", count);
        command.Parameters.AddWithValue("@AcquiredDate",
            acquiredDate.HasValue ? (object)acquiredDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
        command.Parameters.AddWithValue("@BreedNotes", breedNotes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

        Guid id = (Guid)(await command.ExecuteScalarAsync())!;
        _logger.LogInformation("Added livestock animal {AnimalId} ({Name}) to household {HouseholdId}",
            id, name, householdId);
        return id;
    }

    public async Task UpdateAnimalAsync(Guid animalId, string name, int count, bool isActive,
        string? breedNotes, string? notes)
    {
        const string sql = @"
            UPDATE LivestockAnimal
            SET Name = @Name, Count = @Count, IsActive = @IsActive,
                BreedNotes = @BreedNotes, Notes = @Notes, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", animalId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Count", count);
        command.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
        command.Parameters.AddWithValue("@BreedNotes", breedNotes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Updated livestock animal {AnimalId}", animalId);
    }

    public async Task SoftDeleteAnimalAsync(Guid animalId)
    {
        const string sql = @"
            UPDATE LivestockAnimal
            SET IsActive = 0, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", animalId);
        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Soft-deleted livestock animal {AnimalId}", animalId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Production Logging
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<Guid> LogProductionAsync(Guid animalId, Guid userId, DateOnly productionDate, string productType,
        decimal quantity, string unit, bool addToInventory, string? storageLocationId, string? notes)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlTransaction tx = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            Guid? inventoryItemId = null;

            if (addToInventory)
            {
                // Get animal details for naming and household reference
                LivestockAnimalDto? animal = await GetAnimalInTransactionAsync(animalId, connection, tx);
                if (animal == null)
                {
                    throw new InvalidOperationException($"Animal {animalId} not found.");
                }

                inventoryItemId = await UpsertProductionInventoryItemAsync(
                    animal, productType, quantity, unit, productionDate,
                    storageLocationId, userId, connection, tx);
            }

            // MERGE on unique key (AnimalId, ProductionDate, ProductType) — upsert same-day entry
            const string mergeSql = @"
                MERGE LivestockProduction AS target
                USING (SELECT @AnimalId AS AnimalId, @ProductionDate AS ProductionDate, @ProductType AS ProductType) AS source
                    ON target.AnimalId = source.AnimalId
                   AND target.ProductionDate = source.ProductionDate
                   AND target.ProductType = source.ProductType
                WHEN MATCHED THEN
                    UPDATE SET Quantity = @Quantity, Unit = @Unit,
                               AddedToInventory = CASE WHEN @AddedToInventory = 1 THEN 1 ELSE target.AddedToInventory END,
                               InventoryItemId  = COALESCE(@InventoryItemId, target.InventoryItemId),
                               Notes = @Notes
                WHEN NOT MATCHED THEN
                    INSERT (AnimalId, ProductionDate, ProductType, Quantity, Unit, AddedToInventory, InventoryItemId, Notes, CreatedAt)
                    VALUES (@AnimalId, @ProductionDate, @ProductType, @Quantity, @Unit, @AddedToInventory, @InventoryItemId, @Notes, GETUTCDATE())
                OUTPUT INSERTED.Id;";

            await using SqlCommand mergeCmd = new SqlCommand(mergeSql, connection, tx);
            mergeCmd.Parameters.AddWithValue("@AnimalId", animalId);
            mergeCmd.Parameters.AddWithValue("@ProductionDate", productionDate.ToString("yyyy-MM-dd"));
            mergeCmd.Parameters.AddWithValue("@ProductType", productType);
            mergeCmd.Parameters.AddWithValue("@Quantity", quantity);
            mergeCmd.Parameters.AddWithValue("@Unit", unit);
            mergeCmd.Parameters.AddWithValue("@AddedToInventory", addToInventory ? 1 : 0);
            mergeCmd.Parameters.AddWithValue("@InventoryItemId",
                inventoryItemId.HasValue ? (object)inventoryItemId.Value : DBNull.Value);
            mergeCmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

            Guid productionId = (Guid)(await mergeCmd.ExecuteScalarAsync())!;

            await tx.CommitAsync();

            _logger.LogInformation(
                "Logged production {ProductionId} for animal {AnimalId}: {Quantity} {Unit} of {ProductType}",
                productionId, animalId, quantity, unit, productType);

            return productionId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<LivestockProductionDto>> GetProductionAsync(Guid animalId, DateOnly from, DateOnly to)
    {
        const string sql = @"
            SELECT p.Id, p.AnimalId, a.Name AS AnimalName, p.ProductionDate,
                   p.ProductType, p.Quantity, p.Unit, p.AddedToInventory, p.InventoryItemId,
                   p.Notes, p.CreatedAt
            FROM LivestockProduction p
            INNER JOIN LivestockAnimal a ON p.AnimalId = a.Id
            WHERE p.AnimalId = @AnimalId
              AND p.ProductionDate >= @From
              AND p.ProductionDate <= @To
            ORDER BY p.ProductionDate DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AnimalId", animalId);
        command.Parameters.AddWithValue("@From", from.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@To", to.ToString("yyyy-MM-dd"));

        return await ReadProductionAsync(command);
    }

    public async Task<List<LivestockProductionSummaryDto>> GetProductionSummaryAsync(
        Guid householdId, DateOnly from, DateOnly to)
    {
        const string sql = @"
            SELECT p.ProductType,
                   SUM(p.Quantity)    AS TotalQuantity,
                   MAX(p.Unit)        AS Unit,
                   COUNT(DISTINCT p.ProductionDate) AS DaysRecorded,
                   SUM(p.Quantity) / NULLIF(DATEDIFF(day, @From, @To) + 1, 0) AS DailyAverage
            FROM LivestockProduction p
            INNER JOIN LivestockAnimal a ON p.AnimalId = a.Id
            WHERE a.HouseholdId = @HouseholdId
              AND p.ProductionDate >= @From
              AND p.ProductionDate <= @To
            GROUP BY p.ProductType
            ORDER BY p.ProductType";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);
        command.Parameters.AddWithValue("@From", from.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@To", to.ToString("yyyy-MM-dd"));

        List<LivestockProductionSummaryDto> results = new List<LivestockProductionSummaryDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new LivestockProductionSummaryDto
            {
                ProductType   = reader.GetString(0),
                TotalQuantity = reader.GetDecimal(1),
                Unit          = reader.GetString(2),
                DaysRecorded  = reader.GetInt32(3),
                DailyAverage  = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
            });
        }
        return results;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Harvest / Processing
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<Guid> RecordHarvestAsync(Guid animalId, Guid userId, DateOnly harvestDate, int countHarvested,
        decimal? liveWeightLbs, decimal? processedWeightLbs, string? processedBy,
        bool addToInventory, List<HarvestYieldItem> yieldItems, string? storageLocationId, string? notes)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlTransaction tx = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            LivestockAnimalDto? animal = await GetAnimalInTransactionAsync(animalId, connection, tx);
            if (animal == null)
            {
                throw new InvalidOperationException($"Animal {animalId} not found.");
            }

            List<HarvestYieldItem> enrichedYieldItems = new List<HarvestYieldItem>(yieldItems.Count);

            if (addToInventory && yieldItems.Count > 0)
            {
                Guid? storLocId = Guid.TryParse(storageLocationId, out Guid parsedLocId)
                    ? parsedLocId : (Guid?)null;

                foreach (HarvestYieldItem item in yieldItems)
                {
                    Guid invItemId = await CreateHarvestInventoryItemAsync(
                        animal, item, harvestDate, storLocId, userId, connection, tx);

                    enrichedYieldItems.Add(new HarvestYieldItem
                    {
                        Cut             = item.Cut,
                        WeightLbs       = item.WeightLbs,
                        Unit            = item.Unit,
                        InventoryItemId = invItemId,
                    });
                }
            }
            else
            {
                enrichedYieldItems.AddRange(yieldItems);
            }

            string yieldItemsJson = JsonSerializer.Serialize(enrichedYieldItems);

            // Decrement flock/herd count if this is a group
            if (animal.IsFlockOrHerd && countHarvested > 0)
            {
                const string decrementSql = @"
                    UPDATE LivestockAnimal
                    SET Count = CASE WHEN Count - @CountHarvested < 0 THEN 0 ELSE Count - @CountHarvested END,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id";

                await using SqlCommand decCmd = new SqlCommand(decrementSql, connection, tx);
                decCmd.Parameters.AddWithValue("@Id", animalId);
                decCmd.Parameters.AddWithValue("@CountHarvested", countHarvested);
                await decCmd.ExecuteNonQueryAsync();
            }

            const string insertSql = @"
                INSERT INTO LivestockHarvest
                    (AnimalId, HarvestDate, CountHarvested, LiveWeightLbs, ProcessedWeightLbs,
                     YieldItemsJson, AddedToInventory, ProcessedBy, Notes, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@AnimalId, @HarvestDate, @CountHarvested, @LiveWeightLbs, @ProcessedWeightLbs,
                     @YieldItemsJson, @AddedToInventory, @ProcessedBy, @Notes, GETUTCDATE())";

            await using SqlCommand insertCmd = new SqlCommand(insertSql, connection, tx);
            insertCmd.Parameters.AddWithValue("@AnimalId", animalId);
            insertCmd.Parameters.AddWithValue("@HarvestDate", harvestDate.ToString("yyyy-MM-dd"));
            insertCmd.Parameters.AddWithValue("@CountHarvested", countHarvested);
            insertCmd.Parameters.AddWithValue("@LiveWeightLbs",
                liveWeightLbs.HasValue ? (object)liveWeightLbs.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@ProcessedWeightLbs",
                processedWeightLbs.HasValue ? (object)processedWeightLbs.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@YieldItemsJson", yieldItemsJson);
            insertCmd.Parameters.AddWithValue("@AddedToInventory", addToInventory ? 1 : 0);
            insertCmd.Parameters.AddWithValue("@ProcessedBy", processedBy ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

            Guid harvestId = (Guid)(await insertCmd.ExecuteScalarAsync())!;

            await tx.CommitAsync();

            _logger.LogInformation(
                "Recorded harvest {HarvestId} for animal {AnimalId}: {CountHarvested} harvested",
                harvestId, animalId, countHarvested);

            return harvestId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<LivestockHarvestDto>> GetHarvestsAsync(Guid animalId)
    {
        const string sql = @"
            SELECT h.Id, h.AnimalId, a.Name AS AnimalName, h.HarvestDate, h.CountHarvested,
                   h.LiveWeightLbs, h.ProcessedWeightLbs, h.YieldItemsJson,
                   h.AddedToInventory, h.ProcessedBy, h.Notes, h.CreatedAt
            FROM LivestockHarvest h
            INNER JOIN LivestockAnimal a ON h.AnimalId = a.Id
            WHERE h.AnimalId = @AnimalId
            ORDER BY h.HarvestDate DESC";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AnimalId", animalId);

        return await ReadHarvestsAsync(command);
    }

    public async Task LinkHarvestToInventoryAsync(Guid harvestId, string yieldItemsJson)
    {
        const string sql = @"
            UPDATE LivestockHarvest
            SET YieldItemsJson = @YieldItemsJson, AddedToInventory = 1
            WHERE Id = @Id";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", harvestId);
        command.Parameters.AddWithValue("@YieldItemsJson", yieldItemsJson);
        await command.ExecuteNonQueryAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task<LivestockAnimalDto?> GetAnimalInTransactionAsync(
        Guid animalId, SqlConnection connection, SqlTransaction tx)
    {
        const string sql = @"
            SELECT Id, HouseholdId, Name, AnimalType, ProductionCategory, IsFlockOrHerd,
                   Count, AcquiredDate, BreedNotes, IsActive, Notes, CreatedAt, UpdatedAt
            FROM LivestockAnimal
            WHERE Id = @Id";

        await using SqlCommand command = new SqlCommand(sql, connection, tx);
        command.Parameters.AddWithValue("@Id", animalId);
        List<LivestockAnimalDto> results = await ReadAnimalsAsync(command);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Finds or creates an InventoryItem for a daily production log.
    /// Scopes the search to the same household and unit to avoid cross-household collisions.
    /// If a non-expired homestead item already exists for the animal+product, increments its quantity.
    /// </summary>
    private async Task<Guid> UpsertProductionInventoryItemAsync(
        LivestockAnimalDto animal, string productType, decimal quantity, string unit,
        DateOnly productionDate, string? storageLocationId, Guid userId,
        SqlConnection connection, SqlTransaction tx)
    {
        string itemName = $"{animal.Name} {productType}";
        DateTime? expirationDate = ComputeProductionExpiration(productType, productionDate);

        // Try to find an existing non-expired homestead item for this animal+product,
        // scoped to the same household and unit to prevent cross-household or cross-unit matches.
        const string findSql = @"
            SELECT TOP 1 Id, Quantity
            FROM InventoryItem
            WHERE CustomName = @Name
              AND HouseholdId = @HouseholdId
              AND Unit = @Unit
              AND Source = 'Homestead'
              AND IsDeleted = 0
              AND (ExpirationDate IS NULL OR ExpirationDate > GETUTCDATE())
            ORDER BY CreatedAt DESC";

        await using (SqlCommand findCmd = new SqlCommand(findSql, connection, tx))
        {
            findCmd.Parameters.AddWithValue("@Name", itemName);
            findCmd.Parameters.AddWithValue("@HouseholdId", animal.HouseholdId);
            findCmd.Parameters.AddWithValue("@Unit", unit);
            await using SqlDataReader reader = await findCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Guid existingId = reader.GetGuid(0);
                decimal existingQty = reader.GetDecimal(1);
                await reader.CloseAsync();

                // Increment existing item
                const string updateSql = @"
                    UPDATE InventoryItem
                    SET Quantity = @NewQty, UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id";

                await using SqlCommand updateCmd = new SqlCommand(updateSql, connection, tx);
                updateCmd.Parameters.AddWithValue("@Id", existingId);
                updateCmd.Parameters.AddWithValue("@NewQty", existingQty + quantity);
                await updateCmd.ExecuteNonQueryAsync();

                return existingId;
            }
        }

        // Create a new InventoryItem
        Guid? storLocId = Guid.TryParse(storageLocationId, out Guid parsedLocId)
            ? parsedLocId : (Guid?)null;

        return await CreateInventoryItemAsync(itemName, animal.HouseholdId, userId, quantity, unit,
            expirationDate, storLocId, storageMethod: null, connection, tx);
    }

    private static DateTime? ComputeProductionExpiration(string productType, DateOnly productionDate)
    {
        if (ProductFreshnessDays.TryGetValue(productType, out int? days) && days.HasValue)
        {
            return productionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(days.Value);
        }

        return null;
    }

    private async Task<Guid> CreateHarvestInventoryItemAsync(
        LivestockAnimalDto animal, HarvestYieldItem yieldItem,
        DateOnly harvestDate, Guid? storageLocationId, Guid userId,
        SqlConnection connection, SqlTransaction tx)
    {
        string itemName = $"{animal.Name} {yieldItem.Cut}";
        // Harvest items default to FrozenMeal storage — expires in 6 months
        DateTime expirationDate = harvestDate
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            .AddMonths(6);

        return await CreateInventoryItemAsync(
            itemName, animal.HouseholdId, userId, yieldItem.WeightLbs, yieldItem.Unit,
            expirationDate, storageLocationId, storageMethod: "FrozenMeal", connection, tx);
    }

    private static async Task<Guid> CreateInventoryItemAsync(
        string customName, Guid householdId, Guid userId, decimal quantity, string unit,
        DateTime? expirationDate, Guid? storageLocationId, string? storageMethod,
        SqlConnection connection, SqlTransaction tx)
    {
        bool isLongTermStorage = storageMethod is "Canned" or "FreezeDried" or "Dehydrated";

        // Resolve a default storage location for the household if none supplied
        Guid resolvedLocId;
        if (storageLocationId.HasValue)
        {
            resolvedLocId = storageLocationId.Value;
        }
        else
        {
            resolvedLocId = await ResolveDefaultStorageLocationAsync(householdId, connection, tx);
        }

        const string insertSql = @"
            INSERT INTO InventoryItem
                (UserId, HouseholdId, CustomName, StorageLocationId, Quantity, Unit,
                 ExpirationDate, Source, StorageMethod, IsLongTermStorage,
                 AddedBy, IsDeleted, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@UserId, @HouseholdId, @CustomName, @StorageLocationId, @Quantity, @Unit,
                 @ExpirationDate, 'Homestead', @StorageMethod, @IsLongTermStorage,
                 @UserId, 0, GETUTCDATE())";

        await using SqlCommand cmd = new SqlCommand(insertSql, connection, tx);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@HouseholdId", householdId);
        cmd.Parameters.AddWithValue("@CustomName", customName);
        cmd.Parameters.AddWithValue("@StorageLocationId", resolvedLocId);
        cmd.Parameters.AddWithValue("@Quantity", quantity);
        cmd.Parameters.AddWithValue("@Unit", unit);
        cmd.Parameters.AddWithValue("@ExpirationDate",
            expirationDate.HasValue ? (object)expirationDate.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@StorageMethod", storageMethod ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsLongTermStorage", isLongTermStorage ? 1 : 0);

        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> ResolveDefaultStorageLocationAsync(
        Guid householdId, SqlConnection connection, SqlTransaction tx)
    {
        // Find a default storage location for this household
        const string findSql = @"
            SELECT TOP 1 Id FROM StorageLocation
            WHERE HouseholdId = @HouseholdId AND IsDefault = 1 AND IsDeleted = 0";

        await using (SqlCommand cmd = new SqlCommand(findSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("@HouseholdId", householdId);
            object? result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return (Guid)result;
            }
        }

        // Fall back to any storage location for this household
        const string fallbackSql = @"
            SELECT TOP 1 Id FROM StorageLocation
            WHERE HouseholdId = @HouseholdId AND IsDeleted = 0
            ORDER BY CreatedAt";

        await using (SqlCommand cmd = new SqlCommand(fallbackSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("@HouseholdId", householdId);
            object? result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return (Guid)result;
            }
        }

        throw new InvalidOperationException(
            $"No storage location found for household {householdId}. " +
            "Please create a storage location before logging production with addToInventory=true, " +
            "or supply a storageLocationId.");
    }

    private static async Task<List<LivestockAnimalDto>> ReadAnimalsAsync(SqlCommand command)
    {
        List<LivestockAnimalDto> results = new List<LivestockAnimalDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new LivestockAnimalDto
            {
                Id                 = reader.GetGuid(0),
                HouseholdId        = reader.GetGuid(1),
                Name               = reader.GetString(2),
                AnimalType         = reader.GetString(3),
                ProductionCategory = reader.GetString(4),
                IsFlockOrHerd      = reader.GetBoolean(5),
                Count              = reader.GetInt32(6),
                AcquiredDate       = reader.IsDBNull(7) ? null
                    : DateOnly.FromDateTime(reader.GetDateTime(7)),
                BreedNotes         = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsActive           = reader.GetBoolean(9),
                Notes              = reader.IsDBNull(10) ? null : reader.GetString(10),
                CreatedAt          = reader.GetDateTime(11),
                UpdatedAt          = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            });
        }
        return results;
    }

    private static async Task<List<LivestockProductionDto>> ReadProductionAsync(SqlCommand command)
    {
        List<LivestockProductionDto> results = new List<LivestockProductionDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new LivestockProductionDto
            {
                Id               = reader.GetGuid(0),
                AnimalId         = reader.GetGuid(1),
                AnimalName       = reader.GetString(2),
                ProductionDate   = DateOnly.FromDateTime(reader.GetDateTime(3)),
                ProductType      = reader.GetString(4),
                Quantity         = reader.GetDecimal(5),
                Unit             = reader.GetString(6),
                AddedToInventory = reader.GetBoolean(7),
                InventoryItemId  = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                Notes            = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt        = reader.GetDateTime(10),
            });
        }
        return results;
    }

    private static async Task<List<LivestockHarvestDto>> ReadHarvestsAsync(SqlCommand command)
    {
        List<LivestockHarvestDto> results = new List<LivestockHarvestDto>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string? yieldJson = reader.IsDBNull(7) ? null : reader.GetString(7);
            List<HarvestYieldItem> yieldItems = string.IsNullOrWhiteSpace(yieldJson)
                ? new List<HarvestYieldItem>()
                : JsonSerializer.Deserialize<List<HarvestYieldItem>>(yieldJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? new List<HarvestYieldItem>();

            results.Add(new LivestockHarvestDto
            {
                Id                 = reader.GetGuid(0),
                AnimalId           = reader.GetGuid(1),
                AnimalName         = reader.GetString(2),
                HarvestDate        = DateOnly.FromDateTime(reader.GetDateTime(3)),
                CountHarvested     = reader.GetInt32(4),
                LiveWeightLbs      = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                ProcessedWeightLbs = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                YieldItems         = yieldItems,
                AddedToInventory   = reader.GetBoolean(8),
                ProcessedBy        = reader.IsDBNull(9) ? null : reader.GetString(9),
                Notes              = reader.IsDBNull(10) ? null : reader.GetString(10),
                CreatedAt          = reader.GetDateTime(11),
            });
        }
        return results;
    }
}
