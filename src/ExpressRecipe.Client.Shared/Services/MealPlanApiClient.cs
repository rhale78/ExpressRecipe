using System.Net.Http.Json;
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

    // Meal Plan Entries
    Task<Guid?> AddMealEntryAsync(AddMealPlanEntryRequest request);
    Task<bool> UpdateMealEntryAsync(Guid entryId, UpdateMealPlanEntryRequest request);
    Task<bool> DeleteMealEntryAsync(Guid entryId);
    Task<bool> MarkMealPreparedAsync(MarkMealPreparedRequest request);

    // Advanced operations
    Task<Guid?> GenerateShoppingListAsync(GenerateShoppingListFromMealPlanRequest request);
    Task<bool> CompleteMealPlanAsync(Guid id);
    Task<bool> ArchiveMealPlanAsync(Guid id);
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
        var token = await _tokenProvider.GetTokenAsync();
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
            return await _httpClient.GetFromJsonAsync<MealPlanDto>($"/api/mealplan/{id}");
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
            var response = await _httpClient.PostAsJsonAsync("/api/mealplan", request);
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
            var response = await _httpClient.DeleteAsync($"/api/mealplan/{id}");
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
            var response = await _httpClient.PostAsJsonAsync("/api/mealplan/entries", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AddMealEntryResponse>();
            return result?.EntryId;
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
            var response = await _httpClient.PostAsJsonAsync("/api/mealplan/entries/mark-prepared", request);
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
            var response = await _httpClient.PostAsJsonAsync("/api/mealplan/generate-shopping-list", request);
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

    private class CreateMealPlanResponse
    {
        public Guid Id { get; set; }
    }

    private class AddMealEntryResponse
    {
        public Guid EntryId { get; set; }
    }

    private class GenerateShoppingListResponse
    {
        public Guid ShoppingListId { get; set; }
    }
}
