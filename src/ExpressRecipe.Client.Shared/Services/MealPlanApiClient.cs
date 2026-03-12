using System.Globalization;
using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Cooking;
using ExpressRecipe.Client.Shared.Models.MealPlanning;

namespace ExpressRecipe.Client.Shared.Services;

public interface IMealPlanApiClient
{
    // Meal Plan CRUD
    Task<MealPlanDto?> GetMealPlanAsync(Guid id);
    Task<MealPlanSearchResult?> SearchMealPlansAsync(MealPlanSearchRequest request);
    Task<MealPlanSummaryDto?> GetMealPlanSummaryAsync();
    Task<Guid?> CreateMealPlanAsync(CreateMealPlanRequest request);
    Task<Guid?> CreateQuickMealPlanAsync(QuickMealPlanRequest request);
    Task<bool> UpdateMealPlanAsync(Guid id, UpdateMealPlanRequest request);
    Task<bool> DeleteMealPlanAsync(Guid id);

    // Meal Plan Calendar Views
    Task<MealPlanCalendarView?> GetMealPlanCalendarAsync(Guid id);
    Task<MealPlanWeekView?> GetWeekViewAsync(DateTime weekStart);
    Task<MealPlanNutritionSummaryDto?> GetNutritionSummaryAsync(Guid id);

    // Month calendar
    Task<List<MealPlanCalendarDay>?> GetCalendarAsync(int year, int month);

    // Meal Plan Entries
    Task<Guid?> AddMealEntryAsync(AddMealPlanEntryRequest request);
    Task<bool> UpdateMealEntryAsync(Guid entryId, UpdateMealPlanEntryRequest request);
    Task<bool> DeleteMealEntryAsync(Guid entryId);
    Task<bool> MarkMealPreparedAsync(MarkMealPreparedRequest request);

    // Drag-and-drop move
    Task<bool> MoveMealAsync(Guid mealId, DateOnly newDate, string newMealType);

    // Advanced operations
    Task<Guid?> GenerateShoppingListAsync(GenerateShoppingListFromMealPlanRequest request);
    Task<bool> CompleteMealPlanAsync(Guid id);
    Task<bool> ArchiveMealPlanAsync(Guid id);

    // Copy / Clone
    Task<Guid?> CloneMealAsync(Guid mealId, CloneMealRequest request);
    Task<bool> CopyDayAsync(Guid planId, CopyDayRequest request);
    Task<bool> CopyWeekAsync(Guid planId, CopyWeekRequest request);

    // Templates
    Task<List<PlanTemplateDto>?> GetTemplatesAsync(bool includePublic = true);
    Task<Guid?> SaveAsTemplateAsync(SaveTemplateRequest request);
    Task<Guid?> ApplyTemplateAsync(Guid templateId, ApplyTemplateRequest request);

    // Multi-course
    Task<List<MealCourseDto>?> GetMealCoursesAsync(Guid mealId);
    Task<Guid?> AddMealCourseAsync(Guid mealId, AddCourseRequest request);
    Task<bool> UpdateMealCourseAsync(Guid mealId, Guid courseId, UpdateCourseRequest request);
    Task<bool> DeleteMealCourseAsync(Guid mealId, Guid courseId);

    // Attendees
    Task<List<MealAttendeeDto>?> GetMealAttendeesAsync(Guid mealId);
    Task<bool> UpdateMealAttendeesAsync(Guid mealId, List<MealAttendeeDto> attendees);

    // Cooking Timers
    Task<List<CookingTimerDto>?> GetActiveTimersAsync();
    Task<Guid?> CreateTimerAsync(CreateCookingTimerRequest request);
    Task<bool> StartTimerAsync(Guid timerId);
    Task<bool> ResumeTimerAsync(Guid timerId);
    Task<bool> PauseTimerAsync(Guid timerId);
    Task<bool> CancelTimerAsync(Guid timerId);
    Task<bool> AcknowledgeTimerAsync(Guid timerId);

    // Pantry Discovery
    Task<PantryDiscoveryResultDto?> GetPantryDiscoveryAsync(PantryDiscoveryOptionsDto options,
        CancellationToken ct = default);
}

