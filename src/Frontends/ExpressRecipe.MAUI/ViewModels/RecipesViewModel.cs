using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.MAUI.Services;
using System.Collections.ObjectModel;

namespace ExpressRecipe.MAUI.ViewModels;

public partial class RecipesViewModel : ObservableObject
{
    private readonly IRecipeApiClient _recipeClient;
    private readonly IToastService _toastService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<RecipesViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<RecipeItemViewModel> _recipes = new();

    [ObservableProperty]
    private ObservableCollection<RecipeItemViewModel> _filteredRecipes = new();

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
        IToastService toastService,
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

            var apiRecipes = await _recipeClient.GetAllRecipesAsync();
            if (apiRecipes != null)
            {
                Recipes.Clear();
                foreach (var recipe in apiRecipes)
                {
                    Recipes.Add(new RecipeItemViewModel
                    {
                        Id = recipe.Id,
                        Name = recipe.Name ?? "Unknown Recipe",
                        Description = recipe.Description ?? "",
                        PrepTime = recipe.PrepTime ?? 0,
                        CookTime = recipe.CookTime ?? 0,
                        Servings = recipe.Servings ?? 4,
                        DietaryTags = recipe.DietaryTags ?? new List<string>(),
                        IsSafe = recipe.IsSafe ?? true,
                        Rating = recipe.AverageRating ?? 0,
                        ImageUrl = recipe.ImageUrl
                    });
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
    private async Task ViewRecipeAsync(RecipeItemViewModel recipe)
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

        // Safe filter
        if (ShowSafeOnly)
        {
            filtered = filtered.Where(r => r.IsSafe);
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(r =>
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Dietary filters
        if (SelectedDietaryFilters.Any())
        {
            filtered = filtered.Where(r =>
                SelectedDietaryFilters.All(f => r.DietaryTags.Contains(f)));
        }

        foreach (var recipe in filtered)
        {
            FilteredRecipes.Add(recipe);
        }
    }

    [RelayCommand]
    private async Task SaveFavoriteAsync(RecipeItemViewModel recipe)
    {
        try
        {
            recipe.IsFavorite = !recipe.IsFavorite;
            // await _recipeClient.ToggleFavoriteAsync(recipe.Id);
            await _toastService.ShowSuccessToast(recipe.IsFavorite ? "Added to favorites" : "Removed from favorites");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving favorite");
        }
    }
}

public partial class RecipeItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private int _prepTime;

    [ObservableProperty]
    private int _cookTime;

    [ObservableProperty]
    private int _servings;

    [ObservableProperty]
    private List<string> _dietaryTags = new();

    [ObservableProperty]
    private bool _isSafe;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private double _rating;

    [ObservableProperty]
    private string? _imageUrl;

    public int TotalTime => PrepTime + CookTime;

    public string TimeText => $"{TotalTime} min";

    public string SafetyBadgeText => IsSafe ? "✓ Safe" : "⚠ Check Ingredients";

    public Color SafetyBadgeColor => IsSafe ? Colors.Green : Colors.Orange;

    public string DietaryTagsText => string.Join(", ", DietaryTags);

    public string RatingText => Rating > 0 ? $"⭐ {Rating:F1}" : "No ratings";
}
