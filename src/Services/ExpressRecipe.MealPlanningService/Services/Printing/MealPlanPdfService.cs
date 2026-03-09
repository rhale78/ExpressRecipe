using System.Net.Http.Json;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExpressRecipe.MealPlanningService.Services.Printing;

public interface IMealPlanPdfService
{
    Task<byte[]> GeneratePdfAsync(MealPlanPrintOptions options, Guid userId, CancellationToken ct = default);
    Task<MealPlanPrintData> AssemblePrintDataAsync(MealPlanPrintOptions options, Guid userId, CancellationToken ct = default);
}

public sealed record MealPlanPrintOptions
{
    public Guid MealPlanId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public bool IncludeRecipes { get; init; }
    public bool IncludeGroceryList { get; init; }
    public GroceryListGrouping GroceryGrouping { get; init; } = GroceryListGrouping.Aggregated;
}

public enum GroceryListGrouping { Aggregated, ByDay, ByMeal, ByStore }

public sealed class MealPlanPdfService : IMealPlanPdfService
{
    private readonly IMealPlanningRepository _plans;
    private readonly IHttpClientFactory _http;
    private readonly IHolidayService _holidays;
    private readonly ILogger<MealPlanPdfService> _logger;
    private const string RecipeServiceClient   = "RecipeService";
    private const string ShoppingServiceClient = "ShoppingService";
    private static readonly string AccentHex   = "#2D7D46";

    public MealPlanPdfService(IMealPlanningRepository plans, IHttpClientFactory http,
        IHolidayService holidays, ILogger<MealPlanPdfService> logger)
    {
        _plans = plans;
        _http = http;
        _holidays = holidays;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GeneratePdfAsync(MealPlanPrintOptions options, Guid userId, CancellationToken ct = default)
        => BuildPdf(await AssemblePrintDataAsync(options, userId, ct), options);

    public async Task<MealPlanPrintData> AssemblePrintDataAsync(MealPlanPrintOptions options, Guid userId, CancellationToken ct = default)
    {
        MealPlanDto? plan = await _plans.GetMealPlanByIdAsync(options.MealPlanId, ct);
        if (plan is null || plan.UserId != userId) { throw new KeyNotFoundException("Plan not found"); }

        DateOnly from = options.FromDate ?? DateOnly.FromDateTime(plan.StartDate);
        DateOnly to   = options.ToDate   ?? DateOnly.FromDateTime(plan.EndDate);

        List<PlannedMealDto> allMeals = await _plans.GetPlannedMealsAsync(
            options.MealPlanId, from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MaxValue));

        List<Guid> recipeIds = allMeals.Where(m => m.RecipeId.HasValue)
            .Select(m => m.RecipeId!.Value).Distinct().ToList();

        Dictionary<Guid, RecipePrintData> recipeData = options.IncludeRecipes
            ? await FetchRecipeDataAsync(recipeIds, ct) : new();

        // Always fetch recipe names so DisplayName is correct even when IncludeRecipes=false
        Dictionary<Guid, RecipePrintData> namesOnly = !options.IncludeRecipes && recipeIds.Count > 0
            ? await FetchRecipeDataAsync(recipeIds, ct) : recipeData;

        Dictionary<DateOnly, List<PlannedMealDto>> byDate = allMeals
            .GroupBy(m => DateOnly.FromDateTime(m.PlannedDate))
            .ToDictionary(g => g.Key, g => g.OrderBy(m => GetMealTypeOrder(m.MealType)).ToList());

        List<PrintDay> days = new();
        for (DateOnly d = from; d <= to; d = d.AddDays(1))
        {
            List<PlannedMealDto> dayMeals = byDate.TryGetValue(d, out List<PlannedMealDto>? dm) ? dm : new();
            days.Add(new PrintDay
            {
                Date         = d,
                HolidayLabel = _holidays.GetHolidayLabel(d),
                Meals        = dayMeals.Select(m =>
                {
                    namesOnly.TryGetValue(m.RecipeId ?? Guid.Empty, out RecipePrintData? recipe);
                    return new PrintMeal
                    {
                        MealType      = m.MealType,
                        DisplayName   = recipe?.Name ?? m.CustomMealName ?? "Custom Meal",
                        Servings      = m.Servings,
                        EstimatedCost = m.RecipeId.HasValue && recipe is not null ? recipe.EstimatedCost : null,
                        RecipeData    = options.IncludeRecipes && m.RecipeId.HasValue ? recipe : null
                    };
                }).ToList()
            });
        }

        return new MealPlanPrintData
        {
            PlanName        = plan.Name ?? "Meal Plan",
            FromDate        = from,
            ToDate          = to,
            Days            = days,
            Groceries       = options.IncludeGroceryList ? await AggregateIngredientsAsync(allMeals, options.GroceryGrouping, ct) : new(),
            GroceryGrouping = options.GroceryGrouping
        };
    }

