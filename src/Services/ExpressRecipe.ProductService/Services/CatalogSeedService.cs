using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using System.Text.Json;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Loads food group seed data from food_group_seeds.json into the database
/// on first startup when the FoodGroup table is empty.
/// </summary>
public class CatalogSeedService
{
    private readonly IFoodCatalogRepository _catalog;
    private readonly ILogger<CatalogSeedService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public CatalogSeedService(IFoodCatalogRepository catalog, ILogger<CatalogSeedService> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task SeedIfEmptyAsync(CancellationToken ct = default)
    {
        int existingCount = await _catalog.GetFoodGroupCountAsync(ct);
        if (existingCount > 0)
        {
            _logger.LogDebug("[CatalogSeed] FoodGroup table already has {Count} rows – skipping seed", existingCount);
            return;
        }

        _logger.LogInformation("[CatalogSeed] FoodGroup table is empty – loading seed data");

        string seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seeds", "food_group_seeds.json");
        if (!File.Exists(seedPath))
        {
            _logger.LogWarning("[CatalogSeed] Seed file not found at {Path}", seedPath);
            return;
        }

        List<FoodGroupSeedEntry>? entries;
        try
        {
            await using FileStream fs = File.OpenRead(seedPath);
            entries = await JsonSerializer.DeserializeAsync<List<FoodGroupSeedEntry>>(fs, JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CatalogSeed] Failed to deserialize seed file");
            return;
        }

        if (entries == null || entries.Count == 0)
        {
            _logger.LogWarning("[CatalogSeed] Seed file contained no entries");
            return;
        }

        int groupsCreated = 0;
        int membersCreated = 0;

        foreach (FoodGroupSeedEntry entry in entries)
        {
            try
            {
                Guid groupId = await _catalog.CreateFoodGroupAsync(
                    new FoodGroupRecord(entry.Name, entry.Description, entry.FunctionalRole), ct);
                groupsCreated++;

                if (entry.Members == null)
                {
                    continue;
                }

                foreach (FoodGroupMemberSeedEntry member in entry.Members)
                {
                    string? allergenJson = member.AllergenFree != null && member.AllergenFree.Length > 0
                        ? JsonSerializer.Serialize(member.AllergenFree)
                        : null;

                    await _catalog.AddFoodGroupMemberAsync(
                        new FoodGroupMemberRecord(
                            FoodGroupId: groupId,
                            IngredientId: null,
                            ProductId: null,
                            CustomName: member.CustomName,
                            SubstitutionRatio: member.SubstitutionRatio,
                            SubstitutionNotes: member.SubstitutionNotes,
                            BestFor: member.BestFor,
                            NotSuitableFor: member.NotSuitableFor,
                            RankOrder: member.RankOrder,
                            AllergenFreeJson: allergenJson,
                            IsHomemadeRecipeAvailable: member.IsHomemadeRecipeAvailable,
                            HomemadeRecipeId: null), ct);
                    membersCreated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogSeed] Failed to seed food group '{Name}'", entry.Name);
            }
        }

        _logger.LogInformation("[CatalogSeed] Seeded {Groups} food groups and {Members} members",
            groupsCreated, membersCreated);
    }

    // -----------------------------------------------------------------------
    // Internal seed models – only used during deserialization
    // -----------------------------------------------------------------------

    private sealed class FoodGroupSeedEntry
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? FunctionalRole { get; set; }
        public FoodGroupMemberSeedEntry[]? Members { get; set; }
    }

    private sealed class FoodGroupMemberSeedEntry
    {
        public string? CustomName { get; set; }
        public string? SubstitutionRatio { get; set; }
        public string? SubstitutionNotes { get; set; }
        public string? BestFor { get; set; }
        public string? NotSuitableFor { get; set; }
        public int RankOrder { get; set; } = 1;
        public string[]? AllergenFree { get; set; }
        public bool IsHomemadeRecipeAvailable { get; set; }
    }
}
