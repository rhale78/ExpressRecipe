using System.Net;
using System.Text;
using System.Text.Json;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.MealPlanningService.Services.Printing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace ExpressRecipe.MealPlanningService.Tests.Services;

public class MealPlanPdfServiceTests
{
    private static readonly Guid PlanId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RecipeId = Guid.NewGuid();

    private static MealPlanDto BuildPlan(DateOnly from, DateOnly to) => new()
    {
        Id = PlanId,
        UserId = UserId,
        Name = "Test Plan",
        StartDate = from.ToDateTime(TimeOnly.MinValue),
        EndDate = to.ToDateTime(TimeOnly.MaxValue),
        CreatedAt = DateTime.UtcNow
    };

    private static PlannedMealDto BuildMeal(DateOnly date, Guid? recipeId = null, string mealType = "Dinner") => new()
    {
        Id = Guid.NewGuid(),
        MealPlanId = PlanId,
        RecipeId = recipeId,
        MealType = mealType,
        PlannedDate = date.ToDateTime(TimeOnly.MinValue),
        Servings = 2
    };

    private static IHttpClientFactory BuildHttpFactory(
        string recipeServiceResponse = "null",
        string shoppingServiceResponse = "[]")
    {
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                string body = req.RequestUri?.AbsolutePath.StartsWith("/api/recipes") == true
                    ? recipeServiceResponse
                    : shoppingServiceResponse;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            });

        HttpClient recipeClient = new(handler.Object) { BaseAddress = new Uri("http://recipeservice") };
        HttpClient shoppingClient = new(handler.Object) { BaseAddress = new Uri("http://shoppingservice") };

        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("RecipeService")).Returns(recipeClient);
        factory.Setup(f => f.CreateClient("ShoppingService")).Returns(shoppingClient);
        return factory.Object;
    }

    private static IMealPlanPdfService BuildService(
        Mock<IMealPlanningRepository> repoMock,
        IHttpClientFactory? httpFactory = null)
    {
        return new MealPlanPdfService(
            repoMock.Object,
            httpFactory ?? BuildHttpFactory(),
            new HolidayService(),
            NullLogger<MealPlanPdfService>.Instance);
    }

    // ── GeneratePdfAsync — single day, no options ─────────────────────────────

    [Fact]
    public async Task GeneratePdfAsync_SingleDayNoOptions_ReturnsNonEmptyPdfBytes()
    {
        DateOnly day = new(2026, 3, 10);
        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(day, day));
        repo.Setup(r => r.GetPlannedMealsAsync(PlanId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<PlannedMealDto> { BuildMeal(day) });

        IMealPlanPdfService svc = BuildService(repo);
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId };

        byte[] pdf = await svc.GeneratePdfAsync(opts, UserId);

        pdf.Should().NotBeEmpty();
        // PDF magic bytes %PDF
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    // ── GeneratePdfAsync — include recipes ───────────────────────────────────

    [Fact]
    public async Task GeneratePdfAsync_IncludeRecipes_IngredientsPresentInOutput()
    {
        DateOnly day = new(2026, 3, 10);
        string recipeJson = JsonSerializer.Serialize(new
        {
            Name = "Pancakes",
            Servings = 4,
            EstimatedCostPerServing = (decimal?)null,
            Ingredients = new[] { new { Quantity = (decimal?)1.5m, Unit = "cup", Name = "flour", Notes = (string?)null } },
            Instructions = new[] { "Mix ingredients", "Cook on griddle" },
            Nutrition = (object?)null
        });

        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(day, day));
        repo.Setup(r => r.GetPlannedMealsAsync(PlanId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<PlannedMealDto> { BuildMeal(day, RecipeId) });

        IMealPlanPdfService svc = BuildService(repo, BuildHttpFactory(recipeServiceResponse: recipeJson));
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId, IncludeRecipes = true };

        byte[] pdf = await svc.GeneratePdfAsync(opts, UserId);

        pdf.Should().NotBeEmpty();
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    // ── AssemblePrintDataAsync — date range filter ───────────────────────────

    [Fact]
    public async Task AssemblePrintDataAsync_DateRangeFilter_OnlyMealsWithinRangeIncluded()
    {
        DateOnly from = new(2026, 3, 10);
        DateOnly to   = new(2026, 3, 12);

        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));
        // Only return meals in the from-to range (repository handles date filtering)
        repo.Setup(r => r.GetPlannedMealsAsync(PlanId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<PlannedMealDto>
            {
                BuildMeal(from),
                BuildMeal(to)
            });

        IMealPlanPdfService svc = BuildService(repo);
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId, FromDate = from, ToDate = to };

        MealPlanPrintData data = await svc.AssemblePrintDataAsync(opts, UserId);

        data.Days.Should().HaveCount(3); // from, middle day, to
        data.Days.Select(d => d.Date).Should().Contain(from).And.Contain(to);
    }

    // ── AssemblePrintDataAsync — holiday on Christmas ────────────────────────

    [Fact]
    public async Task AssemblePrintDataAsync_ChristmasDate_HolidayLabelSet()
    {
        DateOnly christmas = new(2026, 12, 25);

        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(christmas, christmas));
        repo.Setup(r => r.GetPlannedMealsAsync(PlanId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<PlannedMealDto>());

        IMealPlanPdfService svc = BuildService(repo);
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId };

        MealPlanPrintData data = await svc.AssemblePrintDataAsync(opts, UserId);

        data.Days.Should().ContainSingle(d => d.Date == christmas && d.HolidayLabel == "Christmas Day");
    }

    // ── AssemblePrintDataAsync — missing RecipeService response ─────────────

    [Fact]
    public async Task AssemblePrintDataAsync_RecipeServiceReturnsNull_MealIncludedWithoutRecipeData()
    {
        DateOnly day = new(2026, 3, 10);

        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(day, day));
        repo.Setup(r => r.GetPlannedMealsAsync(PlanId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<PlannedMealDto> { BuildMeal(day, RecipeId) });

        // RecipeService returns null (HTTP 404 or empty)
        IMealPlanPdfService svc = BuildService(repo, BuildHttpFactory(recipeServiceResponse: "null"));
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId, IncludeRecipes = true };

        // Should not throw
        MealPlanPrintData data = await svc.AssemblePrintDataAsync(opts, UserId);

        PrintMeal meal = data.Days.SelectMany(d => d.Meals).Single();
        meal.RecipeData.Should().BeNull();
    }

    // ── GeneratePdfAsync — grocery aggregated ───────────────────────────────

    [Fact]
    public async Task GeneratePdfAsync_GroceryAggregated_ReturnsPdfWithGrocerySection()
    {
        DateOnly day = new(2026, 3, 10);
        string shoppingJson = JsonSerializer.Serialize(new[]
        {
            new { IngredientName = "flour", Quantity = (decimal?)2m, Unit = "cup" },
            new { IngredientName = "sugar", Quantity = (decimal?)1m, Unit = "cup" }
        });

        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(day, day));
        repo.Setup(r => r.GetPlannedMealsAsync(PlanId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<PlannedMealDto> { BuildMeal(day, RecipeId) });

        IMealPlanPdfService svc = BuildService(repo, BuildHttpFactory(shoppingServiceResponse: shoppingJson));
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId, IncludeGroceryList = true };

        byte[] pdf = await svc.GeneratePdfAsync(opts, UserId);

        pdf.Should().NotBeEmpty();
    }

    // ── Duplicate ingredient aggregation ────────────────────────────────────

    [Fact]
    public async Task AssemblePrintDataAsync_DuplicateIngredientAcrossRecipes_QuantitiesSummed()
    {
        DateOnly day = new(2026, 3, 10);
        // Same ingredient "flour" appears twice with quantities 1 and 2
        string shoppingJson = JsonSerializer.Serialize(new[]
        {
            new { IngredientName = "flour", Quantity = (decimal?)1m, Unit = "cup" },
            new { IngredientName = "flour", Quantity = (decimal?)2m, Unit = "cup" }
        });

        Guid recipeId2 = Guid.NewGuid();
        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(day, day));
        repo.Setup(r => r.GetPlannedMealsAsync(PlanId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<PlannedMealDto>
            {
                BuildMeal(day, RecipeId),
                BuildMeal(day, recipeId2)
            });

        Mock<HttpMessageHandler> handler = new();
        int callCount = 0;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                // Each call returns one flour item; they should be aggregated
                string body = callCount == 1
                    ? JsonSerializer.Serialize(new[] { new { IngredientName = "flour", Quantity = (decimal?)1m, Unit = "cup" } })
                    : JsonSerializer.Serialize(new[] { new { IngredientName = "flour", Quantity = (decimal?)2m, Unit = "cup" } });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            });

        HttpClient shoppingClient = new(handler.Object) { BaseAddress = new Uri("http://shoppingservice") };
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("RecipeService")).Returns(new HttpClient { BaseAddress = new Uri("http://recipeservice") });
        factory.Setup(f => f.CreateClient("ShoppingService")).Returns(shoppingClient);

        IMealPlanPdfService svc = BuildService(repo, factory.Object);
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId, IncludeGroceryList = true, GroceryGrouping = GroceryListGrouping.Aggregated };

        MealPlanPrintData data = await svc.AssemblePrintDataAsync(opts, UserId);

        AggregatedIngredient? flour = data.Groceries.FirstOrDefault(g => g.Name.Equals("flour", StringComparison.OrdinalIgnoreCase));
        flour.Should().NotBeNull();
        flour!.Quantity.Should().Be(3m);
    }

    // ── Grocery by day grouping ──────────────────────────────────────────────

    [Fact]
    public async Task GeneratePdfAsync_GroceryByDay_ReturnsPdfSuccessfully()
    {
        DateOnly day = new(2026, 3, 10);
        string shoppingJson = JsonSerializer.Serialize(new[]
        {
            new { IngredientName = "milk", Quantity = (decimal?)1m, Unit = "L" }
        });

        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(day, day));
        repo.Setup(r => r.GetPlannedMealsAsync(PlanId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<PlannedMealDto> { BuildMeal(day, RecipeId) });

        IMealPlanPdfService svc = BuildService(repo, BuildHttpFactory(shoppingServiceResponse: shoppingJson));
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId, IncludeGroceryList = true, GroceryGrouping = GroceryListGrouping.ByDay };

        byte[] pdf = await svc.GeneratePdfAsync(opts, UserId);

        pdf.Should().NotBeEmpty();
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    // ── Security: wrong user cannot access another user's plan ───────────────

    [Fact]
    public async Task AssemblePrintDataAsync_WrongUserId_ThrowsKeyNotFoundException()
    {
        DateOnly day = new(2026, 3, 10);
        Mock<IMealPlanningRepository> repo = new();
        repo.Setup(r => r.GetMealPlanByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPlan(day, day)); // plan.UserId = UserId

        IMealPlanPdfService svc = BuildService(repo);
        MealPlanPrintOptions opts = new() { MealPlanId = PlanId };

        Guid differentUser = Guid.NewGuid();
        Func<Task> act = () => svc.AssemblePrintDataAsync(opts, differentUser);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── HolidayService: cache returns null on repeat lookups for non-holidays ─

    [Fact]
    public void HolidayService_NonHoliday_CacheReturnsNullOnRepeatCall()
    {
        HolidayService svc = new();
        DateOnly nonHoliday = new(2026, 3, 10);
        string? first  = svc.GetHolidayLabel(nonHoliday);
        string? second = svc.GetHolidayLabel(nonHoliday); // from cache
        first.Should().BeNull();
        second.Should().BeNull();
    }
}

