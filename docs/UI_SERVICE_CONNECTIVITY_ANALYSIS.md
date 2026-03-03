# ExpressRecipe — UI-to-Service Connectivity Analysis
**Branch Analyzed**: `copilot/rework-ui-for-recipe-entry` (commit `76c5504`) — PR #17 base  
**Date**: 2026-03-03  
**Prepared by**: Copilot Code Review Agent

---

## Executive Summary

This document catalogs every gap between the Blazor frontend UI and the backend microservices: broken buttons, unregistered services, HTTP method mismatches, orphaned pages, and services that have no UI at all. The analysis covers 38 Razor pages, 24 API client types, 16 registered services, and the Aspire AppHost service registry.

**Critical finding**: Two services — `GroceryStoreLocationService` and `CookbookService` — are fully built on the backend with complete API clients in `Client.Shared`, but neither is registered in the Aspire AppHost, meaning every API call from the frontend to these services will fail with a service-discovery error at runtime.

---

## Table of Contents

1. [Navigation Link Validation (All Links Resolve)](#1-navigation-link-validation)
2. [Orphaned Pages (Exist But Unreachable)](#2-orphaned-pages-exist-but-unreachable)
3. [Broken Buttons and Stub Actions](#3-broken-buttons-and-stub-actions)
4. [HTTP Method / Path Mismatches](#4-http-method--path-mismatches)
5. [Services Not Registered in AppHost](#5-services-not-registered-in-apphost)
6. [Services with No Corresponding UI](#6-services-with-no-corresponding-ui)
7. [API Client Methods with No Backend Endpoint](#7-api-client-methods-with-no-backend-endpoint)
8. [Admin Area — Entirely Unreachable and Mostly Stub](#8-admin-area--entirely-unreachable-and-mostly-stub)
9. [AI Capabilities Not Surfaced in UI](#9-ai-capabilities-not-surfaced-in-ui)
10. [Complete Page-by-Page Wiring Table](#10-complete-page-by-page-wiring-table)
11. [Prioritized Fix List](#11-prioritized-fix-list)

---

## 1. Navigation Link Validation

All 20 navigation links in `NavigationMenu.razor` resolve to a valid `@page` directive. **No broken nav links were found.**

| Nav URL | Page File | Status |
|---|---|---|
| `/dashboard` | `Dashboard/Dashboard.razor` | ✅ |
| `/search` | `Search/GlobalSearch.razor` | ✅ |
| `/scanner` | `Scanner/BarcodeScanner.razor` | ✅ |
| `/products` | `Products/Products.razor` | ✅ |
| `/prices` | `Prices/Prices.razor` | ✅ |
| `/grocery-stores` | `GroceryStores/GroceryStores.razor` | ✅ |
| `/recipes` | `Recipes/Recipes.razor` | ✅ |
| `/inventory` | `Inventory/Inventory.razor` | ✅ |
| `/shopping` | `Shopping/ShoppingLists.razor` | ✅ |
| `/meal-planning` | `MealPlanning/MealPlanning.razor` | ✅ |
| `/discover` | `Community/Discover.razor` | ✅ |
| `/analytics` | `Analytics/Analytics.razor` | ✅ |
| `/notifications` | `Notifications/Notifications.razor` | ✅ |
| `/recalls` | `Recalls/RecallAlerts.razor` | ✅ |
| `/sync` | `Sync/CloudSync.razor` | ✅ |
| `/profile` | `Profile/UserProfile.razor` | ✅ |
| `/settings` | `Settings/ProfileSettings.razor` | ✅ |
| `/help` | `Help/Help.razor` | ✅ |
| `/login` | `Login.razor` | ✅ |
| `/register` | `Register.razor` | ✅ |

> **Note**: `/admin/import` and `/admin/users` are not in the navigation menu. See [§8](#8-admin-area--entirely-unreachable-and-mostly-stub).

---

## 2. Orphaned Pages (Exist But Unreachable)

### 2.1 `RecipeStudio.razor` — Fully Built But Unreachable 🔴

**File**: `Components/Pages/Recipes/RecipeStudio.razor`  
**Routes**: `@page "/recipes/studio"` and `@page "/recipes/studio/{RecipeId:guid}"`

`RecipeStudio.razor` is the most feature-rich recipe creation experience in the application — a dual-panel AI-powered editor with live preview, ingredient parsing, product lookup, undo history, and full sync. It injects 8 services: `IRecipeApiClient`, `IProductApiClient`, `IAIApiClient`, `ILocalStorageService`, `RecipeSyncService`, `IToastService`, `NavigationManager`, `IJSRuntime`.

Despite this, it has **zero navigation links pointing to it** from any other file in the repository:

```
git grep "/recipes/studio" -- "*.razor" *.cs
→ Only RecipeStudio.razor itself references this route
```

The `Recipes.razor` page's "Create Recipe" button navigates to `/recipes/create` (which opens `CreateRecipe.razor`). The `RecipeDetails.razor` edit button also goes to `/recipes/{id}/edit` (also `CreateRecipe.razor`). `RecipeStudio.razor` is completely bypassed.

**Impact**: Users can never reach the Recipe Studio. The only way to access it is to type `/recipes/studio` directly in the browser address bar.

**Duplicate functionality**: There are now two recipe creation/editing tools:

| Tool | Route | Reachable From UI | Notes |
|---|---|---|---|
| `CreateRecipe.razor` | `/recipes/create`, `/recipes/{id}/edit` | ✅ Yes | Linked from Recipes.razor and RecipeDetails.razor |
| `RecipeStudio.razor` | `/recipes/studio`, `/recipes/studio/{id}` | ❌ No | Not linked from anywhere |

**Fix**: Either add a navigation link from `Recipes.razor` to `/recipes/studio` (preferred, as it is more capable), or remove `RecipeStudio.razor` if `CreateRecipe.razor` is the intended path.

---

## 3. Broken Buttons and Stub Actions

### 3.1 `UserManagement.razor` — Entire Page Is a Stub 🔴

**File**: `Components/Pages/Admin/UserManagement.razor`  
**Route**: `/admin/users`

This page injects `IUserProfileApiClient` but makes **zero real API calls**. Every interactive element is hardcoded or a TODO placeholder:

| Button / Action | Handler | Reality |
|---|---|---|
| Search users | `SearchUsers()` → `LoadUsers()` | Returns hardcoded dummy data with TODO comment |
| Page navigation | `ChangePage()` | Same stub `LoadUsers()` |
| Stats panel | `LoadStats()` | Hardcodes `_totalUsers = 1250`, `_activeUsers = 1180`, `_newUsersThisMonth = 47` — no API call |
| 👁️ View user | `ViewUser(userId)` | Shows `"User details view coming soon"` toast, then does nothing |
| ✏️ Edit user | `EditUser(userId)` | Shows `"User edit functionality coming soon"` toast, then does nothing |
| 🚫 Suspend user | `SuspendUser(userId)` | Shows success toast but makes **no API call** — user is never actually suspended |
| ✅ Activate user | `ActivateUser(userId)` | Shows success toast but makes **no API call** — user is never actually activated |

The Suspend and Activate buttons are particularly deceptive: they show a "success" toast to the admin but do nothing on the backend.

### 3.2 `ProductDetails.razor` — Add to Inventory / Add to Shopping List 🟠

**File**: `Components/Pages/Products/ProductDetails.razor`  
**Route**: `/products/{id}`

Two key action buttons navigate away instead of making API calls:

```csharp
private async Task AddToInventory()
{
    // TODO: Implement add to inventory
    // For now, navigate to inventory page
    Navigation.NavigateTo("/inventory");
}

private async Task AddToShoppingList()
{
    // TODO: Implement add to shopping list
    // For now, navigate to shopping page
    Navigation.NavigateTo("/shopping");
}
```

Clicking **"📦 Add to Inventory"** or **"🛒 Add to Shopping List"** from a product detail page loses the product context and drops the user at the inventory/shopping list index page with no pre-filled product. The user must then manually search for the same product again.

**Fix**: Call `InventoryApi.CreateItemAsync(...)` or `ShoppingApi.AddItemAsync(...)` directly from the product detail page, using the already-loaded product data.

### 3.3 `BarcodeScanner.razor` — AddToShoppingList Uses `Guid.Empty` 🟠

**File**: `Components/Pages/Scanner/BarcodeScanner.razor`  
**Route**: `/scanner`

When a user clicks "Add to Shopping List" after scanning a barcode, the request hardcodes `ShoppingListId = Guid.Empty`:

```csharp
var request = new AddShoppingListItemRequest
{
    ShoppingListId = Guid.Empty, // TODO: Get from user's default/active shopping list
    Name = scanResult.Product.Name,
    ...
};
var itemId = await ShoppingClient.AddItemAsync(request);
```

`Guid.Empty` is not a valid shopping list ID. The ShoppingService will reject this request or add the item to a list that doesn't exist. **No shopping list item is actually created.**

**Fix**: Call `ShoppingApi.GetShoppingListsAsync()` to get the user's active list, then use its ID; or add a list-picker UI before submitting.

### 3.4 `BarcodeScanner.razor` — Report Missing Product 🟡

```csharp
private void ShowReportMissing(string barcode)
{
    // TODO: Implement report missing product dialog
    ToastService.ShowInfo("Coming Soon", "Report missing product feature");
}
```

The "🚩 Report Missing Product" button shows a toast but makes no API call. There is no endpoint in `ScannerService` or `ProductService` to receive a missing-product report.

### 3.5 `MealPlanning.razor` — Nutrition Summary Button 🟡

**File**: `Components/Pages/MealPlanning/MealPlanning.razor`

```csharp
private void ViewNutritionSummary()
{
    // TODO: Implement nutrition summary modal
}
```

The **"📊 Nutrition Summary"** button on the weekly view calls this stub with no implementation. No API call is made, no modal is shown. The button is visible but does nothing.

---

## 4. HTTP Method / Path Mismatches

### 4.1 `Prices.razor` Search — POST vs. GET 🔴

**Symptom**: Every search on the Prices page returns `HTTP 405 Method Not Allowed`.

**Client** (`PriceApiClient.cs`):
```csharp
public async Task<PriceSearchResponse> SearchPricesAsync(PriceSearchRequest request)
{
    var response = await _httpClient.PostAsJsonAsync("/api/prices/search", request); // POST
    ...
}
```

**Controller** (`PricesController.cs`):
```csharp
[Route("api/prices")]
[HttpGet("search")]  // ← GET only
public async Task<IActionResult> SearchPrices([FromQuery] PriceSearchRequest request) { ... }
```

The client sends `POST /api/prices/search` but the controller only handles `GET /api/prices/search`. The Prices page is visible and appears functional, but the search button always fails silently (the `catch` block in `PriceApiClient` returns an empty `PriceSearchResponse`).

**Fix**: Either change `PriceApiClient.SearchPricesAsync` to use `GetAsync` with query string parameters, or change the controller to `[HttpPost("search")]` with `[FromBody]`.

### 4.2 `PriceApiClient.ComparePricesAsync` — Wrong Base Path 🟡

The `PriceApiClient` calls:
```csharp
var response = await _httpClient.PostAsJsonAsync("/api/prices/compare", request);
```

But the compare endpoint is in `PriceController` (singular) under `[Route("api/price")]`:
```csharp
[Route("api/price")]  // singular!
[HttpPost("compare")]
public async Task<IActionResult> ComparePrices(...) { }
```

`/api/prices/compare` (plural) → `404 Not Found` — the path should be `/api/price/compare` (singular). There are **two price controllers**: `PriceController` (`api/price`) and `PricesController` (`api/prices`), and the client was written to match `PricesController` exclusively.

---

## 5. Services Not Registered in AppHost

### 5.1 `GroceryStoreLocationService` — Service Exists, Page Exists, Not in AppHost 🔴

| Layer | State |
|---|---|
| Service project | `src/Services/ExpressRecipe.GroceryStoreLocationService/` — fully implemented |
| Controller | `GroceryStoresController` at `[Route("api/grocerystores")]` |
| API Client | `GroceryStoreApiClient.cs` — calls `/api/grocerystores` and `/api/grocerystores/nearby` |
| UI Page | `GroceryStores/GroceryStores.razor` at `@page "/grocery-stores"` — linked from nav menu |
| AppHost | ❌ **NOT registered** — no `AddProject<Projects.ExpressRecipe_GroceryStoreLocationService>()` |
| WebApp references | ❌ No `webApp.WithReference(groceryStoreService)` |

`GroceryStores.razor` calls `StoreApi.SearchStoresAsync(...)` and `StoreApi.GetNearbyStoresAsync(...)`, but because `GroceryStoreLocationService` is absent from `AppHost.cs`, Aspire never starts it and the `HttpClient` has no base URL to resolve. **Every call on the Grocery Stores page fails at runtime with a service-discovery/connection error.**

`AppHost.cs` registers 16 services and the webapp references all of them — but `GroceryStoreLocationService` is missing from both lists.

**Fix**: Add to `AppHost.cs`:
```csharp
var groceryStoreDb = sqlServer.AddDatabase("grocerystoredb", "ExpressRecipe.GroceryStores");
var groceryStoreService = builder.AddProject<Projects.ExpressRecipe_GroceryStoreLocationService>("grocerystoreservice")
    .WithReference(groceryStoreDb)
    .WithReference(redis)
    .WithReference(messaging);
```
And add `.WithReference(groceryStoreService)` to the `webApp` builder.

### 5.2 `CookbookService` — Service Exists, Client Exists, No UI, Not in AppHost 🔴

| Layer | State |
|---|---|
| Service project | `src/Services/ExpressRecipe.CookbookService/` — 3 controllers: `CookbooksController`, `CookbookSectionsController`, `CookbookExportController` |
| API Client | `CookbookApiClient.cs` — 35+ methods covering create/read/update/delete/export/share for cookbooks and sections |
| UI Page | ❌ **No Blazor page exists** anywhere in BlazorWeb for cookbooks |
| Navigation entry | ❌ **No nav link** to any cookbook URL |
| Used by UI pages | ❌ `ICookbookApiClient` is **never injected** in any `.razor` file |
| AppHost | ❌ **NOT registered** — not in `AppHost.cs` at all |

The CookbookService has the second-largest test suite in the project (195 tests), a fully-implemented backend with its own database, and a complete `CookbookApiClient` in `Client.Shared` — but there is no way for any user to reach cookbook functionality. The service is not started by Aspire, and no page links to it.

**Fix**: 
1. Add `CookbookService` to `AppHost.cs` (register service + add `WithReference` in webApp)
2. Create `Cookbooks/Cookbooks.razor` page  
3. Add "📚 Cookbooks" link in `NavigationMenu.razor`  
4. Link cookbook management from `RecipeDetails.razor` ("Save to Cookbook" button)

---

## 6. Services with No Corresponding UI

This section covers services that are fully registered in AppHost and accessible from the frontend, but have little or no dedicated UI.

### 6.1 `IngredientService` — No UI Page 🟠

| Layer | State |
|---|---|
| Service project | `src/Services/ExpressRecipe.IngredientService/` |
| AppHost | ✅ Registered — `webApp.WithReference(ingredientService)` |
| API Client | `IngredientServiceClient.cs` — ingredient lookup, allergen data, parsing |
| UI Page | ❌ **No dedicated page** |
| Used by UI | Partially — used internally by `CreateRecipe.razor` and `AddInventoryItem.razor` for ingredient search |

Ingredient management (browsing/editing the ingredient dictionary, viewing allergen information by ingredient) has no dedicated UI. Users can look up ingredients via recipe creation but cannot browse or manage the ingredient catalog directly.

### 6.2 `SearchService` — Basic Page Exists, Advanced Features Not Surfaced 🟡

`GlobalSearch.razor` at `/search` provides basic search. However, `SearchApiClient` defines several advanced capabilities not surfaced in the UI:
- Recent search history (`GetRecentSearchesAsync`) — displayed but read-only; no "clear history" action
- Suggestions are shown but never acted upon with keyboard navigation
- No filter-by-service-type UI (search type dropdown not connected to backend type filtering)

### 6.3 `SyncService` — UI Exists But Conflict Resolution Is Passive 🟡

`CloudSync.razor` displays sync status and queued changes, but the conflict resolution workflow only shows existing conflicts and allows manual resolution. There is no proactive "sync now" button that triggers push/pull, and the conflict detail view (`_selectedConflict`) exists in code but has no modal or detail panel to display it.

### 6.4 `CommunityService` — Discover Page Exists, Write Features Missing 🟡

`Discover.razor` shows trending and popular community content. However:
- Users can see community recipes but cannot post to the community feed directly from the UI  
- Product submission moderation (admin approve/reject) has no UI page  
- Community point management (viewing other users' points, challenges) has no UI  
- Social actions (follow user, share recipe to community) are not surfaced anywhere

---

## 7. API Client Methods with No Backend Endpoint

These methods exist in `Client.Shared` API clients but call URLs that have no matching controller action. They are currently unused by any Blazor page, but represent a future maintenance trap — any developer who adds a UI for these features will get 404/405 errors immediately.

### 7.1 `PriceApiClient` — Budget Management Endpoints (20+ Methods) 🟠

The `PriceApiClient` has a large set of budget, alert, and transaction methods that all target `/api/prices/...` endpoints that don't exist in any controller:

| Client Method | URL Called | Controller | Status |
|---|---|---|---|
| `RecordPriceAsync` | `POST /api/prices/record` | None | ❌ 404 |
| `GetUserPriceAlertsAsync` | `GET /api/prices/alerts` | None | ❌ 404 |
| `CreatePriceAlertAsync` | `POST /api/prices/alerts` | None | ❌ 404 |
| `GetUserBudgetsAsync` | `GET /api/prices/budgets` | None | ❌ 404 |
| `GetActiveBudgetAsync` | `GET /api/prices/budgets/active` | None | ❌ 404 |
| `GetBudgetAnalyticsAsync` | `GET /api/prices/budgets/{id}/analytics` | None | ❌ 404 |
| `CreateBudgetAsync` | `POST /api/prices/budgets` | None | ❌ 404 |
| `GetBudgetTransactionsAsync` | `GET /api/prices/budgets/{id}/transactions` | None | ❌ 404 |
| `RecordTransactionAsync` | `POST /api/prices/transactions` | None | ❌ 404 |
| `GetBestPriceAlertsAsync` | `GET /api/prices/best-price-alerts` | None | ❌ 404 |
| `GetStorePreferencesAsync` | `GET /api/prices/store-preferences` | None | ❌ 404 |
| `GetPriceTrendsAsync` | `POST /api/prices/trends` | None | ❌ 404 |
| `ComparePricesAsync` | `POST /api/prices/compare` | `PriceController` has `POST /api/price/compare` (singular path) | ❌ Wrong path |

**Root cause**: There are two price controllers — `PriceController` (`[Route("api/price")]`, the original) and `PricesController` (`[Route("api/prices")]`, added in PR #15). The client was written against `api/prices` (plural) but many endpoints are only defined under `api/price` (singular). The two controllers need to be consolidated.

### 7.2 `AIApiClient` — 6 Unused Operations 🟡

`AIApiClient` defines 8 operations; only 2 are used by any Blazor page:

| Method | Used By | Status |
|---|---|---|
| `ExtractRecipeAsync` | `CreateRecipe.razor` | ✅ Used |
| `ExtractRecipeDeepAsync` | `RecipeStudio.razor` | ⚠️ Used (but page is orphaned — see §2.1) |
| `GetIngredientSubstitutesAsync` | None | ❌ Not used |
| `ChatAsync` | None | ❌ Not used |
| `GetMealPlanSuggestionsAsync` | None | ❌ Not used |
| `AnalyzeDietAsync` | None | ❌ Not used |
| `OptimizeShoppingListAsync` | None | ❌ Not used |
| `GetAllergenRisksAsync` | None | ❌ Not used |

Six of eight AI capabilities — ingredient substitution, diet analysis, meal plan suggestions, shopping optimization, allergen risk scoring, and AI chat — are defined and implemented in the backend but have no UI entry point.

### 7.3 `CookbookApiClient` — Never Injected Anywhere 🔴

`CookbookApiClient.cs` is registered in `Client.Shared` with 35+ methods covering full cookbook CRUD, section management, export, sharing, and collaboration. It is **never injected** into any `.razor` file. No user can access any cookbook functionality. (See also §5.2.)

---

## 8. Admin Area — Entirely Unreachable and Mostly Stub

### 8.1 No Admin Navigation Path

The admin pages are at `/admin/import` and `/admin/users`. Neither URL is in the navigation menu, and neither page links to the other. There is no admin dashboard or landing page. **The only way to reach admin pages is to type the URL directly.** There is also no role-based guard on the nav menu to show/hide admin links for admin users.

### 8.2 `UserManagement.razor` — Stub Actions (see §3.1)

All six admin actions (view, edit, suspend, activate users) are TODO placeholders. The Suspend and Activate buttons show false success toasts.

### 8.3 `DatabaseImport.razor` — Three Buttons, All Functional

All three import buttons (USDA, FDA, OpenFoodFacts) correctly call the respective services:
- `AdminApiClient.ImportUSDADatabaseAsync()` → `POST /api/admin/import/usda` on ProductService ✅  
- `AdminApiClient.ImportFDARecallsAsync()` → `POST /api/admin/import/fda` on RecallService ✅  
- `AdminApiClient.ImportOpenFoodFactsAsync()` → `POST /api/admin/import/openfoodfacts` on ProductService ✅

> **Note**: The previous gap analysis (PR #16) incorrectly stated FDA import was broken. The `RecallService.AdminController` does have `[HttpPost("import/fda")]` and RecallService is registered in the webApp. The FDA import button works correctly.

---

## 9. AI Capabilities Not Surfaced in UI

The AI service is registered in AppHost and the `IAIApiClient` is available in `Client.Shared`, but 6 of 8 capabilities are never exposed in the UI:

| Capability | Page | Recommendation |
|---|---|---|
| Recipe extraction (quick) | `CreateRecipe.razor` | ✅ Implemented |
| Recipe extraction (deep) | `RecipeStudio.razor` | ⚠️ Implemented but page is orphaned |
| Ingredient substitution | None | Add "Suggest Substitute" button in recipe ingredient editor |
| Allergen risk scoring | None | Add "Check Allergens" button on `ProductDetails.razor` and scan result |
| Meal plan suggestions | None | Add AI suggestions panel in `MealPlanning.razor` |
| Diet analysis | None | Add "Analyze My Diet" section in `Analytics.razor` |
| Shopping list optimization | None | Add "Optimize List" button in `ShoppingListDetails.razor` |
| AI chat / Q&A | None | Add a floating chat widget or `/ai-assistant` page |

---

## 10. Complete Page-by-Page Wiring Table

| Page | Route | API Clients Injected | Status | Issues |
|---|---|---|---|---|
| `Home.razor` | `/` | None | ✅ Static page | — |
| `Login.razor` | `/login` | `IAuthService` | ✅ Wired | — |
| `Register.razor` | `/register` | `IAuthService` | ✅ Wired | — |
| `Dashboard.razor` | `/dashboard` | UserProfile, Inventory, Shopping, MealPlan, Recipe | ✅ Wired | — |
| `GlobalSearch.razor` | `/search` | `ISearchApiClient` | ✅ Wired | — |
| `BarcodeScanner.razor` | `/scanner` | Scanner, Inventory, ShoppingList | 🟡 Partial | AddToShoppingList uses `Guid.Empty`; Report Missing is stub |
| `Products.razor` | `/products` | `IProductApiClient` | ✅ Wired | — |
| `ProductDetails.razor` | `/products/{id}` | `IProductApiClient` | 🟡 Partial | AddToInventory and AddToShoppingList navigate away instead of calling API |
| `BarcodeScan.razor` | `/products/scan` | `IProductApiClient` | ✅ Wired | — |
| `Prices.razor` | `/prices` | `IPriceApiClient` | 🔴 Broken | Search sends POST; controller only accepts GET → HTTP 405 |
| `GroceryStores.razor` | `/grocery-stores` | `IGroceryStoreApiClient` | 🔴 Broken | Service not registered in AppHost → all calls fail |
| `Recipes.razor` | `/recipes` | Recipe, UserProfile | ✅ Wired | Links to Create/Import/ManageExports but not RecipeStudio |
| `CreateRecipe.razor` | `/recipes/create`, `/recipes/{id}/edit` | Recipe, AI, LocalStorage | ✅ Wired | — |
| `RecipeStudio.razor` | `/recipes/studio`, `/recipes/studio/{id}` | Recipe, Product, AI, LocalStorage | 🔴 Orphaned | No navigation link from any page |
| `RecipeDetails.razor` | `/recipes/{id}` | `IRecipeApiClient` | ✅ Wired | — |
| `ImportRecipe.razor` | `/recipes/import` | `IRecipeApiClient` | ✅ Wired | — |
| `ManageExports.razor` | `/recipes/manage-exports` | Recipe, RecipeSyncService | ✅ Wired | Linked from Recipes.razor |
| `Inventory.razor` | `/inventory` | `IInventoryApiClient` | ✅ Wired | — |
| `AddInventoryItem.razor` | `/inventory/add` | Inventory, Product | ✅ Wired | — |
| `EditInventoryItem.razor` | `/inventory/{id}/edit` | `IInventoryApiClient` | ✅ Wired | — |
| `ShoppingLists.razor` | `/shopping` | `IShoppingListApiClient` | ✅ Wired | — |
| `ShoppingListDetails.razor` | `/shopping/{id}` | Shopping, Product, Recipe | ✅ Wired | — |
| `MealPlanning.razor` | `/meal-planning` | MealPlan, Recipe | 🟡 Partial | ViewNutritionSummary is empty stub |
| `CreateMealPlan.razor` | `/meal-planning/create` | `IMealPlanApiClient` | ✅ Wired | — |
| `Notifications.razor` | `/notifications` | `INotificationApiClient` | ✅ Wired | — |
| `NotificationPreferences.razor` | `/notifications/preferences` | `INotificationApiClient` | ✅ Wired | — |
| `RecallAlerts.razor` | `/recalls` | `IRecallApiClient` | ✅ Wired | — |
| `Analytics.razor` | `/analytics` | `IAnalyticsApiClient` | ✅ Wired (stubs on backend) | Backend returns fake data |
| `Discover.razor` | `/discover` | Community, Recipe | ✅ Wired | Write/social features not surfaced |
| `CloudSync.razor` | `/sync` | `ISyncApiClient` | ✅ Wired | — |
| `UserProfile.razor` | `/profile` | `IUserProfileApiClient` | ✅ Wired | — |
| `ProfileSettings.razor` | `/settings` | `IUserProfileApiClient` | ✅ Wired | — |
| `DatabaseImport.razor` | `/admin/import` | `IAdminApiClient` | ✅ Wired | Page unreachable from nav (no admin menu) |
| `UserManagement.razor` | `/admin/users` | `IUserProfileApiClient` | 🔴 Stub | All actions are TODO placeholders; no real API calls |
| `Help.razor` | `/help`, `/faq` | None | ✅ Static | — |

---

## 11. Prioritized Fix List

### 🔴 Critical — Will Fail or Mislead Users at Runtime

| # | Issue | File(s) to Fix | Effort |
|---|---|---|---|
| 1 | `GroceryStoreLocationService` not in AppHost — nav link present but every API call fails | `src/ExpressRecipe.AppHost.New/AppHost.cs` | Small |
| 2 | `Prices.razor` search is broken — POST vs. GET mismatch → HTTP 405 on every search | `src/ExpressRecipe.Client.Shared/Services/PriceApiClient.cs` | Small |
| 3 | `RecipeStudio.razor` is completely unreachable — no navigation links | `Recipes.razor` or `RecipeDetails.razor` | Small |
| 4 | `UserManagement.razor` Suspend/Activate show fake success toasts — no actual state change | `Admin/UserManagement.razor` | Medium |
| 5 | `CookbookService` not in AppHost and has zero UI — 195 tests but unreachable | `AppHost.cs` + new `Cookbooks.razor` + `NavigationMenu.razor` | Large |

### 🟠 High — Broken Core Workflows

| # | Issue | File(s) to Fix | Effort |
|---|---|---|---|
| 6 | `BarcodeScanner.razor` AddToShoppingList uses `ShoppingListId = Guid.Empty` — request always fails | `Scanner/BarcodeScanner.razor` | Medium |
| 7 | `ProductDetails.razor` Add to Inventory / Shopping List navigate away instead of calling API | `Products/ProductDetails.razor` | Medium |
| 8 | Admin area has no navigation path — admin users must type URLs manually | `NavigationMenu.razor` (add admin section with `AuthorizeView Roles="Admin"`) | Small |

### 🟡 Medium — Features Present But Incomplete

| # | Issue | File(s) to Fix | Effort |
|---|---|---|---|
| 9 | `MealPlanning.razor` Nutrition Summary button is an empty stub | `MealPlanning/MealPlanning.razor` | Medium |
| 10 | `BarcodeScanner.razor` Report Missing Product shows "Coming Soon" only | `Scanner/BarcodeScanner.razor` + backend endpoint | Large |
| 11 | `PriceApiClient` 13+ methods target non-existent endpoints (budget/alert/comparison) | `Services/PriceApiClient.cs` + PriceService controllers | Large |
| 12 | `UserManagement.razor` View/Edit user show stubs | `Admin/UserManagement.razor` | Large |
| 13 | Sync conflict detail panel exists in code but never rendered | `Sync/CloudSync.razor` | Medium |

### 🟢 Low — Missing UI for Available AI Capabilities

| # | Issue | Recommendation | Effort |
|---|---|---|---|
| 14 | Ingredient substitution AI not exposed | Add button in `CreateRecipe.razor` ingredient row | Small |
| 15 | Allergen risk AI not exposed | Add panel on `ProductDetails.razor` and barcode scan result | Medium |
| 16 | Meal plan AI suggestions not exposed | Add AI panel in `MealPlanning.razor` | Medium |
| 17 | Shopping list optimization AI not exposed | Add button in `ShoppingListDetails.razor` | Small |
| 18 | AI diet analysis not exposed | Add section in `Analytics.razor` | Medium |
| 19 | AI chat/assistant not exposed | New `/ai-assistant` page + nav link | Large |
| 20 | `IngredientService` has no dedicated browse/search UI | New `Ingredients/Ingredients.razor` page | Medium |

---

## Appendix — AppHost Service Registration vs. UI Coverage

| Service | In AppHost | WebApp Reference | UI Page | Nav Link |
|---|---|---|---|---|
| AuthService | ✅ | ✅ | Login, Register | ✅ |
| UserService | ✅ | ✅ | Profile, Settings | ✅ |
| IngredientService | ✅ | ✅ | None (used as sub-service) | ❌ |
| ProductService | ✅ | ✅ | Products, ProductDetails, BarcodeScan | ✅ |
| RecipeService | ✅ | ✅ | Recipes, Create, Import, Details, Studio | ✅ |
| InventoryService | ✅ | ✅ | Inventory, Add, Edit | ✅ |
| ScannerService | ✅ | ✅ | BarcodeScanner | ✅ |
| ShoppingService | ✅ | ✅ | ShoppingLists, ShoppingListDetails | ✅ |
| MealPlanningService | ✅ | ✅ | MealPlanning, CreateMealPlan | ✅ |
| PriceService | ✅ | ✅ | Prices (broken search) | ✅ |
| RecallService | ✅ | ✅ | RecallAlerts | ✅ |
| NotificationService | ✅ | ✅ | Notifications, NotificationPreferences | ✅ |
| CommunityService | ✅ | ✅ | Discover | ✅ |
| SyncService | ✅ | ✅ | CloudSync | ✅ |
| SearchService | ✅ | ✅ | GlobalSearch | ✅ |
| AnalyticsService | ✅ | ✅ | Analytics (backend stubs) | ✅ |
| AIService | ✅ | ✅ | Partial (CreateRecipe, RecipeStudio) | ❌ |
| **GroceryStoreLocationService** | ❌ | ❌ | GroceryStores (broken) | ✅ |
| **CookbookService** | ❌ | ❌ | None | ❌ |

---

*Analysis based on commit `76c5504` (`copilot/rework-ui-for-recipe-entry`, PR #17 base branch). Last updated 2026-03-03.*
