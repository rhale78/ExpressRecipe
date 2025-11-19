# ExpressRecipe - Frontend Architecture

## Multi-Platform Strategy

ExpressRecipe provides native experiences across platforms:

| Platform | Technology | Use Case |
|----------|------------|----------|
| **Web** | Blazor Server/WASM | Full-featured desktop browser experience |
| **Windows** | WPF/WinUI 3 | Native Windows app with deep OS integration |
| **Android** | .NET MAUI | Mobile scanning, shopping, on-the-go access |
| **PWA** | Blazor WASM | Offline-capable web app, iOS fallback |

## Shared Architecture

### Code Sharing Strategy

```
┌─────────────────────────────────────────┐
│   ExpressRecipe.Shared                  │
│   - Models (User, Product, Recipe)      │
│   - DTOs (API contracts)                │
│   - ViewModels (shared logic)           │
│   - Services (business logic)           │
│   - Validators                          │
│   - Utilities                           │
└─────────────────────────────────────────┘
         │           │            │
    ┌────┴────┐  ┌──┴───┐  ┌─────┴─────┐
    │ Blazor  │  │ WPF  │  │   MAUI    │
    │  Web    │  │ Win  │  │ Android   │
    └─────────┘  └──────┘  └───────────┘
```

**Shared Components:**
- Domain models
- API client services
- Business logic
- Validation rules
- Local database access (SQLite wrapper)
- Sync service
- Authentication service

**Platform-Specific:**
- UI frameworks and controls
- Navigation patterns
- Platform APIs (camera, barcode scanner, push notifications)
- Styling and theming

## Blazor Web Application

### Project Structure
```
ExpressRecipe.BlazorWeb/
├── Components/
│   ├── Pages/          # Routable pages
│   ├── Shared/         # Shared components (NavBar, Layout)
│   ├── Features/       # Feature-specific components
│   │   ├── Products/
│   │   ├── Recipes/
│   │   ├── Inventory/
│   │   └── Shopping/
│   └── Common/         # Reusable UI components
├── Services/           # Blazor-specific services
├── wwwroot/            # Static assets
├── Program.cs
└── appsettings.json
```

### Rendering Modes

**Blazor Server:**
- Real-time connection via SignalR
- Server-side rendering
- Low client resource usage
- Requires constant connection

**Blazor WebAssembly:**
- Runs in browser
- Offline capable
- Higher initial load
- Better scalability

**Auto Mode (.NET 8+):**
- Start with Server for fast initial render
- Download WASM in background
- Switch to WASM for offline capability

**Recommendation:** Use Auto mode for best of both worlds

### Component Example

```razor
@* Pages/Inventory/InventoryList.razor *@
@page "/inventory"
@using ExpressRecipe.Shared.ViewModels
@inject InventoryViewModel ViewModel
@inject NavigationManager Navigation

<PageTitle>My Inventory</PageTitle>

<div class="inventory-container">
    <div class="header">
        <h1>Food Inventory</h1>
        <button @onclick="AddItem">Add Item</button>
    </div>

    <div class="filter-bar">
        <select @bind="ViewModel.SelectedLocation">
            <option value="">All Locations</option>
            <option value="Pantry">Pantry</option>
            <option value="Fridge">Fridge</option>
            <option value="Freezer">Freezer</option>
        </select>

        <input type="text"
               placeholder="Search..."
               @bind="ViewModel.SearchQuery"
               @bind:event="oninput" />
    </div>

    @if (ViewModel.IsLoading) {
        <LoadingSpinner />
    } else if (!ViewModel.Items.Any()) {
        <EmptyState Message="No items in inventory" />
    } else {
        <div class="inventory-grid">
            @foreach (var item in ViewModel.FilteredItems) {
                <InventoryCard Item="@item"
                             OnUse="@(() => UseItem(item))"
                             OnDelete="@(() => DeleteItem(item))" />
            }
        </div>
    }

    @if (ViewModel.ExpiringItems.Any()) {
        <ExpirationAlert Items="@ViewModel.ExpiringItems" />
    }
</div>

@code {
    protected override async Task OnInitializedAsync() {
        await ViewModel.LoadInventoryAsync();
    }

    private void AddItem() {
        Navigation.NavigateTo("/inventory/add");
    }

    private async Task UseItem(InventoryItemDto item) {
        await ViewModel.UseItemAsync(item.Id);
    }

    private async Task DeleteItem(InventoryItemDto item) {
        var confirmed = await JSRuntime.InvokeAsync<bool>(
            "confirm", $"Delete {item.ProductName}?");

        if (confirmed) {
            await ViewModel.DeleteItemAsync(item.Id);
        }
    }
}
```

