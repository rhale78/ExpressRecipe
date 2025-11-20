using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.MAUI.Services;
using System.Collections.ObjectModel;

namespace ExpressRecipe.MAUI.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly IInventoryApiClient _inventoryClient;
    private readonly IToastService _toastService;
    private readonly INavigationService _navigationService;
    private readonly ISQLiteDatabase _database;
    private readonly ILogger<InventoryViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<InventoryItemViewModel> _items = new();

    [ObservableProperty]
    private ObservableCollection<InventoryItemViewModel> _expiringItems = new();

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
                var apiItems = await _inventoryClient.GetAllItemsAsync();
                if (apiItems != null)
                {
                    Items.Clear();
                    foreach (var item in apiItems)
                    {
                        Items.Add(new InventoryItemViewModel
                        {
                            Id = item.Id,
                            ProductName = item.ProductName ?? "Unknown",
                            Quantity = item.Quantity,
                            Unit = item.Unit ?? "units",
                            ExpirationDate = item.ExpirationDate,
                            Category = item.Category ?? "Other",
                            Location = item.Location ?? "Pantry"
                        });
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

        var item = new InventoryItemViewModel
        {
            Id = Guid.NewGuid(),
            ProductName = result,
            Quantity = quantity,
            Unit = "units",
            Category = "Other",
            Location = "Pantry"
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
    private async Task DeleteItemAsync(InventoryItemViewModel item)
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Delete Item",
            $"Delete {item.ProductName}?",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        Items.Remove(item);

        try
        {
            await _inventoryClient.DeleteItemAsync(item.Id);
            await _toastService.ShowSuccessToast($"Deleted {item.ProductName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item");
            await _toastService.ShowErrorToast("Error deleting item");
        }
    }

    [RelayCommand]
    private async Task UpdateQuantityAsync(InventoryItemViewModel item)
    {
        var result = await Application.Current!.MainPage!.DisplayPromptAsync(
            "Update Quantity",
            $"New quantity for {item.ProductName}:",
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

    partial void OnSearchTextChanged(string value)
    {
        // Implement search filtering
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
                    ProductName = item.ProductName,
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
                Items.Add(new InventoryItemViewModel
                {
                    Id = item.ServerId,
                    ProductName = item.ProductName,
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

public partial class InventoryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private int _quantity;

    [ObservableProperty]
    private string _unit = "units";

    [ObservableProperty]
    private DateTime? _expirationDate;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _location = string.Empty;

    public bool IsExpiringSoon => ExpirationDate.HasValue && ExpirationDate.Value <= DateTime.Now.AddDays(7);
    public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value < DateTime.Now;

    public string ExpirationText
    {
        get
        {
            if (!ExpirationDate.HasValue)
                return "No expiration";

            if (IsExpired)
                return "EXPIRED";

            var daysLeft = (ExpirationDate.Value - DateTime.Now).Days;
            if (daysLeft == 0)
                return "Expires today";
            if (daysLeft == 1)
                return "Expires tomorrow";
            if (daysLeft <= 7)
                return $"Expires in {daysLeft} days";

            return $"Expires {ExpirationDate.Value:MMM d}";
        }
    }

    public Color ExpirationColor
    {
        get
        {
            if (IsExpired)
                return Colors.Red;
            if (IsExpiringSoon)
                return Colors.Orange;
            return Colors.Green;
        }
    }
}
