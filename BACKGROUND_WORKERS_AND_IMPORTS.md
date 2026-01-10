# Background Workers and API Import Services - Overview

## Background Workers (Automatic)

Your project has **3 background workers** that run automatically on scheduled intervals:

### 1. RecallMonitorWorker
**Location:** `src/Services/ExpressRecipe.RecallService/Services/RecallMonitorWorker.cs`

**Schedule:** Every 1 hour  
**Purpose:** Automatically imports new food recalls  
**Data Sources:**
- FDA openFDA API (general food recalls)
- FDA API filtered for meat/poultry (USDA-regulated products)

**What it does:**
```csharp
// Runs automatically every hour
private async Task CheckForNewRecallsAsync(CancellationToken cancellationToken)
{
    // Import recent FDA recalls (limit: 50)
    var result = await importService.ImportRecentRecallsAsync(limit: 50);
    
    // Import meat/poultry recalls from FDA (limit: 50)
    var meatPoultryResult = await importService.ImportMeatPoultryRecallsFromFDAAsync(limit: 50);
}
```

**Status:** ? Active and working (after recent fixes)

---

### 2. ExpirationAlertWorker
**Location:** `src/Services/ExpressRecipe.InventoryService/Services/ExpirationAlertWorker.cs`

**Schedule:** Every 6 hours  
**Purpose:** Monitors inventory items for upcoming expiration dates  
**Data Sources:** Internal database (no external API)

**Status:** ?? Placeholder implementation (needs completion)

---

### 3. PriceAnalysisWorker
**Location:** `src/Services/ExpressRecipe.PriceService/Services/PriceAnalysisWorker.cs`

**Schedule:** Every 12 hours (twice daily)  
**Purpose:** Analyzes price trends and calculates moving averages  
**Data Sources:** Internal database (no external API)

**Status:** ?? Placeholder implementation (needs completion)

---

## Import Services (On-Demand via API)

These services are **NOT automatic** - they are triggered manually through admin API endpoints:

### 1. USDAFoodDataImportService
**Location:** `src/Services/ExpressRecipe.ProductService/Services/USDAFoodDataImportService.cs`

**API Endpoint:** `POST /api/admin/import/usda`  
**Purpose:** Import food/product data from USDA FoodData Central  
**Data Source:** https://api.nal.usda.gov/fdc/v1/

**Features:**
- Import single food by FDC ID
- Search and bulk import (default: 500 items)
- Requires USDA API key in configuration

**Usage:**
```bash
POST http://localhost:5003/api/admin/import/usda
Content-Type: application/json
Authorization: Bearer {admin-token}

{
  "query": "apple",
  "maxResults": 100
}
```

**Status:** ? Implemented (requires API key configuration)

---

### 2. OpenFoodFactsImportService
**Location:** `src/Services/ExpressRecipe.ProductService/Services/OpenFoodFactsImportService.cs`

**API Endpoint:** `POST /api/admin/import/openfoodfacts`  
**Purpose:** Import product data from Open Food Facts community database  
**Data Source:** https://world.openfoodfacts.org/cgi/search.pl

**Features:**
- Search by query (country, category, brand, etc.)
- Bulk import (default: 1000 items)
- No API key required (free public API)

**Usage:**
```bash
POST http://localhost:5003/api/admin/import/openfoodfacts
Content-Type: application/json
Authorization: Bearer {admin-token}

{
  "query": "united-states",
  "maxResults": 500
}
```

**Status:** ? Implemented (ready to use)

---

### 3. FDARecallImportService
**Location:** `src/Services/ExpressRecipe.RecallService/Services/FDARecallImportService.cs`

**API Endpoints:**
- `POST /api/admin/import/fda-recalls` - General FDA recalls
- `POST /api/admin/import/meat-poultry-recalls` - Meat/poultry recalls
- `POST /api/admin/import/usda-recalls` - ?? Returns error (no USDA API exists)

**Purpose:** Import food recall data  
**Data Source:** https://api.fda.gov/food/enforcement.json

**Usage:**
```bash
POST http://localhost:5005/api/admin/import/meat-poultry-recalls?limit=50
Authorization: Bearer {admin-token}
```

**Status:** ? Working (FDA API), ? USDA direct import not available

---

## Summary Table