### State Management

**Blazor Built-in:**
- Component state (fields, properties)
- Cascading parameters for shared state
- Scoped services for session state

**Fluxor (Redux Pattern):**
```csharp
// State
public record InventoryState {
    public List<InventoryItem> Items { get; init; } = new();
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}

// Actions
public record LoadInventoryAction;
public record LoadInventorySuccessAction(List<InventoryItem> Items);
public record LoadInventoryFailureAction(string Error);

// Reducer
public static class InventoryReducers {
    [ReducerMethod]
    public static InventoryState OnLoadInventory(
        InventoryState state, LoadInventoryAction action) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static InventoryState OnLoadSuccess(
        InventoryState state, LoadInventorySuccessAction action) =>
        state with { Items = action.Items, IsLoading = false };
}

// Effects
public class InventoryEffects {
    [EffectMethod]
    public async Task LoadInventory(LoadInventoryAction action, IDispatcher dispatcher) {
        try {
            var items = await inventoryService.GetAllAsync();
            dispatcher.Dispatch(new LoadInventorySuccessAction(items));
        } catch (Exception ex) {
            dispatcher.Dispatch(new LoadInventoryFailureAction(ex.Message));
        }
    }
}
```

## Windows Desktop Application

### Technology Choice: WinUI 3

**Why WinUI 3:**
- Modern Windows 11 design (Fluent)
- Native performance
- Full Windows API access
- Better offline support than Blazor Hybrid
- Advanced features (file system, notifications)

### Project Structure
```
ExpressRecipe.Windows/
├── Views/              # XAML pages
│   ├── MainWindow.xaml
│   ├── InventoryView.xaml
│   ├── RecipeView.xaml
│   └── ShoppingView.xaml
├── ViewModels/         # MVVM pattern
│   ├── MainViewModel.cs
│   ├── InventoryViewModel.cs
│   └── RecipeViewModel.cs
├── Services/           # Windows-specific services
├── Converters/         # XAML value converters
├── Assets/             # Images, icons
└── App.xaml
```

### MVVM Example

**ViewModel:**
```csharp
public partial class InventoryViewModel : ObservableObject {
    private readonly IInventoryService _inventoryService;

    [ObservableProperty]
    private ObservableCollection<InventoryItemDto> _items = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public InventoryViewModel(IInventoryService inventoryService) {
        _inventoryService = inventoryService;
    }

    [RelayCommand]
    private async Task LoadInventoryAsync() {
        IsLoading = true;
        try {
            var items = await _inventoryService.GetAllAsync();
            Items = new ObservableCollection<InventoryItemDto>(items);
        } finally {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UseItemAsync(InventoryItemDto item) {
        await _inventoryService.UseItemAsync(item.Id, quantity: 1);
        await LoadInventoryAsync();
    }

    partial void OnSearchQueryChanged(string value) {
        // Filter items based on search
        FilterItems();
    }
}
```

**View (XAML):**
```xml
<Page x:Class="ExpressRecipe.Windows.Views.InventoryView"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <StackPanel Grid.Row="0" Padding="20">
            <TextBlock Text="Food Inventory" Style="{StaticResource TitleTextBlockStyle}"/>
            <TextBox PlaceholderText="Search..."
                     Text="{x:Bind ViewModel.SearchQuery, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                     Margin="0,10,0,0"/>
        </StackPanel>

        <!-- Items List -->
        <ListView Grid.Row="1"
                  ItemsSource="{x:Bind ViewModel.Items}"
                  SelectionMode="None">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="dto:InventoryItemDto">
                    <Grid Padding="10">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>

                        <StackPanel>
                            <TextBlock Text="{x:Bind ProductName}" FontWeight="Bold"/>
                            <TextBlock Text="{x:Bind Location}" Foreground="Gray"/>
                            <TextBlock Text="{x:Bind ExpirationDate, Converter={StaticResource DateConverter}}"/>
                        </StackPanel>

                        <Button Grid.Column="1"
                                Content="Use"
                                Command="{x:Bind ViewModel.UseItemCommand}"
                                CommandParameter="{x:Bind}"/>
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <!-- Loading Indicator -->
        <ProgressRing IsActive="{x:Bind ViewModel.IsLoading, Mode=OneWay}"
                      Grid.Row="1"
                      HorizontalAlignment="Center"
                      VerticalAlignment="Center"/>
    </Grid>
</Page>
```

