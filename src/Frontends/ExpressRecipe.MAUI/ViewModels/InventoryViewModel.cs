using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.Client.Shared.Models.Inventory;
using ExpressRecipe.MAUI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using IToastService = ExpressRecipe.MAUI.Services.IToastService;

namespace ExpressRecipe.MAUI.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly IInventoryApiClient _inventoryClient;
    private readonly IToastService _toastService;
    private readonly INavigationService _navigationService;
    private readonly ISQLiteDatabase _database;
    private readonly ILogger<InventoryViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<InventoryItemDto> _items = new();

    [ObservableProperty]
    private ObservableCollection<InventoryItemDto> _expiringItems = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    public List<string> Filters { get; } = new() { "All", "Expiring Soon", "Low Stock", "By Category" };

    public InventoryViewModel(
        IInventoryApiClient inventoryClient,
        IToastService toastService,
        INavigationService navigationService,
        ISQLiteDatabase database,
        ILogger<InventoryViewModel> logger)
    {
        _inventoryClient = inventoryClient;
        _toastService = toastService;
        _navigationService = navigationService;
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

            // Try to load from API
            try
            {
                var searchResult = await _inventoryClient.SearchInventoryAsync(new InventorySearchRequest { PageSize = 1000 });
                var apiItems = searchResult?.Items;
                if (apiItems != null)
                {
                    Items.Clear();
                    foreach (var item in apiItems)
                    {
                        Items.Add(item);
                    }

                    // Cache to local database
                    await CacheItemsLocallyAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load from API, loading from local cache");
                await LoadFromLocalCacheAsync();
            }

            UpdateExpiringItems();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading inventory");
            await _toastService.ShowErrorToast("Error loading inventory");
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

        var quantityStr = await Application.Current.MainPage.DisplayPromptAsync(
            "Add Item",
            "Quantity:",
            "Add",
            "Cancel",
            keyboard: Keyboard.Numeric);

        if (!int.TryParse(quantityStr, out var quantity))
            quantity = 1;

        var item = new InventoryItemDto
        {
            Id = Guid.NewGuid(),
            Name = result,
            Quantity = quantity,
            Unit = "units",
            Category = "Other",
            Location = "Pantry",
            CreatedAt = DateTime.UtcNow
        };

        Items.Add(item);

        // Save to API
        try
        {
            // await _inventoryClient.CreateItemAsync(...);
            await _toastService.ShowSuccessToast($"Added {result}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item");
            await _toastService.ShowErrorToast("Error adding item");
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(InventoryItemDto item)
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Delete Item",
            $"Delete {item.Name}?",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        Items.Remove(item);

        try
        {
            await _inventoryClient.DeleteInventoryItemAsync(item.Id);
            await _toastService.ShowSuccessToast($"Deleted {item.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item");
            await _toastService.ShowErrorToast("Error deleting item");
        }
    }

    [RelayCommand]
    private async Task UpdateQuantityAsync(InventoryItemDto item)
    {
        var result = await Application.Current!.MainPage!.DisplayPromptAsync(
            "Update Quantity",
            $"New quantity for {item.Name}:",
            "Update",
            "Cancel",
            initialValue: item.Quantity.ToString(),
            keyboard: Keyboard.Numeric);

        if (int.TryParse(result, out var newQuantity))
        {
            item.Quantity = newQuantity;

            try
            {
                // await _inventoryClient.UpdateItemAsync(...);
                await _toastService.ShowSuccessToast("Quantity updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quantity");
            }
        }
    }

    [RelayCommand]
    private void FilterItems()
    {
        // Filter logic based on SelectedFilter
        UpdateExpiringItems();
    }

    private void UpdateExpiringItems()
    {
        ExpiringItems.Clear();
        var expiringThreshold = DateTime.Now.AddDays(7);

        foreach (var item in Items.Where(i => i.ExpirationDate.HasValue && i.ExpirationDate.Value <= expiringThreshold))
        {
            ExpiringItems.Add(item);
        }
    }

    private async Task CacheItemsLocallyAsync()
    {
        try
        {
            var connection = _database.GetConnection();
            foreach (var item in Items)
            {
                await connection.InsertOrReplaceAsync(new OfflineInventoryItem
                {
                    ServerId = item.Id,
                    ProductName = item.Name,
                    Quantity = item.Quantity,
                    ExpirationDate = item.ExpirationDate,
                    IsSynced = true,
                    LastModified = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching items locally");
        }
    }

    private async Task LoadFromLocalCacheAsync()
    {
        try
        {
            var connection = _database.GetConnection();
            var cachedItems = await connection.Table<OfflineInventoryItem>().ToListAsync();

            Items.Clear();
            foreach (var item in cachedItems)
            {
                Items.Add(new InventoryItemDto
                {
                    Id = item.ServerId,
                    Name = item.ProductName,
                    Quantity = item.Quantity,
                    ExpirationDate = item.ExpirationDate,
                    Category = "Cached",
                    Location = "Unknown"
                });
            }

            await _toastService.ShowWarningToast("Loaded from offline cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading from cache");
        }
    }
}