public class MealPlanApiClient : IMealPlanApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public MealPlanApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        var token = await _tokenProvider.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
            return false;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task<MealPlanDto?> GetMealPlanAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<MealPlanDto>($"/api/mealplan/plans/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<MealPlanSearchResult?> SearchMealPlansAsync(MealPlanSearchRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/mealplan/search", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MealPlanSearchResult>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<MealPlanSummaryDto?> GetMealPlanSummaryAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<MealPlanSummaryDto>("/api/mealplan/summary");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CreateMealPlanAsync(CreateMealPlanRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/mealplan/plans", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateMealPlanResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CreateQuickMealPlanAsync(QuickMealPlanRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/mealplan/quick-plan", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateMealPlanResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateMealPlanAsync(Guid id, UpdateMealPlanRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/mealplan/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteMealPlanAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/mealplan/plans/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<MealPlanCalendarView?> GetMealPlanCalendarAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<MealPlanCalendarView>($"/api/mealplan/{id}/calendar");
        }
        catch
        {
            return null;
        }
    }

    public async Task<MealPlanWeekView?> GetWeekViewAsync(DateTime weekStart)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var dateStr = weekStart.ToString("yyyy-MM-dd");
            return await _httpClient.GetFromJsonAsync<MealPlanWeekView>($"/api/mealplan/week/{dateStr}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<MealPlanNutritionSummaryDto?> GetNutritionSummaryAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<MealPlanNutritionSummaryDto>($"/api/mealplan/{id}/nutrition");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> AddMealEntryAsync(AddMealPlanEntryRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/mealplan/plans/{request.MealPlanId}/meals", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AddMealEntryResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateMealEntryAsync(Guid entryId, UpdateMealPlanEntryRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/mealplan/entries/{entryId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteMealEntryAsync(Guid entryId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/mealplan/entries/{entryId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> MarkMealPreparedAsync(MarkMealPreparedRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/mealplan/meals/{request.EntryId}/complete",
                new { IsPrepared = request.IsPrepared });
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Guid?> GenerateShoppingListAsync(GenerateShoppingListFromMealPlanRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/mealplan/plans/{request.MealPlanId}/generate-shopping-list", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GenerateShoppingListResponse>();
            return result?.ShoppingListId;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CompleteMealPlanAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/mealplan/{id}/complete", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ArchiveMealPlanAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/mealplan/{id}/archive", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<MealPlanCalendarDay>?> GetCalendarAsync(int year, int month)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<MealPlanCalendarDay>>(
                $"/api/mealplan/calendar?year={year}&month={month}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> MoveMealAsync(Guid mealId, DateOnly newDate, string newMealType)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/mealplan/meals/{mealId}/move",
                new { newDate, newMealType });
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private class CreateMealPlanResponse
    {
        public Guid Id { get; set; }
    }

    private class AddMealEntryResponse
    {
        public Guid Id { get; set; }
    }

    private class GenerateShoppingListResponse
    {
        public Guid ShoppingListId { get; set; }
    }

    // ── Copy / Clone ───────────────────────────────────────────────────────────

    public async Task<Guid?> CloneMealAsync(Guid mealId, CloneMealRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/mealplan/meals/{mealId}/clone", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CloneMealResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CopyDayAsync(Guid planId, CopyDayRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/mealplan/plans/{planId}/copy-day", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CopyWeekAsync(Guid planId, CopyWeekRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/mealplan/plans/{planId}/copy-week", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Templates ──────────────────────────────────────────────────────────────

    public async Task<List<PlanTemplateDto>?> GetTemplatesAsync(bool includePublic = true)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<PlanTemplateDto>>(
                $"/api/mealplan/templates?includePublic={includePublic}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> SaveAsTemplateAsync(SaveTemplateRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/mealplan/templates", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SaveTemplateResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> ApplyTemplateAsync(Guid templateId, ApplyTemplateRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/mealplan/templates/{templateId}/apply", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApplyTemplateResponse>();
            return result?.PlanId;
        }
        catch
        {
            return null;
        }
    }

    // ── Multi-course ───────────────────────────────────────────────────────────

    public async Task<List<MealCourseDto>?> GetMealCoursesAsync(Guid mealId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<MealCourseDto>>(
                $"/api/mealplan/meals/{mealId}/courses");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> AddMealCourseAsync(Guid mealId, AddCourseRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/mealplan/meals/{mealId}/courses", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AddCourseResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateMealCourseAsync(Guid mealId, Guid courseId, UpdateCourseRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/mealplan/meals/{mealId}/courses/{courseId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteMealCourseAsync(Guid mealId, Guid courseId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/api/mealplan/meals/{mealId}/courses/{courseId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Attendees ──────────────────────────────────────────────────────────────

    public async Task<List<MealAttendeeDto>?> GetMealAttendeesAsync(Guid mealId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<MealAttendeeDto>>(
                $"/api/mealplan/meals/{mealId}/attendees");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateMealAttendeesAsync(Guid mealId, List<MealAttendeeDto> attendees)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/mealplan/meals/{mealId}/attendees", attendees);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Cooking Timers ─────────────────────────────────────────────────────────

    public async Task<List<CookingTimerDto>?> GetActiveTimersAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<CookingTimerDto>>("/api/timers");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CreateTimerAsync(CreateCookingTimerRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/timers", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateTimerResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> StartTimerAsync(Guid timerId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/timers/{timerId}/start", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResumeTimerAsync(Guid timerId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/timers/{timerId}/resume", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PauseTimerAsync(Guid timerId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/timers/{timerId}/pause", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CancelTimerAsync(Guid timerId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/timers/{timerId}/cancel", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AcknowledgeTimerAsync(Guid timerId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/timers/{timerId}/acknowledge", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private class CloneMealResponse { public Guid Id { get; set; } }
    private class SaveTemplateResponse { public Guid Id { get; set; } }
    private class ApplyTemplateResponse { public Guid PlanId { get; set; } }
    private class AddCourseResponse { public Guid Id { get; set; } }
    private class CreateTimerResponse { public Guid Id { get; set; } }

    // ── Pantry Discovery ───────────────────────────────────────────────────────

    public async Task<PantryDiscoveryResultDto?> GetPantryDiscoveryAsync(
        PantryDiscoveryOptionsDto options, CancellationToken ct = default)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            string minMatch = options.MinMatchPercent.ToString("F2", CultureInfo.InvariantCulture);
            string sortBy = Uri.EscapeDataString(options.SortBy);
            string url = $"/api/discover?minMatch={minMatch}&sortBy={sortBy}&limit={options.Limit}&respectDiet={options.RespectDietaryRestrictions}";
            return await _httpClient.GetFromJsonAsync<PantryDiscoveryResultDto>(url, ct);
        }
        catch
        {
            // Suppress network/deserialization errors; callers check for null
            return null;
        }
    }
}