### Windows-Specific Features

**File System Access:**
```csharp
public async Task ExportInventoryAsync() {
    var picker = new FileSavePicker();
    picker.SuggestedFileName = "inventory.csv";
    picker.FileTypeChoices.Add("CSV", new[] { ".csv" });

    var file = await picker.PickSaveFileAsync();
    if (file != null) {
        var csv = GenerateCsv(Items);
        await FileIO.WriteTextAsync(file, csv);
    }
}
```

**Toast Notifications:**
```csharp
public void ShowExpirationNotification(InventoryItem item) {
    var toast = new ToastContentBuilder()
        .AddText("Item Expiring Soon!")
        .AddText($"{item.ProductName} expires in 2 days")
        .AddButton("View", ToastActivationType.Foreground, "view")
        .AddButton("Dismiss", ToastActivationType.Background, "dismiss")
        .GetToastContent();

    ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(toast.GetXml()));
}
```

## Android Mobile Application

### Technology: .NET MAUI

**Project Structure:**
```
ExpressRecipe.MAUI/
├── Platforms/
│   └── Android/        # Android-specific code
├── Views/              # XAML pages
├── ViewModels/         # Shared ViewModels
├── Services/
│   ├── CameraService.cs
│   ├── BarcodeScanner.cs
│   └── LocationService.cs
├── Resources/
│   ├── Images/
│   ├── Fonts/
│   └── Styles/
└── MauiProgram.cs
```

### Scanner View Example

**ViewModel:**
```csharp
public partial class ScannerViewModel : ObservableObject {
    private readonly IBarcodeScanner _scanner;
    private readonly IProductService _productService;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private ProductDto? _scannedProduct;

    [ObservableProperty]
    private string? _allergenWarning;

    [RelayCommand]
    private async Task StartScanningAsync() {
        IsScanning = true;
        try {
            var barcode = await _scanner.ScanBarcodeAsync();
            if (!string.IsNullOrEmpty(barcode)) {
                await ProcessBarcodeAsync(barcode);
            }
        } finally {
            IsScanning = false;
        }
    }

    private async Task ProcessBarcodeAsync(string barcode) {
        var product = await _productService.GetByBarcodeAsync(barcode);
        if (product == null) {
            await Shell.Current.DisplayAlert("Not Found",
                "Product not in database. Would you like to add it?", "OK");
            return;
        }

        ScannedProduct = product;

        // Check for allergens
        var userAllergens = await GetUserAllergensAsync();
        var productIngredients = await _productService.GetIngredientsAsync(product.Id);

        var matches = productIngredients
            .Where(i => userAllergens.Any(a =>
                a.Name.Equals(i.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matches.Any()) {
            AllergenWarning = $"⚠️ CONTAINS: {string.Join(", ", matches.Select(m => m.Name))}";
        } else {
            AllergenWarning = "✓ Safe based on your restrictions";
        }
    }
}
```

**View (MAUI XAML):**
```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="ExpressRecipe.MAUI.Views.ScannerPage"
             Title="Scan Product">

    <Grid RowDefinitions="*, Auto">

        <!-- Camera Preview -->
        <zxing:CameraBarcodeReaderView Grid.Row="0"
                                       IsDetecting="{Binding IsScanning}"
                                       BarcodesDetected="OnBarcodesDetected"/>

        <!-- Result Panel -->
        <Frame Grid.Row="1"
               IsVisible="{Binding ScannedProduct, Converter={StaticResource IsNotNullConverter}}"
               BackgroundColor="{AppThemeBinding Light=White, Dark=#1E1E1E}"
               Padding="20">
            <StackPanel>
                <Label Text="{Binding ScannedProduct.Name}"
                       FontSize="20"
                       FontAttributes="Bold"/>
                <Label Text="{Binding ScannedProduct.Brand}"/>

                <Frame Padding="10"
                       Margin="0,10,0,0"
                       BackgroundColor="{Binding AllergenWarning, Converter={StaticResource AllergenColorConverter}}">
                    <Label Text="{Binding AllergenWarning}"
                           FontSize="16"
                           FontAttributes="Bold"/>
                </Frame>

                <Button Text="Add to Shopping List"
                        Command="{Binding AddToShoppingListCommand}"/>
                <Button Text="View Details"
                        Command="{Binding ViewDetailsCommand}"/>
                <Button Text="Scan Another"
                        Command="{Binding StartScanningCommand}"/>
            </StackPanel>
        </Frame>
    </Grid>
</ContentPage>
```

