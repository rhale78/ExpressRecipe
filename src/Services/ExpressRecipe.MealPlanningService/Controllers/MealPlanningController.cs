using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Claims;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Logging;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.MealPlanningService.Services.Printing;
using ExpressRecipe.MealPlanningService.Services.GoogleCalendar;

namespace ExpressRecipe.MealPlanningService.Controllers;

[Authorize]
[ApiController]
[Route("api/mealplan")]
public class MealPlanningController : ControllerBase
{
    private readonly ILogger<MealPlanningController> _logger;
    private readonly IMealPlanningRepository _repository;
    private readonly IMealSuggestionService _suggestionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly INutritionLogRepository _nutritionLogRepo;
    private readonly INutritionLoggingService _nutritionLoggingService;
    private readonly ICookingHistoryRepository _cookingHistoryRepo;
    private readonly IMealCourseRepository _courseRepo;
    private readonly IMealAttendeeRepository _attendeeRepo;
    private readonly IMealPlanCopyService _copyService;
    private readonly IMealPlanTemplateService _templateService;

    private readonly IMealPlanHistoryService _history;
    private readonly IMealVotingRepository _voting;


    private readonly IMealPlanPdfService _pdfService;

    private readonly IMealScheduleConfigRepository _scheduleRepo;
    private readonly IGoogleCalendarService _googleCal;
    private readonly IHolidayService _holidays;

    public MealPlanningController(
        ILogger<MealPlanningController> logger,
        IMealPlanningRepository repository,
        IMealSuggestionService suggestionService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        INutritionLogRepository nutritionLogRepo,
        INutritionLoggingService nutritionLoggingService,
        ICookingHistoryRepository cookingHistoryRepo,
        IMealCourseRepository courseRepo,
        IMealAttendeeRepository attendeeRepo,
        IMealPlanCopyService copyService,
        IMealPlanTemplateService templateService,
        IMealPlanHistoryService history, IMealVotingRepository voting,
        IMealPlanPdfService pdfService,
        IMealScheduleConfigRepository scheduleRepo,
        IGoogleCalendarService googleCal,
        IHolidayService holidays)
    {
        _logger = logger;
        _repository = repository;
        _suggestionService = suggestionService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _nutritionLogRepo = nutritionLogRepo;
        _nutritionLoggingService = nutritionLoggingService;
        _cookingHistoryRepo = cookingHistoryRepo;
        _courseRepo = courseRepo;
        _attendeeRepo = attendeeRepo;
        _copyService = copyService;
        _templateService = templateService;
        _history = history;
        _voting = voting;
        _pdfService = pdfService;
        _scheduleRepo = scheduleRepo;
        _googleCal    = googleCal;
        _holidays     = holidays;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Meal Plans ────────────────────────────────────────────────────────────

    [HttpPost("plans")]
    public async Task<IActionResult> CreateMealPlan([FromBody] CreatePlanRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        _logger.LogCreatingMealPlan(userId, request.StartDate, request.EndDate);
        Guid planId = await _repository.CreateMealPlanAsync(userId, request.StartDate, request.EndDate, request.Name, ct);
        MealPlanDto? plan = await _repository.GetMealPlanAsync(planId, userId, ct);
        return CreatedAtAction(nameof(GetMealPlan), new { id = planId }, plan);
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetMealPlans(CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        _logger.LogGettingMealPlans(userId);
        List<MealPlanDto> plans = await _repository.GetUserMealPlansAsync(userId, ct);
        return Ok(plans);
    }

    [HttpGet("plans/{id}")]
    public async Task<IActionResult> GetMealPlan(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        _logger.LogGettingMealPlan(userId, id);
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId, ct);
        if (plan == null) return NotFound();
        return Ok(plan);
    }

    [HttpDelete("plans/{id}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        await _repository.DeleteMealPlanAsync(id, userId, ct);
        _logger.LogMealPlanDeleted(userId, id);
        return NoContent();
    }

    // ── Summary / Search / Quick-plan ─────────────────────────────────────────

    [HttpGet("summary")]
    public async Task<IActionResult> GetPlanSummary(CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        List<MealPlanDto> plans = await _repository.GetUserMealPlansAsync(userId, ct);
        var now = DateTime.UtcNow;
        var activePlans = plans.Where(p => p.StartDate <= now && p.EndDate >= now).ToList();
        return Ok(new
        {
            TotalPlans = plans.Count,
            ActivePlans = activePlans.Count,
            NextMealPlanName = plans
                .Where(p => p.StartDate >= now)
                .OrderBy(p => p.StartDate)
                .FirstOrDefault()?.Name
        });
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchPlans([FromBody] SearchPlansRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        List<MealPlanDto> plans = await _repository.GetUserMealPlansAsync(userId, ct);
        if (!string.IsNullOrEmpty(request.Query))
            plans = plans.Where(p => p.Name?.Contains(request.Query, StringComparison.OrdinalIgnoreCase) == true).ToList();
        var total = plans.Count;
        var page = Math.Max(1, request.Page);
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        plans = plans.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { Plans = plans, Total = total, Page = page, PageSize = pageSize });
    }

    [HttpPost("quick-plan")]
    public async Task<IActionResult> CreateQuickPlan([FromBody] QuickPlanRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        var startDate = request.StartDate?.Date ?? DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(request.DurationDays > 0 ? request.DurationDays - 1 : 6);
        var name = string.IsNullOrEmpty(request.Name) ? $"Week of {startDate:MMM dd}" : request.Name;
        Guid planId = await _repository.CreateMealPlanAsync(userId, startDate, endDate, name, ct);
        MealPlanDto? plan = await _repository.GetMealPlanAsync(planId, userId, ct);
        return CreatedAtAction(nameof(GetMealPlan), new { id = planId }, plan);
    }

    // ── Update / Extended Plan Views ──────────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMealPlan(Guid id, [FromBody] CreatePlanRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId, ct);
        if (plan == null) return NotFound();
        // Full update not yet wired to repository; returns current plan until migration adds UPDATE support.
        return Ok(plan);
    }

    [HttpGet("{id}/calendar")]
    public async Task<IActionResult> GetPlanCalendar(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId, ct);
        if (plan == null) return NotFound();
        var year = plan.StartDate.Year;
        var month = plan.StartDate.Month;
        List<MealPlanCalendarDay> days = await _repository.GetCalendarAsync(userId, year, month, ct);
        return Ok(new { Plan = plan, Days = days });
    }