    private async Task<Dictionary<Guid, RecipePrintData>> FetchRecipeDataAsync(List<Guid> recipeIds, CancellationToken ct)
    {
        HttpClient client = _http.CreateClient(RecipeServiceClient);
        Dictionary<Guid, RecipePrintData> result = new();
        using SemaphoreSlim sem = new(5);

        await Task.WhenAll(recipeIds.Select(async id =>
        {
            await sem.WaitAsync(ct);
            try
            {
                RecipeDetailResponse? detail = await client.GetFromJsonAsync<RecipeDetailResponse>($"/api/recipes/{id}", ct);
                if (detail is null) { return; }
                RecipePrintData printData = new()
                {
                    Name             = detail.Name,
                    Servings         = detail.Servings,
                    Ingredients      = detail.Ingredients.Select(FormatIngredient).ToList(),
                    Instructions     = detail.Instructions,
                    EstimatedCost    = detail.EstimatedCostPerServing.HasValue ? detail.EstimatedCostPerServing.Value * detail.Servings : null,
                    NutritionSummary = detail.Nutrition is null ? null
                        : $"{detail.Nutrition.Calories:F0} cal · {detail.Nutrition.Protein:F0}g protein · {detail.Nutrition.TotalCarbohydrates:F0}g carbs · {detail.Nutrition.TotalFat:F0}g fat"
                };
                lock (result) { result[id] = printData; }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not fetch recipe {RecipeId} for print", id); }
            finally { sem.Release(); }
        }));
        return result;
    }

    private static string FormatIngredient(RecipeIngredientResponse ing)
    {
        System.Text.StringBuilder sb = new();
        if (ing.Quantity.HasValue)
        {
            sb.Append(ExpressRecipe.Shared.Units.FractionFormatter.Format(ing.Quantity.Value, ExpressRecipe.Shared.Units.NumberFormat.Fraction)).Append(' ');
        }
        if (!string.IsNullOrEmpty(ing.Unit)) { sb.Append(ing.Unit).Append(' '); }
        sb.Append(ing.Name);
        if (!string.IsNullOrEmpty(ing.Notes)) { sb.Append(" (").Append(ing.Notes).Append(')'); }
        return sb.ToString();
    }

    private async Task<List<AggregatedIngredient>> AggregateIngredientsAsync(
        List<PlannedMealDto> meals, GroceryListGrouping grouping, CancellationToken ct)
    {
        HttpClient client = _http.CreateClient(ShoppingServiceClient);
        Dictionary<string, (decimal qty, string unit, string? date, string? mealType)> tally = new(StringComparer.OrdinalIgnoreCase);

        foreach (PlannedMealDto meal in meals.Where(m => m.RecipeId.HasValue))
        {
            List<ShoppingItemResponse>? items;
            try
            {
                items = await client.GetFromJsonAsync<List<ShoppingItemResponse>>(
                    $"/api/shopping/recipe-ingredients?recipeId={meal.RecipeId}&servings={meal.Servings}", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch shopping ingredients for recipe {RecipeId} — grocery list may be incomplete", meal.RecipeId);
                continue;
            }
            if (items is null) { continue; }

            string dateKey = DateOnly.FromDateTime(meal.PlannedDate).ToString("yyyy-MM-dd");
            string mealLabel = $"{dateKey} {meal.MealType}";
            foreach (ShoppingItemResponse item in items)
            {
                string key = grouping switch
                {
                    GroceryListGrouping.ByDay  => $"{dateKey}|{item.IngredientName}",
                    GroceryListGrouping.ByMeal => $"{mealLabel}|{item.IngredientName}",
                    _                          => item.IngredientName
                };
                if (tally.TryGetValue(key, out (decimal qty, string unit, string? date, string? mt) existing))
                {
                    tally[key] = (existing.qty + (item.Quantity ?? 0), item.Unit ?? string.Empty, dateKey, meal.MealType);
                }
                else
                {
                    tally[key] = (item.Quantity ?? 0, item.Unit ?? string.Empty, dateKey, meal.MealType);
                }
            }
        }

        return tally.Select(kvp =>
        {
            string displayName = kvp.Key.Contains('|') ? kvp.Key[(kvp.Key.LastIndexOf('|') + 1)..] : kvp.Key;
            return new AggregatedIngredient { Name = displayName, Quantity = kvp.Value.qty, Unit = kvp.Value.unit, GroupDate = kvp.Value.date, GroupMeal = kvp.Value.mealType };
        }).OrderBy(i => i.GroupDate).ThenBy(i => i.Name).ToList();
    }

    private static int GetMealTypeOrder(string mealType) => mealType switch
    {
        "Breakfast" => 0,
        "Brunch"    => 1,
        "Lunch"     => 2,
        "Snack"     => 3,
        "Dinner"    => 4,
        _           => 5
    };

    // ── QuestPDF Composition ──────────────────────────────────────────────────

    private static byte[] BuildPdf(MealPlanPrintData data, MealPlanPrintOptions options)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri));
                page.Header().Element(ComposeHeader(data));
                page.Content().Element(ComposeContent(data, options));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("ExpressRecipe  ·  ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.TotalPages().FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private static Action<IContainer> ComposeHeader(MealPlanPrintData data)
        => container => container.BorderBottom(1).BorderColor(AccentHex).PaddingBottom(4).Row(row =>
        {
            row.RelativeItem().Text(data.PlanName).FontSize(16).Bold().FontColor(AccentHex);
            row.ConstantItem(200).AlignRight().Text($"{data.FromDate:MMM dd} – {data.ToDate:MMM dd, yyyy}").FontSize(10).FontColor(Colors.Grey.Darken2);
        });

    private static Action<IContainer> ComposeContent(MealPlanPrintData data, MealPlanPrintOptions options)
    {
        return container => container.Column(col =>
        {
            bool firstDay = true;
            foreach (PrintDay day in data.Days)
            {
                if (!firstDay) { col.Item().PageBreak(); }
                firstDay = false;
                col.Item().PaddingTop(8).Text(txt =>
                {
                    txt.Span(day.Date.ToString("dddd, MMMM dd, yyyy")).Bold().FontSize(14);
                    if (!string.IsNullOrEmpty(day.HolidayLabel))
                    {
                        txt.Span($"  🎉 {day.HolidayLabel}").FontSize(10).FontColor(Colors.Orange.Darken2);
                    }
                });

                if (day.Meals.Count == 0)
                {
                    col.Item().PaddingTop(4).Text("No meals planned").FontColor(Colors.Grey.Medium).Italic();
                    continue;
                }

                foreach (PrintMeal meal in day.Meals)
                {
                    col.Item().PaddingTop(10).Background(Colors.Grey.Lighten4).Padding(6).Column(mc =>
                    {
                        mc.Item().Text($"{meal.MealType}: {meal.DisplayName}").Bold().FontSize(12);
                        if (meal.AttendeeNames.Count > 0)
                        {
                            mc.Item().Text($"👥 {string.Join(", ", meal.AttendeeNames)}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        }
                        if (meal.EstimatedCost.HasValue)
                        {
                            mc.Item().Text($"Est. cost: ${meal.EstimatedCost.Value:F2}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        }

                        if (options.IncludeRecipes && meal.RecipeData is not null)
                        {
                            mc.Item().PaddingTop(6).Text("Ingredients").Bold().FontSize(10);
                            mc.Item().Column(ic => { foreach (string ing in meal.RecipeData.Ingredients) { ic.Item().Text($"• {ing}").FontSize(9); } });
                            mc.Item().PaddingTop(6).Text("Instructions").Bold().FontSize(10);
                            mc.Item().Column(ic =>
                            {
                                int step = 1;
                                foreach (string inst in meal.RecipeData.Instructions) { ic.Item().Text($"{step++}. {inst}").FontSize(9); }
                            });
                            if (!string.IsNullOrEmpty(meal.RecipeData.NutritionSummary))
                            {
                                mc.Item().PaddingTop(4).Text(meal.RecipeData.NutritionSummary).FontSize(8).FontColor(Colors.Grey.Darken2);
                            }
                        }
                    });
                }
            }

            if (options.IncludeGroceryList && data.Groceries.Count > 0)
            {
                col.Item().PageBreak();
                col.Item().Text("Grocery List").Bold().FontSize(16).FontColor(AccentHex);
                IEnumerable<IGrouping<string, AggregatedIngredient>> groups = data.GroceryGrouping switch
                {
                    GroceryListGrouping.ByDay  => data.Groceries.GroupBy(i => i.GroupDate ?? "Other"),
                    GroceryListGrouping.ByMeal => data.Groceries.GroupBy(i => i.GroupMeal != null && i.GroupDate != null ? $"{i.GroupDate} {i.GroupMeal}" : i.GroupDate ?? "Other"),
                    _                          => data.Groceries.GroupBy(_ => "All Items")
                };

                foreach (IGrouping<string, AggregatedIngredient> group in groups)
                {
                    if (data.GroceryGrouping != GroceryListGrouping.Aggregated)
                    {
                        col.Item().PaddingTop(8).Text(group.Key).Bold().FontSize(11);
                    }
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(1); cols.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Ingredient").Bold(); h.Cell().Text("Qty").Bold(); h.Cell().Text("Unit").Bold(); });
                        foreach (AggregatedIngredient item in group.OrderBy(i => i.Name))
                        {
                            table.Cell().Text(item.Name);
                            table.Cell().Text(item.Quantity.ToString("G4"));
                            table.Cell().Text(item.Unit);
                        }
                    });
                }
            }
        });
    }
}

// ── Supporting Records ────────────────────────────────────────────────────────
public sealed record MealPlanPrintData
{
    public string PlanName { get; init; } = string.Empty;
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public List<PrintDay> Days { get; init; } = new();
    public List<AggregatedIngredient> Groceries { get; init; } = new();
    public GroceryListGrouping GroceryGrouping { get; init; }
}

public sealed record PrintDay
{
    public DateOnly Date { get; init; }
    public string? HolidayLabel { get; init; }
    public List<PrintMeal> Meals { get; init; } = new();
}

public sealed record PrintMeal
{
    public string MealType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int Servings { get; init; }
    public List<string> AttendeeNames { get; init; } = new();
    public decimal? BudgetTarget { get; init; }
    public decimal? EstimatedCost { get; init; }
    public RecipePrintData? RecipeData { get; init; }
}

public sealed record RecipePrintData
{
    public string Name { get; init; } = string.Empty;
    public int Servings { get; init; }
    public List<string> Ingredients { get; init; } = new();
    public List<string> Instructions { get; init; } = new();
    public string? NutritionSummary { get; init; }
    public decimal? EstimatedCost { get; init; }
}

public sealed record AggregatedIngredient
{
    public string Name { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string? GroupDate { get; init; }
    public string? GroupMeal { get; init; }
}

// HTTP response shapes (internal to this service)
internal sealed record RecipeDetailResponse
{
    public string Name { get; init; } = string.Empty;
    public int Servings { get; init; }
    public decimal? EstimatedCostPerServing { get; init; }
    public List<RecipeIngredientResponse> Ingredients { get; init; } = new();
    public List<string> Instructions { get; init; } = new();
    public RecipeNutritionResponse? Nutrition { get; init; }
}

internal sealed record RecipeIngredientResponse
{
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

internal sealed record RecipeNutritionResponse
{
    public decimal Calories { get; init; }
    public decimal Protein { get; init; }
    public decimal TotalCarbohydrates { get; init; }
    public decimal TotalFat { get; init; }
}

internal sealed record ShoppingItemResponse
{
    public string IngredientName { get; init; } = string.Empty;
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }
}