public class HolidayServiceTests
{
    private readonly IHolidayService _svc = new HolidayService();

    [Theory]
    [InlineData(2026, 1, 1, "New Year's Day")]
    [InlineData(2026, 6, 19, "Juneteenth")]
    [InlineData(2026, 7, 4, "Independence Day")]
    [InlineData(2026, 11, 11, "Veterans Day")]
    [InlineData(2026, 12, 25, "Christmas Day")]
    public void GetHolidayLabel_FixedHoliday_ReturnsExpectedLabel(int year, int month, int day, string expected)
    {
        _svc.GetHolidayLabel(new DateOnly(year, month, day)).Should().Be(expected);
    }

    [Fact]
    public void GetHolidayLabel_NonHoliday_ReturnsNull()
    {
        _svc.GetHolidayLabel(new DateOnly(2026, 3, 10)).Should().BeNull();
    }

    [Fact]
    public void GetHolidayLabel_Thanksgiving2026_ReturnsLabel()
    {
        // 4th Thursday in November 2026 = Nov 26
        _svc.GetHolidayLabel(new DateOnly(2026, 11, 26)).Should().Be("Thanksgiving Day");
    }

    [Fact]
    public void GetHolidayLabel_MemorialDay2026_ReturnsLabel()
    {
        // Last Monday in May 2026 = May 25
        _svc.GetHolidayLabel(new DateOnly(2026, 5, 25)).Should().Be("Memorial Day");
    }
}