| Service | Type | Frequency | External API | Status | Requires Auth |
|---------|------|-----------|--------------|--------|---------------|
| RecallMonitorWorker | Automatic | 1 hour | FDA openFDA | ? Active | No |
| ExpirationAlertWorker | Automatic | 6 hours | None | ?? Placeholder | No |
| PriceAnalysisWorker | Automatic | 12 hours | None | ?? Placeholder | No |
| USDAFoodDataImportService | On-Demand | Manual | USDA FoodData Central | ? Working | Yes (Admin) |
| OpenFoodFactsImportService | On-Demand | Manual | Open Food Facts | ? Working | Yes (Admin) |
| FDARecallImportService | On-Demand + Auto | Manual + 1hr | FDA openFDA | ? Working | Yes (Admin) |

---

## Configuration Requirements

### USDA FoodData Central API
**Required Configuration:**
```json
{
  "USDA": {
    "ApiKey": "your-api-key-here"
  }
}
```

**Get API Key:** https://fdc.nal.usda.gov/api-key-signup.html

### Open Food Facts
**No API key required** - Public API

### FDA openFDA
**No API key required** - Public API  
**Rate Limit:** 240 requests/minute (1000/hour without key)

---

## How to Trigger On-Demand Imports

### 1. Get Admin Token
```bash
POST http://localhost:5001/api/auth/login
Content-Type: application/json

{
  "email": "admin@expressrecipe.com",
  "password": "your-password"
}
```

### 2. Import USDA Food Data
```bash
POST http://localhost:5003/api/admin/import/usda
Content-Type: application/json
Authorization: Bearer {token}

{
  "query": "chicken breast",
  "maxResults": 100
}
```

### 3. Import Open Food Facts Products
```bash
POST http://localhost:5003/api/admin/import/openfoodfacts
Content-Type: application/json
Authorization: Bearer {token}

{
  "query": "category:beverages",
  "maxResults": 200
}
```

### 4. Check Import Status
```bash
GET http://localhost:5003/api/admin/import/{importId}
Authorization: Bearer {token}
```

---

## Missing Service Registrations

?? **Important:** The USDA and OpenFoodFacts import services are **NOT registered in Program.cs**

You need to add these registrations:

**File:** `src/Services/ExpressRecipe.ProductService/Program.cs`

```csharp
// Register import services
builder.Services.AddHttpClient<USDAFoodDataImportService>();
builder.Services.AddHttpClient<OpenFoodFactsImportService>();
```

**Without this, the AdminController will fail to inject these services!**

---

## Automatic vs On-Demand Strategy

### Current Strategy:
- **Recalls:** Automatic (1 hour) ? Good - safety critical
- **Food Data:** On-demand only ?? Consider adding scheduled imports
- **Price Analysis:** Automatic (12 hours) but placeholder
- **Expiration Alerts:** Automatic (6 hours) but placeholder

### Recommended Strategy:

#### Keep Automatic:
1. **Recalls** - Already working, safety critical
2. **Price Analysis** - When implemented, good for trends
3. **Expiration Alerts** - When implemented, user-facing feature

#### Consider Making Automatic:
1. **USDA Food Data Import** - Weekly batch import of popular foods
2. **Open Food Facts** - Daily import of new products

#### Keep On-Demand:
1. Specific product searches
2. Manual database seeding
3. Testing/debugging

---

## Next Steps to Enable Automatic USDA/OpenFoodFacts Imports

### Option 1: Add Background Worker (Recommended)

**File:** `src/Services/ExpressRecipe.ProductService/Services/ProductDataImportWorker.cs`

```csharp
public class ProductDataImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductDataImportWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromDays(1); // Daily

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Product Data Import Worker started");

        // Wait 5 minutes on startup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ImportPopularProductsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Product Data Import Worker");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task ImportPopularProductsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var usdaService = scope.ServiceProvider.GetRequiredService<USDAFoodDataImportService>();
        var openFoodService = scope.ServiceProvider.GetRequiredService<OpenFoodFactsImportService>();

        // Import popular USDA foods
        await usdaService.SearchAndImportAsync("popular", pageSize: 50, maxResults: 200);

        // Import US products from Open Food Facts
        await openFoodService.SearchAndImportAsync("united-states", pageSize: 100, maxResults: 500);
    }
}
```

**Register in Program.cs:**
```csharp
builder.Services.AddHttpClient<USDAFoodDataImportService>();
builder.Services.AddHttpClient<OpenFoodFactsImportService>();
builder.Services.AddHostedService<ProductDataImportWorker>();
```

### Option 2: Keep On-Demand, Add Scheduler
Use Hangfire or similar to schedule imports on specific days/times.

---

## API Documentation Links

- **USDA FoodData Central:** https://fdc.nal.usda.gov/api-guide.html
- **Open Food Facts:** https://world.openfoodfacts.org/data
- **FDA openFDA:** https://open.fda.gov/apis/food/enforcement/
- **Recall API Details:** See `USDA_RECALL_SOLUTION.md`
