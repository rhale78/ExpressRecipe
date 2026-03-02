using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.Client.Shared.Models.Recipe;
using ExpressRecipe.MAUI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using IToastService = ExpressRecipe.MAUI.Services.IToastService;

namespace ExpressRecipe.MAUI.ViewModels;

public partial class RecipesViewModel : ObservableObject
{
    private readonly IRecipeApiClient _recipeClient;
    private readonly ExpressRecipe.MAUI.Services.IToastService _toastService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<RecipesViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<RecipeDto> _recipes = new();

    [ObservableProperty]
    private ObservableCollection<RecipeDto> _filteredRecipes = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showSafeOnly = true;

    [ObservableProperty]
    private List<string> _selectedDietaryFilters = new();

    public List<string> DietaryOptions { get; } = new()
    {
        "Vegetarian", "Vegan", "Gluten-Free", "Dairy-Free",
        "Nut-Free", "Low-Carb", "Keto", "Paleo"
    };

    public RecipesViewModel(
        IRecipeApiClient recipeClient,
        ExpressRecipe.MAUI.Services.IToastService toastService,
        INavigationService navigationService,
        ILogger<RecipesViewModel> logger)
    {
        _recipeClient = recipeClient;
        _toastService = toastService;
        _navigationService = navigationService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await LoadRecipesAsync();
    }

    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        try
        {
            IsLoading = true;

            var searchResult = await _recipeClient.SearchRecipesAsync(new RecipeSearchRequest
            {
                PageSize = 100,
                Page = 1
            });

            if (searchResult?.Recipes != null)
            {
                Recipes.Clear();
                foreach (var recipe in searchResult.Recipes)
                {
                    Recipes.Add(recipe);
                }

                ApplyFilters();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recipes");
            await _toastService.ShowErrorToast("Error loading recipes");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadRecipesAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task ViewRecipeAsync(RecipeDto recipe)
    {
        await _navigationService.NavigateToAsync("recipedetail", new Dictionary<string, object>
        {
            { "RecipeId", recipe.Id }
        });
    }

    [RelayCommand]
    private void ToggleSafeFilter()
    {
        ShowSafeOnly = !ShowSafeOnly;
        ApplyFilters();
    }

    [RelayCommand]
    private void ToggleDietaryFilter(string filter)
    {
        if (SelectedDietaryFilters.Contains(filter))
        {
            SelectedDietaryFilters.Remove(filter);
        }
        else
        {
            SelectedDietaryFilters.Add(filter);
        }
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        FilteredRecipes.Clear();

        var filtered = Recipes.AsEnumerable();

        // Safe filter (check if recipe has no allergens)
        if (ShowSafeOnly)
        {
            filtered = filtered.Where(r => r.Allergens == null || !r.Allergens.Any());
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(r =>
                r.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Dietary filters
        if (SelectedDietaryFilters.Any())
        {
            filtered = filtered.Where(r =>
                SelectedDietaryFilters.All(f => r.DietaryInfo.Contains(f)));
        }

        foreach (var recipe in filtered)
        {
            FilteredRecipes.Add(recipe);
        }
    }

    [RelayCommand]
    private async Task SaveFavoriteAsync(RecipeDto recipe)
    {
        try
        {
            var isFavorite = await _recipeClient.IsFavoriteAsync(recipe.Id);
            if (isFavorite)
            {
                await _recipeClient.UnfavoriteRecipeAsync(recipe.Id);
                await _toastService.ShowSuccessToast("Removed from favorites");
            }
            else
            {
                await _recipeClient.FavoriteRecipeAsync(recipe.Id);
                await _toastService.ShowSuccessToast("Added to favorites");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving favorite");
            await _toastService.ShowErrorToast("Error updating favorite");
        }
    }
}
