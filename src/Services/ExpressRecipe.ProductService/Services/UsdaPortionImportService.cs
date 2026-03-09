using CsvHelper;
using CsvHelper.Configuration;
using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Units;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Imports USDA FoodData Central food_portion.csv into the IngredientUnitDensity table.
/// Run via IHostedService (table empty) or POST /api/admin/import/usda-portions.
/// Safe to re-run (UPSERT is idempotent).
/// </summary>
public sealed class UsdaPortionImportService : SqlHelper
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UsdaPortionImportService> _logger;

    public UsdaPortionImportService(
        string connectionString,
        IConfiguration configuration,
        ILogger<UsdaPortionImportService> logger) : base(connectionString)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Returns the count of rows in IngredientUnitDensity.</summary>
    public async Task<int> GetDensityRowCountAsync()
    {
        object? scalar = await ExecuteScalarAsync<object>("SELECT COUNT(*) FROM IngredientUnitDensity");
        return scalar is not null ? Convert.ToInt32(scalar) : 0;
    }

    /// <summary>Runs the import. Returns number of rows upserted.</summary>
    public async Task<int> RunImportAsync(CancellationToken ct = default)
    {
        string? csvPath = _configuration["UsdaImport:PortionCsvPath"];
        if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
        {
            _logger.LogWarning("USDA portion CSV not found at '{Path}'. Skipping USDA portion import.", csvPath);
            return 0;
        }

        _logger.LogInformation("Starting USDA portion import from {Path}", csvPath);
        int upserted = 0;

        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using StreamReader reader = new(csvPath);
        using CsvReader csv = new(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        List<PortionRow> rows = new();
        while (await csv.ReadAsync())
        {
            PortionRow row = new()
            {
                FdcId        = csv.GetField<int>("fdc_id"),
                Amount       = csv.GetField<decimal>("amount"),
                Description  = csv.GetField<string?>("measure_description") ?? string.Empty,
                GramWeight   = csv.GetField<decimal>("gram_weight")
            };
            rows.Add(row);
        }

        foreach (PortionRow row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (row.GramWeight <= 0m) { continue; }

            (decimal amount, UnitCode unitCode) = UnitParser.Parse($"{row.Amount} {row.Description}");
            if (unitCode == UnitCode.Unknown || unitCode == UnitCode.ToTaste
                || UnitParser.GetDimension(unitCode) != UnitDimension.Volume)
            {
                continue; // Only volume → mass conversions yield g/ml
            }

            decimal ml;
            try
            {
                ml = UnitConverter.ToMilliliters(amount == 0m ? 1m : amount, unitCode);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (ml <= 0m) { continue; }
            decimal gramsPerMl = row.GramWeight / ml;

            // UPSERT by UsdaFdcId + measure_description
            string ingredientName = $"USDA:{row.FdcId}";
            string? prepNote = row.Description.Length > 100
                ? row.Description[..100]
                : (string.IsNullOrWhiteSpace(row.Description) ? null : row.Description);

            try
            {
                await UpsertDensityAsync(ingredientName, prepNote, gramsPerMl, row.FdcId);
                upserted++;
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "Skipping density row for FDC {FdcId}: {Description}", row.FdcId, row.Description);
            }
        }

        _logger.LogInformation("USDA portion import complete. Upserted {Count} rows.", upserted);
        return upserted;
    }

    private async Task UpsertDensityAsync(
        string ingredientName, string? preparationNote, decimal gramsPerMl, int usdaFdcId)
    {
        const string sql = @"
            MERGE IngredientUnitDensity AS target
            USING (SELECT @IngredientName AS IngredientName, @PreparationNote AS PreparationNote) AS src
            ON (target.IngredientName = src.IngredientName
                AND (target.PreparationNote = src.PreparationNote
                     OR (target.PreparationNote IS NULL AND src.PreparationNote IS NULL)))
            WHEN MATCHED THEN
                UPDATE SET GramsPerMl = @GramsPerMl,
                           UsdaFdcId  = @UsdaFdcId,
                           UpdatedAt  = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Id, IngredientName, PreparationNote, GramsPerMl, Source, UsdaFdcId, IsVerified, CreatedAt)
                VALUES (NEWID(), @IngredientName, @PreparationNote, @GramsPerMl, 'USDA', @UsdaFdcId, 0, GETUTCDATE());";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@IngredientName", ingredientName),
            CreateParameter("@PreparationNote", (object?)preparationNote ?? DBNull.Value),
            CreateParameter("@GramsPerMl", gramsPerMl),
            CreateParameter("@UsdaFdcId", usdaFdcId));
    }

    private sealed class PortionRow
    {
        public int FdcId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal GramWeight { get; set; }
    }
}

/// <summary>
/// Hosted service that triggers USDA import on startup if the density table is empty.
/// </summary>
public sealed class UsdaPortionImportWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UsdaPortionImportWorker> _logger;

    public UsdaPortionImportWorker(IServiceScopeFactory scopeFactory, ILogger<UsdaPortionImportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // Wait for migrations

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        UsdaPortionImportService svc = scope.ServiceProvider.GetRequiredService<UsdaPortionImportService>();

        try
        {
            int count = await svc.GetDensityRowCountAsync();
            if (count == 0)
            {
                _logger.LogInformation("IngredientUnitDensity is empty — starting USDA portion import.");
                await svc.RunImportAsync(stoppingToken);
            }
            else
            {
                _logger.LogDebug("IngredientUnitDensity has {Count} rows — skipping USDA auto-import.", count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during USDA portion auto-import.");
        }
    }
}
