using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.MAUI.Services;
using System.Collections.ObjectModel;

namespace ExpressRecipe.MAUI.ViewModels;

public partial class ShoppingListViewModel : ObservableObject
{
    private readonly IShoppingListApiClient _shoppingClient;
    private readonly IToastService _toastService;
    private readonly ISQLiteDatabase _database;
    private readonly ILogger<ShoppingListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ShoppingItemViewModel> _items = new();

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
                var apiItems = await _shoppingClient.GetAllItemsAsync();
                if (apiItems != null)
                {
                    Items.Clear();
                    foreach (var item in apiItems)
                    {
                        Items.Add(new ShoppingItemViewModel
                        {
                            Id = item.Id,
                            ProductName = item.ProductName ?? "Unknown",
                            Quantity = item.Quantity,
                            IsCompleted = item.IsCompleted,
                            Category = item.Category ?? "Other"
                        });
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

        var item = new ShoppingItemViewModel
        {
            Id = Guid.NewGuid(),
            ProductName = result,
            Quantity = 1,
            IsCompleted = false,
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
    private async Task DeleteItemAsync(ShoppingItemViewModel item)
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
    private async Task ToggleCompletedAsync(ShoppingItemViewModel item)
    {
        item.IsCompleted = !item.IsCompleted;
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
        var completed = Items.Where(i => i.IsCompleted).ToList();
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
        CompletedItems = Items.Count(i => i.IsCompleted);
        OnPropertyChanged(nameof(ProgressPercentage));
    }
}

public partial class ShoppingItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private int _quantity;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _category = string.Empty;
}