### Android-Specific Features

**Camera Permission:**
```csharp
public async Task<bool> RequestCameraPermissionAsync() {
    var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

    if (status != PermissionStatus.Granted) {
        status = await Permissions.RequestAsync<Permissions.Camera>();
    }

    return status == PermissionStatus.Granted;
}
```

**Barcode Scanning (ZXing.Net.Maui):**
```csharp
public class BarcodeScanner : IBarcodeScanner {
    public async Task<string?> ScanBarcodeAsync() {
        var hasPermission = await RequestCameraPermissionAsync();
        if (!hasPermission) return null;

        var result = await BarcodeReader.ScanAsync();
        return result?.Value;
    }
}
```

**Location Services:**
```csharp
public async Task<Location?> GetCurrentLocationAsync() {
    try {
        var location = await Geolocation.GetLocationAsync(new GeolocationRequest {
            DesiredAccuracy = GeolocationAccuracy.Medium,
            Timeout = TimeSpan.FromSeconds(10)
        });

        return location;
    } catch (Exception ex) {
        Debug.WriteLine($"Unable to get location: {ex.Message}");
        return null;
    }
}
```

## Shared Services Layer

### API Client Service

```csharp
public class ProductApiClient : IProductService {
    private readonly HttpClient _httpClient;
    private readonly ILocalDatabase _localDb;
    private readonly ISyncService _syncService;

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode) {
        // Try local first
        var local = await _localDb.GetProductByBarcodeAsync(barcode);
        if (local != null) return local;

        // Try server
        try {
            var response = await _httpClient.GetAsync($"/api/products/barcode/{barcode}");
            if (response.IsSuccessStatusCode) {
                var product = await response.Content.ReadFromJsonAsync<ProductDto>();

                // Cache locally
                await _localDb.UpsertProductAsync(product);

                return product;
            }
        } catch (HttpRequestException) {
            // Offline - return null
        }

        return null;
    }
}
```

### Local Database Service

```csharp
public class LocalDatabaseService : ILocalDatabase {
    private readonly SQLiteAsyncConnection _db;

    public LocalDatabaseService(string dbPath) {
        _db = new SQLiteAsyncConnection(dbPath);
        InitializeAsync().Wait();
    }

    private async Task InitializeAsync() {
        await _db.CreateTableAsync<ProductLocal>();
        await _db.CreateTableAsync<InventoryItemLocal>();
        await _db.CreateTableAsync<ShoppingListLocal>();
    }

    public async Task<ProductDto?> GetProductByBarcodeAsync(string barcode) {
        var local = await _db.Table<ProductLocal>()
            .Where(p => p.UPC == barcode)
            .FirstOrDefaultAsync();

        return local?.ToDto();
    }
}
```

## Styling & Theming

### Shared Styles
```css
/* wwwroot/css/app.css */
:root {
    --color-primary: #2E7D32;
    --color-secondary: #FFA000;
    --color-danger: #D32F2F;
    --color-warning: #F57C00;
    --color-success: #388E3C;

    --spacing-xs: 4px;
    --spacing-sm: 8px;
    --spacing-md: 16px;
    --spacing-lg: 24px;
    --spacing-xl: 32px;
}

.btn-primary {
    background-color: var(--color-primary);
    color: white;
    padding: var(--spacing-sm) var(--spacing-md);
    border-radius: 4px;
}

.allergen-warning {
    background-color: var(--color-danger);
    color: white;
    padding: var(--spacing-md);
    border-radius: 8px;
}
```

## Offline Support

### Service Worker (PWA)
```javascript
// wwwroot/service-worker.js
const CACHE_NAME = 'expressrecipe-v1';
const urlsToCache = [
    '/',
    '/css/app.css',
    '/js/app.js',
    '/images/logo.png'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(urlsToCache))
    );
});

self.addEventListener('fetch', event => {
    event.respondWith(
        caches.match(event.request)
            .then(response => response || fetch(event.request))
    );
});
```

## Next Steps
See implementation roadmap in `06-IMPLEMENTATION-ROADMAP.md`