    [HttpGet("week/{date}")]
    public async Task<IActionResult> GetWeekView(DateTime date, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        var weekStart = date.Date;
        var weekEnd = weekStart.AddDays(7);
        List<MealPlanDto> plans = await _repository.GetUserMealPlansAsync(userId, ct);
        var weekPlans = plans.Where(p => p.StartDate <= weekEnd && p.EndDate >= weekStart).ToList();
        return Ok(new { WeekStart = weekStart, WeekEnd = weekEnd, Plans = weekPlans });
    }

    [HttpGet("{id}/nutrition")]
    public async Task<IActionResult> GetPlanNutrition(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId, ct);
        if (plan == null) return NotFound();
        NutritionSummaryDto summary = await _repository.GetNutritionSummaryAsync(userId, plan.StartDate, ct);
        return Ok(summary);
    }

    // ── Entries (alias routes) ────────────────────────────────────────────────

    [HttpPut("entries/{id}")]
    public async Task<IActionResult> UpdateMealEntry(Guid id, [FromBody] UpdateEntryRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        bool canAccess = await _repository.UserCanAccessPlannedMealAsync(id, userId, ct);
        if (!canAccess) return Forbid();
        await _repository.UpdatePlannedMealAsync(id, request.Date, request.MealType, request.Servings, ct);
        return NoContent();
    }

    [HttpDelete("entries/{id}")]
    public async Task<IActionResult> DeleteMealEntry(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        bool canAccess = await _repository.UserCanAccessPlannedMealAsync(id, userId, ct);
        if (!canAccess) return Forbid();
        await _repository.RemovePlannedMealAsync(id, ct);
        return NoContent();
    }

    // ── Plan Status Operations ────────────────────────────────────────────────

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> SetPlanComplete(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId, ct);
        if (plan == null) return NotFound();
        // Status update not yet in repository - returns 200 to unblock UI.
        return Ok(new { id, status = "Completed" });
    }

    [HttpPost("{id}/archive")]
    public async Task<IActionResult> ArchivePlan(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId, ct);
        if (plan == null) return NotFound();
        // Status update not yet in repository - returns 200 to unblock UI.
        return Ok(new { id, status = "Archived" });
    }

    // ── Planned Meals ──────────────────────────────────────────────────────────

    [HttpPost("plans/{id}/meals")]
    public async Task<IActionResult> AddPlannedMeal(Guid id, [FromBody] AddMealRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        _logger.LogAddingPlannedMeal(userId, id, request.RecipeId.GetValueOrDefault());
        Guid mealId = await _repository.AddPlannedMealAsync(
            id, userId, request.RecipeId, request.PlannedDate, request.MealType, request.Servings, ct);
        return Ok(new { id = mealId });
    }

    [HttpGet("plans/{id}/meals")]
    public async Task<IActionResult> GetPlannedMeals(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct = default)
    {
        _logger.LogGettingPlannedMeals(id);
        List<PlannedMealDto> meals = await _repository.GetPlannedMealsAsync(id, startDate, endDate, ct);
        return Ok(meals);
    }


    [HttpPost("plans/{id}/meals/{mealId}/complete")]
    public async Task<IActionResult> CompletePlannedMeal(Guid id, Guid mealId, [FromBody] CompleteMealRequest? request = null, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        _logger.LogCompletingPlannedMeal(userId, id, mealId);

        PlannedMealDto? meal = await _repository.GetPlannedMealByIdAsync(mealId, ct);
        if (meal == null) return NotFound();

        // Verify the meal belongs to the plan in the route and to the authenticated user
        if (meal.MealPlanId != id) return NotFound();
        MealPlanDto? plan = await _repository.GetMealPlanAsync(meal.MealPlanId, userId);
        if (plan == null) return Forbid();

        await _repository.MarkMealAsCompletedAsync(mealId);

        // RecipeName is not stored on PlannedMeal; use caller-supplied name when available.
        string recipeName = request?.RecipeName ?? meal.RecipeName;

        // Create a cooking history row from the planned meal
        CookingHistoryRecord record = new()
        {
            UserId      = userId,
            RecipeId    = meal.RecipeId.GetValueOrDefault(),
            RecipeName  = recipeName,
            CookedAt    = DateTime.UtcNow,
            Servings    = meal.Servings ?? 1,
            MealType    = meal.MealType,
            Source      = "PlannedMeal",
            PlannedMealId = mealId
        };

        Guid historyId = await _repository.RecordCookingHistoryAsync(record);
        _logger.LogPlannedMealCompleted(userId, mealId, historyId);

        return Ok(new { historyId });
    }

    // Keep old route for backwards compatibility
    // Keep old route for backwards compatibility
    [HttpPut("meals/{id}/complete")]
    public async Task<IActionResult> CompleteMeal(Guid id, [FromBody] CompleteMealRequest? request = null)
    {
        Guid userId = GetUserId();
        PlannedMealDto? plannedMeal = await _repository.GetPlannedMealByIdAsync(id);
        if (plannedMeal is null) { return NotFound(); }
        if (plannedMeal.UserId != userId) { return Forbid(); }

        await _repository.MarkMealAsCompletedAsync(id);

        if (plannedMeal.RecipeId.HasValue)
        {
            decimal servingsEaten = request?.ServingsEaten ?? (decimal)(plannedMeal.Servings ?? 1);
            Guid historyId = await _cookingHistoryRepo.CreateAsync(new CookingHistoryRow
            {
                Id             = Guid.NewGuid(),
                UserId         = userId,
                RecipeId       = plannedMeal.RecipeId.Value,
                PlannedMealId  = plannedMeal.Id,
                CookedAt       = DateTime.UtcNow,
                ServingsCooked = (decimal)(plannedMeal.Servings ?? 1),
                ServingsEaten  = servingsEaten
            });

            // Fire-and-forget with exception logging so failures are visible
            _ = _nutritionLoggingService.LogCookingEventAsync(
                    userId,
                    plannedMeal.RecipeId.Value,
                    request?.RecipeName ?? string.Empty,
                    plannedMeal.MealType,
                    servingsEaten,
                    historyId,
                    plannedMeal.Id,
                    CancellationToken.None)
                .ContinueWith(
                    t => _logger.LogError(t.Exception,
                        "Failed to log nutrition for CookingHistoryId={HistoryId}", historyId),
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        return NoContent();
    }


    // ── Move (DnD) ─────────────────────────────────────────────────────────────

    [HttpPut("meals/{id}/move")]
    public async Task<IActionResult> MoveMeal(Guid id, [FromBody] MoveMealRequest req, CancellationToken ct = default)
    {
        await _repository.UpdatePlannedMealAsync(id, req.NewDate.ToDateTime(TimeOnly.MinValue), req.NewMealType, null, ct);
        return NoContent();
    }

    // ── Multi-course ───────────────────────────────────────────────────────────

    [HttpGet("meals/{id}/courses")]
    public async Task<IActionResult> GetCourses(Guid id, CancellationToken ct = default)
        => Ok(await _courseRepo.GetCoursesAsync(id, ct));

    [HttpPost("meals/{id}/courses")]
    public async Task<IActionResult> AddCourse(Guid id, [FromBody] AddCourseRequest req, CancellationToken ct = default)
    {
        // Use max existing sort order + 1 so new courses naturally append to the end
        List<MealCourseDto> existing = await _courseRepo.GetCoursesAsync(id, ct);
        int nextSortOrder = existing.Count > 0 ? existing.Max(c => c.SortOrder) + 1 : 0;
        Guid courseId = await _courseRepo.AddCourseAsync(id, req.CourseType, req.RecipeId, req.CustomName, req.Servings, nextSortOrder, ct);
        return Ok(new { id = courseId });
    }

    [HttpPut("meals/{id}/courses/{courseId}")]
    public async Task<IActionResult> UpdateCourse(Guid id, Guid courseId, [FromBody] UpdateCourseRequest req, CancellationToken ct = default)
    {
        await _courseRepo.UpdateCourseAsync(courseId, req.RecipeId, req.CustomName, req.Servings, req.SortOrder, ct);
        return NoContent();
    }

    [HttpDelete("meals/{id}/courses/{courseId}")]
    public async Task<IActionResult> DeleteCourse(Guid id, Guid courseId, CancellationToken ct = default)
    {
        await _courseRepo.DeleteCourseAsync(courseId, ct);
        return NoContent();
    }

    [HttpPut("meals/{id}/courses/reorder")]
    public async Task<IActionResult> ReorderCourses(Guid id, [FromBody] List<CourseOrderItem> order, CancellationToken ct = default)
    {
        await _courseRepo.ReorderCoursesAsync(order.Select(o => (o.CourseId, o.SortOrder)).ToList(), ct);
        return NoContent();
    }

    // ── Copy / Clone ───────────────────────────────────────────────────────────

    [HttpPost("meals/{id}/clone")]
    public async Task<IActionResult> CloneMeal(Guid id, [FromBody] CloneMealRequest req, CancellationToken ct = default)
        => Ok(new { id = await _copyService.CloneMealAsync(id, req.TargetDate, req.TargetMealType, ct) });

    [HttpPost("plans/{id}/copy-day")]
    public async Task<IActionResult> CopyDay(Guid id, [FromBody] CopyDayRequest req, CancellationToken ct = default)
    {
        await _copyService.CopyDayAsync(id, req.SourceDate, req.TargetDate, ct);
        return NoContent();
    }

    [HttpPost("plans/{id}/copy-week")]
    public async Task<IActionResult> CopyWeek(Guid id, [FromBody] CopyWeekRequest req, CancellationToken ct = default)
    {
        await _copyService.CopyWeekAsync(id, req.SourceWeekStart, req.TargetWeekStart, ct);
        return NoContent();
    }

    [HttpPost("plans/{id}/copy-month")]
    public async Task<IActionResult> CopyMonth(Guid id, [FromBody] CopyMonthRequest req, CancellationToken ct = default)
    {
        await _copyService.CopyMonthAsync(id, req.SourceYear, req.SourceMonth, req.TargetYear, req.TargetMonth, ct);
        return NoContent();
    }

    [HttpPost("plans/{id}/copy")]
    public async Task<IActionResult> CopyPlan(Guid id, [FromBody] CopyPlanRequest req, CancellationToken ct = default)
        => Ok(new { id = await _copyService.CopyPlanAsync(id, req.NewName, req.NewStartDate, ct) });

    // ── Calendar ───────────────────────────────────────────────────────────────

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] int year, [FromQuery] int month, CancellationToken ct = default)
        => Ok(await _repository.GetCalendarAsync(GetUserId(), year, month, ct));

    // ── Attendees ──────────────────────────────────────────────────────────────

    [HttpGet("meals/{id}/attendees")]
    public async Task<IActionResult> GetAttendees(Guid id, CancellationToken ct = default)
        => Ok(await _attendeeRepo.GetAttendeesAsync(id, ct));

    [HttpPut("meals/{id}/attendees")]
    public async Task<IActionResult> SetAttendees(Guid id, [FromBody] List<MealAttendeeDto> attendees, CancellationToken ct = default)
    {
        await _attendeeRepo.SetAttendeesAsync(id, attendees, ct);
        return NoContent();
    }

    // ── Templates ──────────────────────────────────────────────────────────────

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] bool includePublic = true, CancellationToken ct = default)
        => Ok(await _templateService.GetTemplatesAsync(GetUserId(), includePublic, ct));

    [HttpPost("templates")]
    public async Task<IActionResult> SaveTemplate([FromBody] SaveTemplateRequest req, CancellationToken ct = default)
    {
        Guid id = await _templateService.SaveTemplateFromPlanAsync(GetUserId(), req.PlanId,
            req.FromDate, req.ToDate, req.Name, req.Description, req.Category, req.IsPublic, ct);
        return Ok(new { id });
    }

    [HttpPost("templates/{id}/apply")]
    public async Task<IActionResult> ApplyTemplate(Guid id, [FromBody] ApplyTemplateRequest req, CancellationToken ct = default)
    {
        Guid planId = await _templateService.ApplyTemplateAsync(id, GetUserId(), req.TargetPlanId, req.StartDate, ct);
        return Ok(new { planId });
    }

    // ── Nutritional Goals ──────────────────────────────────────────────────────

    [HttpPost("goals")]
    public async Task<IActionResult> SetGoal([FromBody] SetGoalRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        _logger.LogSettingGoal(userId, request.GoalType);
        Guid goalId = await _repository.SetNutritionalGoalAsync(
            userId, request.GoalType, request.TargetValue, request.Unit, request.StartDate, request.EndDate, ct);
        return Ok(new { id = goalId });
    }

    [HttpGet("goals")]
    public async Task<IActionResult> GetGoals(CancellationToken ct)
    {
        Guid userId = GetUserId();
        _logger.LogGettingGoals(userId);
        List<NutritionalGoalDto> goals = await _repository.GetUserGoalsAsync(userId, ct);
        return Ok(goals);
    }

    [HttpGet("nutrition/summary")]
    public async Task<IActionResult> GetNutritionSummary([FromQuery] DateTime date, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        _logger.LogNutritionSummaryRequest(userId, date);
        NutritionSummaryDto summary = await _repository.GetNutritionSummaryAsync(userId, date, ct);
        return Ok(summary);
    }

    // ── Cooking History ───────────────────────────────────────────────────────

    [HttpPost("history")]
    public async Task<IActionResult> RecordCookingHistory([FromBody] RecordCookingHistoryRequest request)
    {
        Guid userId = GetUserId();
        _logger.LogRecordingCookingHistory(userId, request.RecipeId, request.HouseholdId);
        CookingHistoryRecord record = new()
        {
            UserId      = userId,
            HouseholdId = request.HouseholdId,
            RecipeId    = request.RecipeId,
            RecipeName  = request.RecipeName,
            CookedAt    = request.CookedAt ?? DateTime.UtcNow,
            Servings    = request.Servings,
            MealType    = request.MealType,
            Source      = "Spontaneous"
        };

        Guid historyId = await _repository.RecordCookingHistoryAsync(record);
        _logger.LogCookingHistoryRecorded(userId, historyId);
        return Ok(new { id = historyId });
    }

    [HttpPut("history/{id}/rating")]
    public async Task<IActionResult> UpdateCookingRating(Guid id, [FromBody] UpdateRatingRequest request)
    {
        Guid userId = GetUserId();
        _logger.LogUpdatingCookingRating(userId, id, request.Rating);
        await _repository.UpdateCookingRatingAsync(id, userId, request.Rating, request.WouldCookAgain, request.Notes);
        return NoContent();
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetCookingHistory([FromQuery] int daysBack = 90)
    {
        Guid userId = GetUserId();
        _logger.LogGettingCookingHistory(userId, daysBack);
        List<CookingHistoryDto> history = await _repository.GetCookingHistoryAsync(userId, daysBack);
        return Ok(history);
    }

    [HttpGet("history/most-cooked")]
    public async Task<IActionResult> GetMostCooked([FromQuery] int limit = 10, [FromQuery] int daysBack = 365)
    {
        Guid userId = GetUserId();
        _logger.LogMostCookedRequest(userId, daysBack);
        List<CookingHistorySummaryDto> summary = await _repository.GetMostCookedAsync(userId, limit, daysBack);
        return Ok(summary);
    }

    // ── Suggestions ───────────────────────────────────────────────────────────

    [HttpPost("suggest")]
    public async Task<IActionResult> GetSuggestions([FromBody] SuggestionRequest request)
    {
        Guid userId = GetUserId();
        _logger.LogGettingSuggestions(userId, request.SuggestionMode, request.MealType ?? "any");
        SuggestionRequest requestWithUserId = new()
        {
            UserId           = userId,
            HouseholdId      = request.HouseholdId,
            MealType         = request.MealType,
            SuggestionMode   = request.SuggestionMode,
            InventorySlider  = request.InventorySlider,
            MaxCookMinutes   = request.MaxCookMinutes,
            Count            = request.Count,
            ExcludeRecentDays = request.ExcludeRecentDays,
            RecentDaysCutoff  = request.RecentDaysCutoff,
            ExcludeRecipeIds  = request.ExcludeRecipeIds
        };

        List<MealSuggestion> suggestions = await _suggestionService.SuggestAsync(requestWithUserId);
        _logger.LogSuggestionsGenerated(userId, suggestions.Count);
        return Ok(suggestions);
    }

    [HttpPost("suggest/week")]
    public async Task<IActionResult> GetWeekSuggestions([FromBody] SuggestionRequest request)
    {
        Guid userId = GetUserId();
        _logger.LogWeekSuggestionsRequested(userId, request.SuggestionMode);
        List<MealSuggestion> suggestions = await _suggestionService.SuggestForWeekAsync(
            userId, request.HouseholdId, request);
        return Ok(suggestions);
    }

    // ── Shopping List Generation ──────────────────────────────────────────────

    [HttpPost("plans/{id}/generate-shopping-list")]
    public async Task<IActionResult> GenerateShoppingList(Guid id, [FromBody] GenerateShoppingListRequest request)
    {
        Guid userId = GetUserId();


        // Verify the plan belongs to the authenticated user
        MealPlanDto? mealPlan = await _repository.GetMealPlanAsync(id, userId);
        if (mealPlan == null) return NotFound();

        // Load all non-completed planned meals for this plan
        List<PlannedMealDto> meals = await _repository.GetPlannedMealsAsync(id, null, null);
        meals = meals.Where(m => !m.IsCompleted).ToList();

        _logger.LogGeneratingShoppingList(userId, id, meals.Count);

        if (meals.Count == 0)
        {
            return Ok(new { itemsAdded = 0, message = "No upcoming meals in this plan." });
        }

        string recipeServiceUrl    = _configuration["Services:RecipeService"] ?? "http://recipeservice";
        string inventoryServiceUrl = _configuration["Services:InventoryService"] ?? "http://inventoryservice";
        string shoppingServiceUrl  = _configuration["Services:ShoppingService"] ?? "http://shoppingservice";

        using HttpClient httpClient = _httpClientFactory.CreateClient("MealPlanningService");

        // Fetch ingredients for all meals concurrently to avoid sequential HTTP calls
        IEnumerable<Task<(PlannedMealDto Meal, List<RecipeIngredientItem>? Ingredients)>> ingredientTasks =
            meals.Select(async meal =>
            {
                try
                {
                    List<RecipeIngredientItem>? ingredients = await httpClient.GetFromJsonAsync<List<RecipeIngredientItem>>(
                        $"{recipeServiceUrl}/api/recipes/{meal.RecipeId}/ingredients");
                    return (Meal: meal, Ingredients: ingredients);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch ingredients for recipe {RecipeId}", meal.RecipeId);
                    return (Meal: meal, Ingredients: (List<RecipeIngredientItem>?)null);
                }
            });

        (PlannedMealDto Meal, List<RecipeIngredientItem>? Ingredients)[] mealIngredientResults =
            await Task.WhenAll(ingredientTasks);

        // Aggregate ingredients by name
        Dictionary<string, AggregatedIngredient> aggregated = new(StringComparer.OrdinalIgnoreCase);

        foreach ((PlannedMealDto meal, List<RecipeIngredientItem>? ingredients) in mealIngredientResults)
        {
            if (ingredients == null) continue;

            foreach (RecipeIngredientItem ing in ingredients)
            {
                string key = ing.Name.ToLowerInvariant();
                if (aggregated.TryGetValue(key, out AggregatedIngredient? existing))
                {
                    aggregated[key] = existing with { TotalQuantity = existing.TotalQuantity + (ing.Quantity * (meal.Servings ?? 1)) };
                }
                else
                {
                    aggregated[key] = new AggregatedIngredient(
                        ing.IngredientId, ing.Name, ing.Quantity * (meal.Servings ?? 1), ing.Unit);
                }
            }
        }

        // Fetch on-hand inventory
        Dictionary<string, decimal> onHand = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            List<InventoryItemDto>? inventory = await httpClient.GetFromJsonAsync<List<InventoryItemDto>>(
                $"{inventoryServiceUrl}/api/inventory?userId={userId}");

            if (inventory != null)
            {
                foreach (InventoryItemDto item in inventory)
                {
                    onHand[item.Name.ToLowerInvariant()] = item.QuantityOnHand;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch inventory for user {UserId} — proceeding without", userId);
        }

        // Determine net quantities needed, respecting the inventory slider
        int slider = request.InventorySlider ?? 50;
        Guid? targetListId = request.TargetListId;
        int itemsAdded = 0;

        foreach (AggregatedIngredient agg in aggregated.Values)
        {
            decimal onHandQty = onHand.TryGetValue(agg.Name.ToLowerInvariant(), out decimal q) ? q : 0m;
            decimal usableOnHand = onHandQty * (1m - slider / 100m);
            decimal netQty = agg.TotalQuantity - usableOnHand;

            if (netQty <= 0m) continue;

            if (targetListId.HasValue)
            {
                try
                {
                    await httpClient.PostAsJsonAsync(
                        $"{shoppingServiceUrl}/api/shopping/{targetListId.Value}/items",
                        new
                        {
                            Name     = agg.Name,
                            Quantity = netQty,
                            Unit     = agg.Unit
                        });

                    itemsAdded++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add {Ingredient} to shopping list", agg.Name);
                }
            }
        }

        _logger.LogShoppingListGenerated(userId, id, itemsAdded);
        return Ok(new { itemsAdded });
    }

    [HttpGet("/api/nutrition/trend")]
    public async Task<IActionResult> GetTrend([FromQuery] int days = 7, CancellationToken ct = default)
    {
        if (days is < 1 or > 365) { return BadRequest("days must be between 1 and 365"); }
        return Ok(await _nutritionLogRepo.GetTrendAsync(GetUserId(), days, ct));
    }

    [HttpGet("/api/nutrition/log")]
    public async Task<IActionResult> GetDayDetail([FromQuery] DateTime date, CancellationToken ct = default) =>
        Ok(await _nutritionLogRepo.GetDayDetailAsync(GetUserId(), DateOnly.FromDateTime(date), ct));

    [HttpPost("/api/nutrition/log")]
    public async Task<IActionResult> LogManual([FromBody] ManualNutritionLogRequest req, CancellationToken ct = default)
    {
        DateOnly? date = req.Date.HasValue ? DateOnly.FromDateTime(req.Date.Value) : null;
        await _nutritionLoggingService.LogManualEntryAsync(GetUserId(), req.RecipeName, req.MealType,
            req.ServingsEaten, date, req.Calories, req.Protein, req.Carbohydrates, req.Fat, req.Fiber, req.Sodium, ct);
        return NoContent();
    }

    [HttpGet("/api/nutrition/goals")]
    public async Task<IActionResult> GetActiveGoal(CancellationToken ct = default) =>
        Ok(await _nutritionLogRepo.GetActiveGoalAsync(GetUserId(), ct));

    [HttpGet("/api/nutrition/goals/history")]
    public async Task<IActionResult> GetGoalHistory(CancellationToken ct = default) =>
        Ok(await _nutritionLogRepo.GetGoalHistoryAsync(GetUserId(), ct));

    [HttpPost("/api/nutrition/goals")]
    public async Task<IActionResult> UpsertGoal([FromBody] UpsertGoalRequest req, CancellationToken ct = default)
    {
        if (req.EndDate.HasValue && req.EndDate.Value <= req.StartDate)
        {
            return BadRequest("EndDate must be after StartDate");
        }

        // Validate no overlapping active goal (excluding same goal)
        NutritionalGoalRow? active = await _nutritionLogRepo.GetActiveGoalAsync(GetUserId(), ct);
        if (active is not null && active.Id != req.Id)
        {
            return BadRequest("An active goal already exists. End it before creating a new one.");
        }

        NutritionalGoalRow row = new()
        {
            Id             = req.Id ?? Guid.Empty,
            GoalType       = req.GoalType,
            StartDate      = DateOnly.FromDateTime(req.StartDate),
            EndDate        = req.EndDate.HasValue ? DateOnly.FromDateTime(req.EndDate.Value) : null,
            TargetCalories = req.TargetCalories,
            TargetProtein  = req.TargetProtein,
            TargetCarbs    = req.TargetCarbs,
            TargetFat      = req.TargetFat,
            TargetFiber    = req.TargetFiber,
            TargetSodium   = req.TargetSodium,
            Notes          = req.Notes
        };

        Guid id = await _nutritionLogRepo.UpsertGoalAsync(GetUserId(), row, ct);
        return Ok(new { id });
    }

    [HttpDelete("/api/nutrition/goals/{id}")]
    public async Task<IActionResult> EndGoal(Guid id, CancellationToken ct = default)
    {
        await _nutritionLogRepo.EndGoalAsync(id, GetUserId(), ct);
        return NoContent();
    }

    // ── Snapshots / History ───────────────────────────────────────────────────────

    [HttpGet("plans/{id}/history")]
    public async Task<IActionResult> GetPlanHistory(Guid id, [FromQuery] string? scope, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        if (await _repository.GetMealPlanAsync(id, userId) is null) { return NotFound(); }
        return Ok(await _history.GetSnapshotsAsync(id, scope, ct));
    }

    [HttpPost("plans/{id}/snapshot")]
    public async Task<IActionResult> TakeSnapshot(Guid id, [FromBody] TakeSnapshotRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        if (await _repository.GetMealPlanAsync(id, userId) is null) { return NotFound(); }
        return Ok(new { id = await _history.TakePlanSnapshotAsync(id, userId, "Manual", request.Label, ct) });
    }

    [HttpPost("snapshots/{id}/restore")]
    public async Task<IActionResult> RestoreSnapshot(Guid id, CancellationToken ct = default)
    {
        await _history.RestoreSnapshotAsync(id, GetUserId(), ct);
        return NoContent();
    }

    [HttpGet("meals/{id}/history")]
    public async Task<IActionResult> GetMealHistory(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        return Ok(await _history.GetMealHistoryAsync(id, ct));
    }

    // ── Voting ────────────────────────────────────────────────────────────────────

    [HttpPost("meals/{id}/vote")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] VoteRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        await _voting.UpsertVoteAsync(id, userId, request.Reaction, request.Comment, ct);
        return NoContent();
    }

    [HttpDelete("meals/{id}/vote")]
    public async Task<IActionResult> RemoveVote(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        await _voting.DeleteVoteAsync(id, userId, ct);
        return NoContent();
    }

    [HttpGet("meals/{id}/votes")]
    public async Task<IActionResult> GetVotes(Guid id, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        return Ok(await _voting.GetVoteSummaryAsync(id, ct));
    }

    [HttpPost("meals/{id}/review")]
    public async Task<IActionResult> PostReview(Guid id, [FromBody] PostMealReviewRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        await _voting.UpsertPostMealReviewAsync(id, userId, request.MealRating, request.Comment, request.WouldHaveAgain, ct);
        return NoContent();
    }

    [HttpPost("meals/{id}/course-reviews")]
    public async Task<IActionResult> PostCourseReviews(Guid id, [FromBody] List<CourseReviewRequest> reviews, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        foreach (CourseReviewRequest r in reviews)
        {
            await _voting.UpsertCourseReviewAsync(id, r.RecipeId, r.CourseType, userId, r.Rating, r.Comment, ct);
        }
        return NoContent();
    }

    [HttpPost("plans/{id}/print")]
    public async Task<IActionResult> PrintPlan(Guid id, [FromBody] MealPlanPrintOptions options, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        // Verify ownership and get plan name via the user-scoped lookup before generating the PDF
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId);
        if (plan is null) { return NotFound(); }
        byte[] pdf = await _pdfService.GeneratePdfAsync(options with { MealPlanId = id }, userId, ct);
        string rawName = plan.Name ?? "MealPlan";
        string safeName = string.Concat(rawName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Replace(' ', '_');
        return File(pdf, "application/pdf", $"MealPlan_{safeName}_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("plans/{id}/print/preview")]
    public async Task<IActionResult> PrintPreview(Guid id,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] bool includeRecipes = false, [FromQuery] bool includeGrocery = false,
        [FromQuery] GroceryListGrouping grouping = GroceryListGrouping.Aggregated, CancellationToken ct = default)
    {
        MealPlanPrintOptions options = new()
        {
            MealPlanId = id,
            FromDate = fromDate,
            ToDate = toDate,
            IncludeRecipes = includeRecipes,
            IncludeGroceryList = includeGrocery,
            GroceryGrouping = grouping
        };
        return Ok(await _pdfService.AssemblePrintDataAsync(options, GetUserId(), ct));
    }

    // ── Schedule config ───────────────────────────────────────────────────────

    [HttpGet("schedule-config")]
    public async Task<IActionResult> GetScheduleConfig(CancellationToken ct)
        => Ok(await _scheduleRepo.GetConfigsAsync(GetUserId(), ct));

    [HttpPut("schedule-config")]
    public async Task<IActionResult> SetScheduleConfig([FromBody] List<MealScheduleConfigDto> configs, CancellationToken ct = default)
    {
        await _scheduleRepo.SetConfigsAsync(GetUserId(), configs, ct);
        return NoContent();
    }

    // ── Google Calendar ───────────────────────────────────────────────────────

    [HttpGet("calendar/status")]
    public async Task<IActionResult> GetCalendarStatus(CancellationToken ct)
        => Ok(new { connected = await _googleCal.IsConnectedAsync(GetUserId(), ct) });

    [HttpGet("calendar/events")]
    public async Task<IActionResult> GetCalendarEvents([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct = default)
        => Ok(await _googleCal.GetEventsAsync(GetUserId(), from, to, ct));

    // ── Holidays ─────────────────────────────────────────────────────────────

    [HttpGet("holidays")]
    public IActionResult GetHolidays([FromQuery] int year, [FromQuery] int month, [FromQuery] string? categories)
    {
        IReadOnlyList<string> cats = categories?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            ?? Array.Empty<string>();
        return Ok(_holidays.GetHolidaysForMonth(year, month, cats));
    }

    // ── Auto-fill ─────────────────────────────────────────────────────────────

    [HttpPost("plans/{id}/auto-fill")]
    public async Task<IActionResult> AutoFill(Guid id, [FromBody] AutoFillRequest request, CancellationToken ct = default)
    {
        Guid userId = GetUserId();
        List<CalendarEventDto> calEvents = request.RespectCalendar
            ? await _googleCal.GetEventsAsync(userId, request.FromDate, request.ToDate, ct)
            : new();
        // Auto-fill logic placeholder — returns calendar events that affect the plan window
        return Ok(new { planId = id, busyDates = calEvents.Where(e => e.IsBusy).Select(e => e.Date).Distinct() });
    }
}

// ── Request/Response DTOs ─────────────────────────────────────────────────────

public class CreatePlanRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Name { get; set; }
}

public class AddMealRequest
{
    public Guid? RecipeId { get; set; }
    public DateTime PlannedDate { get; set; }
    public string MealType { get; set; } = "Dinner";
    public int Servings { get; set; } = 1;
}

public class CompleteMealRequest
{
    public decimal? ServingsEaten { get; set; }
    public string? RecipeName { get; set; }
}

public class SetGoalRequest
{
    public string GoalType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class RecordCookingHistoryRequest
{
    public Guid? HouseholdId { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public DateTime? CookedAt { get; set; }
    public int Servings { get; set; } = 1;
    public string MealType { get; set; } = "Dinner";
}

public class UpdateRatingRequest
{
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public byte Rating { get; set; }
    public bool? WouldCookAgain { get; set; }
    public string? Notes { get; set; }
}

public class GenerateShoppingListRequest
{
    public Guid? TargetListId { get; set; }
    public Guid? HouseholdId { get; set; }
    public int? InventorySlider { get; set; }
}

// Internal helper records for shopping list generation
internal record AggregatedIngredient(Guid? IngredientId, string Name, decimal TotalQuantity, string? Unit);

internal class RecipeIngredientItem
{
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
}

internal class InventoryItemDto
{
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public string? Unit { get; set; }
}

public sealed record ManualNutritionLogRequest
{
    public string RecipeName { get; init; } = string.Empty;
    public string? MealType { get; init; }
    public decimal ServingsEaten { get; init; } = 1m;
    /// <summary>Date to log against. Defaults to today (UTC) when omitted, allowing backfill.</summary>
    public DateTime? Date { get; init; }
    public decimal? Calories { get; init; }
    public decimal? Protein { get; init; }
    public decimal? Carbohydrates { get; init; }
    public decimal? Fat { get; init; }
    public decimal? Fiber { get; init; }
    public decimal? Sodium { get; init; }
}

public sealed record UpsertGoalRequest
{
    public Guid? Id { get; init; }
    public string GoalType { get; init; } = "Daily";
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal? TargetCalories { get; init; }
    public decimal? TargetProtein { get; init; }
    public decimal? TargetCarbs { get; init; }
    public decimal? TargetFat { get; init; }
    public decimal? TargetFiber { get; init; }
    public decimal? TargetSodium { get; init; }
    public string? Notes { get; init; }
}

public class MoveMealRequest
{
    public DateOnly NewDate { get; set; }
    public string NewMealType { get; set; } = "Dinner";
}

public class AddCourseRequest
{
    public string CourseType { get; set; } = "Main";
    public Guid? RecipeId { get; set; }
    public string? CustomName { get; set; }
    public decimal Servings { get; set; } = 1;
}

public class UpdateCourseRequest
{
    public Guid? RecipeId { get; set; }
    public string? CustomName { get; set; }
    public decimal Servings { get; set; } = 1;
    public int SortOrder { get; set; }
}

public class CourseOrderItem
{
    public Guid CourseId { get; set; }
    public int SortOrder { get; set; }
}

public class CloneMealRequest
{
    public DateOnly TargetDate { get; set; }
    public string TargetMealType { get; set; } = "Dinner";
}

public class CopyDayRequest
{
    public DateOnly SourceDate { get; set; }
    public DateOnly TargetDate { get; set; }
}

public class CopyWeekRequest
{
    public DateOnly SourceWeekStart { get; set; }
    public DateOnly TargetWeekStart { get; set; }
}

public class CopyMonthRequest
{
    public int SourceYear { get; set; }
    public int SourceMonth { get; set; }
    public int TargetYear { get; set; }
    public int TargetMonth { get; set; }
}

public class CopyPlanRequest
{
    public string NewName { get; set; } = string.Empty;
    public DateOnly NewStartDate { get; set; }
}

public class SaveTemplateRequest
{
    public Guid PlanId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsPublic { get; set; }
}

public class ApplyTemplateRequest
{
    public Guid TargetPlanId { get; set; }
    public DateOnly StartDate { get; set; }
}

public class TakeSnapshotRequest
{
    public string? Label { get; set; }
}

public class VoteRequest
{
    [Required]
    [RegularExpression("^(Love|Like|Neutral|Dislike|Veto)$", ErrorMessage = "Reaction must be one of: Love, Like, Neutral, Dislike, Veto")]
    public string Reaction { get; set; } = string.Empty;
    [MaxLength(300)]
    public string? Comment { get; set; }
}

public class PostMealReviewRequest
{
    [Range(1, 5)]
    public byte MealRating { get; set; }
    [MaxLength(500)]
    public string? Comment { get; set; }
    public bool? WouldHaveAgain { get; set; }
}

public class CourseReviewRequest
{
    [Required]
    public Guid RecipeId { get; set; }
    [MaxLength(50)]
    public string? CourseType { get; set; }
    [Range(1, 5)]
    public byte Rating { get; set; }
    [MaxLength(500)]
    public string? Comment { get; set; }
}

public sealed class AutoFillRequest
{
    public DateOnly FromDate       { get; set; }
    public DateOnly ToDate         { get; set; }
    public bool     RespectCalendar { get; set; } = true;
}

public class SearchPlansRequest
{
    public string? Query { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class QuickPlanRequest
{
    public string? Name { get; set; }
    public DateTime? StartDate { get; set; }
    public int DurationDays { get; set; } = 7;
}

public class UpdateEntryRequest
{
    public DateTime Date { get; set; }
    public string MealType { get; set; } = "Dinner";
    public int? Servings { get; set; }
}
