using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.Client.Shared.Models.Shopping;
using ExpressRecipe.MAUI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using IToastService = ExpressRecipe.MAUI.Services.IToastService;

namespace ExpressRecipe.MAUI.ViewModels;

public partial class ShoppingListViewModel : ObservableObject
{
    private readonly IShoppingListApiClient _shoppingClient;
    private readonly IToastService _toastService;
    private readonly ISQLiteDatabase _database;
    private readonly ILogger<ShoppingListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ShoppingListItemDto> _items = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private int _totalItems;

    [ObservableProperty]
    private int _completedItems;

    public int ProgressPercentage => TotalItems > 0 ? (CompletedItems * 100 / TotalItems) : 0;

    public ShoppingListViewModel(
        IShoppingListApiClient shoppingClient,
        IToastService toastService,
        ISQLiteDatabase database,
        ILogger<ShoppingListViewModel> logger)
    {
        _shoppingClient = shoppingClient;
        _toastService = toastService;
        _database = database;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await LoadItemsAsync();
    }

    [RelayCommand]
    private async Task LoadItemsAsync()
    {
        try
        {
            IsLoading = true;

            try
            {
                var searchResult = await _shoppingClient.SearchShoppingListsAsync(new ShoppingListSearchRequest { PageSize = 1 });
                var firstList = searchResult?.Lists.FirstOrDefault();
                var apiItems = firstList?.Items;

                if (apiItems != null)
                {
                    Items.Clear();
                    foreach (var item in apiItems)
                    {
                        Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load from API");
            }

            UpdateProgress();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading shopping list");
            await _toastService.ShowErrorToast("Error loading shopping list");
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
        await LoadItemsAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task AddItemAsync()
    {
        var result = await Application.Current!.MainPage!.DisplayPromptAsync(
            "Add Item",
            "Product name:",
            "Add",
            "Cancel");

        if (string.IsNullOrWhiteSpace(result))
            return;

        var item = new ShoppingListItemDto
        {
            Id = Guid.NewGuid(),
            Name = result,
            Quantity = 1,
            Unit = "item",
            IsPurchased = false,
            Category = "Other"
        };

        Items.Add(item);
        UpdateProgress();

        try
        {
            // await _shoppingClient.CreateItemAsync(...);
            await _toastService.ShowSuccessToast($"Added {result}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item");
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(ShoppingListItemDto item)
    {
        Items.Remove(item);
        UpdateProgress();

        try
        {
            await _shoppingClient.DeleteItemAsync(item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item");
        }
    }

    [RelayCommand]
    private async Task ToggleCompletedAsync(ShoppingListItemDto item)
    {
        item.IsPurchased = !item.IsPurchased;
        UpdateProgress();

        try
        {
            // await _shoppingClient.UpdateItemAsync(...);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item");
        }
    }

    [RelayCommand]
    private async Task ClearCompletedAsync()
    {
        var completed = Items.Where(i => i.IsPurchased).ToList();
        foreach (var item in completed)
        {
            Items.Remove(item);
        }

        UpdateProgress();
        await _toastService.ShowSuccessToast($"Cleared {completed.Count} completed items");
    }

    private void UpdateProgress()
    {
        TotalItems = Items.Count;
        CompletedItems = Items.Count(i => i.IsPurchased);
        OnPropertyChanged(nameof(ProgressPercentage));
    }
}
