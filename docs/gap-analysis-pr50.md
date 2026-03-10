# Gap Analysis — PR #50 (Consolidation Branch)

**Scope:** Full codebase audit of `copilot/merge-prs-34-49-into-33`  
**Date:** 2026-03-10  
**Method:** Static code analysis across all services, API clients, Blazor/MAUI frontends, and test projects.

---

## 1. UI ↔ Backend Connectivity Gaps (Route Mismatches & Missing Endpoints)

### 1.1 MealPlanApiClient — Entire Client Is Broken (404s on Every Call)

**Client** (`MealPlanApiClient`) calls `http://mealplanningservice/api/mealplan/*`  
**Backend** (`MealPlanningController`) is registered at `[Route("api/[controller]")]` → resolves to `/api/mealplanning/*`

Every single `MealPlanApiClient` method will receive a 404. Additionally, most advanced endpoints the client expects do not exist in the backend at all:

| Client Calls | Backend Status |
|---|---|
| `GET /api/mealplan/summary` | ❌ MISSING (no summary endpoint) |
| `GET /api/mealplan/week/{date}` | ❌ MISSING |
| `GET /api/mealplan/{id}/calendar` | ❌ MISSING |
| `GET /api/mealplan/{id}/nutrition` | ❌ MISSING |
| `POST /api/mealplan/search` | ❌ MISSING |
| `POST /api/mealplan/quick-plan` | ❌ MISSING |
| `POST /api/mealplan/entries` | ❌ MISSING (backend has `/plans/{id}/meals` instead) |
| `PUT /api/mealplan/entries/{id}` | ❌ MISSING |
| `DELETE /api/mealplan/entries/{id}` | ❌ MISSING |
| `POST /api/mealplan/entries/mark-prepared` | ❌ MISSING (backend has `PUT /meals/{id}/complete`) |
| `POST /api/mealplan/generate-shopping-list` | ❌ MISSING |
| `POST /api/mealplan/{id}/complete` | ❌ MISSING |
| `POST /api/mealplan/{id}/archive` | ❌ MISSING |
| `GET /api/mealplan/{id}` | ❌ MISSING (backend has `/plans/{id}`) |

**Affected UI:** `MealPlanning.razor`, `CreateMealPlan.razor`, `Dashboard.razor` (GetMealPlanSummaryAsync, GetWeekViewAsync).

---

### 1.2 NotificationApiClient — Plural/Singular Route Mismatch

**Client** calls `/api/notifications/*` (plural)  
**Backend** `NotificationController` resolves to `/api/notification/*` (singular, from class name)

Additionally, the client calls endpoints that don't exist in the backend:

| Client Calls | Backend Status |
|---|---|
| `POST /api/notifications/search` | ❌ MISSING |
| `GET /api/notifications/summary` | ❌ MISSING |
| `POST /api/notifications/mark-read` | ❌ MISSING (backend has `PUT /{id}/read`) |
| `POST /api/notifications/mark-all-read` | ❌ MISSING (backend has `PUT /read-all`) |
| `DELETE /api/notifications/delete-all-read` | ❌ MISSING |
| `POST /api/notifications/generate-expiring` | ❌ MISSING |
| `POST /api/notifications/generate-low-stock` | ❌ MISSING |
| `PUT /api/notifications/preferences` | ❌ MISSING (backend uses `POST`) |

**Affected UI:** `Notifications.razor`, `NotificationPreferences.razor`, `MainLayout.razor`.

---

### 1.3 AnalyticsApiClient — Calls Domain-Specific Endpoints That Don't Exist

**Backend** `AnalyticsController` tracks events, usage stats, patterns, insights, and a generic dashboard.  
**Client** calls a completely different set of domain-specific endpoints:

| Client Calls | Backend Status |
|---|---|
| `GET /api/analytics/spending/summary` | ❌ MISSING |
| `POST /api/analytics/spending/report` | ❌ MISSING |
| `GET /api/analytics/nutrition/summary` | ❌ MISSING |
| `POST /api/analytics/nutrition/report` | ❌ MISSING |
| `GET /api/analytics/inventory/summary` | ❌ MISSING |
| `POST /api/analytics/inventory/report` | ❌ MISSING |
| `GET /api/analytics/waste/summary` | ❌ MISSING |
| `POST /api/analytics/waste/report` | ❌ MISSING |
| `POST /api/analytics/export` | ❌ MISSING |

**Affected UI:** `Analytics.razor`, `SpendingReport.razor`, `NutritionReport.razor`, `InventoryReport.razor`, `WasteReport.razor`.

---

### 1.4 SearchApiClient — Route Naming Mismatches

| Client Calls | Backend Route | Status |
|---|---|---|
| `POST /api/search` | `GET /api/search` | ⚠️ Method mismatch (client POSTs, backend GETs) |
| `POST /api/search/advanced` | ❌ No such endpoint | ❌ MISSING |
| `GET /api/search/recent` | `GET /api/search/history` | ❌ Name mismatch |
| `DELETE /api/search/recent` | `DELETE /api/search/history` | ❌ Name mismatch |
| `GET /api/search/suggestions?q=` | `GET /api/search/suggest?q=` | ❌ Name mismatch |
| `POST /api/search/save` | `POST /api/search/preferences` | ❌ Name mismatch |

**Affected UI:** `GlobalSearch.razor`.

---

### 1.5 PriceApiClient — Missing Endpoints in PricesController

`PriceApiClient` calls `/api/prices/*` routes that exist in neither `PricesController` nor `PriceController`:

| Client Calls | Backend Status |
|---|---|
| `GET /api/prices/alerts` | ❌ MISSING |
| `POST /api/prices/alerts` | ❌ MISSING |
| `PUT /api/prices/alerts/{id}` | ❌ MISSING |
| `DELETE /api/prices/alerts/{id}` | ❌ MISSING |
| `GET /api/prices/best-price-alerts` | ❌ MISSING |
| `GET /api/prices/budgets` | ❌ MISSING |
| `POST /api/prices/budgets` | ❌ MISSING |
| `GET /api/prices/budgets/active` | ❌ MISSING |
| `GET /api/prices/budgets/{id}` | ❌ MISSING |
| `PUT /api/prices/budgets/{id}` | ❌ MISSING |
| `GET /api/prices/budgets/{id}/analytics` | ❌ MISSING |
| `GET /api/prices/budgets/{id}/transactions` | ❌ MISSING |
| `GET /api/prices/transactions` | ❌ MISSING |
| `GET /api/prices/transactions/{id}` | ❌ MISSING |
| `GET /api/prices/trends` | ❌ MISSING |
| `GET /api/prices/store-preferences` | ❌ MISSING |
| `PUT /api/prices/store-preferences` | ❌ MISSING |

**Affected UI:** `Prices.razor`.

---

### 1.6 GroceryStoreApiClient — No Authentication Headers Sent

`GroceryStoreApiClient` never calls `EnsureAuthenticatedAsync()` — it sends no `Authorization: Bearer` header. `GroceryStoresController` has `[Authorize]` on the import-trigger endpoint (`/import/trigger`). Anonymous users can search/view, which is correct, but calls from an authenticated user context will not pass the token along.

---

### 1.7 Admin Pages — Inconsistent Authorization

- `DatabaseImport.razor` wraps content in `<AuthorizeView>` without specifying `Roles="Admin"` — any authenticated user can access the import UI.
- `UserManagement.razor` correctly uses `@attribute [Authorize(Roles = "Admin")]`.
- `ImportDashboard.razor` correctly uses `<AuthorizeView Roles="Admin">`.

The inconsistency means `DatabaseImport.razor` is accessible by all authenticated users, not just admins.

---

### 1.8 Shopping Integration — Stub Implementations (return empty / placeholder GUIDs)

All cross-service integration methods in `ShoppingRepository.Integration.cs` are stubs:

```csharp
// AddItemsFromRecipeAsync → returns Guid.NewGuid() placeholder
// GetRecipeIngredientsAsItemsAsync → returns empty list
// AddLowStockItemsAsync → returns Guid.NewGuid() placeholder
// GetLowStockItemsFromInventoryAsync → returns empty list
// AddPurchasedItemsToInventoryAsync → finds items but does NOT call InventoryService
```

**Affected UI:** `ShoppingListDetails.razor` — "Add from Recipe" and "Add Low-Stock Items" buttons do nothing useful; "Mark as Purchased → Add to Inventory" is a no-op.

---

### 1.9 Inventory Scanning — Product Lookup Not Implemented

`InventoryRepository.Scanning.cs` line 255:
```csharp
// TODO: Look up product by barcode
// For now, add with barcode only — productId is null
```
Scanned barcodes add items without resolving the product, so the item is unnamed and unlinked.

---

## 2. Stub / Placeholder Code

| File | Stub Description |
|---|---|
| `ShoppingRepository.Integration.cs` | All 4 cross-service integration methods return empty/placeholder values |
| `ExpirationAlertWorker.cs` | `ProcessExpirationAlertsAsync` logs "processing" but does nothing — no users are queried |
| `SubscriptionRenewalService.cs` | All renewal logic is commented out; method body is empty except logging |
| `RecallMonitorWorker.cs` | Imports recalls from FDA but the TODO "check user subscriptions and send alerts" is unimplemented |
| `AIController.cs` | `POST /api/ai/shopping/optimize` returns `new ShoppingOptimizationResult()` (empty) with TODO comment |
| `IOllamaService.cs` | `// Response parsers (stubs – AI responses handled in TryParseAiExtractionResponse)` |
| `NutritionExtractionService.cs` | Documented as "placeholder for more sophisticated nutrition calculation" |
| `InventoryRepository.Scanning.cs` | Product barcode lookup is skipped; adds placeholder items |
| `PriceScraper Service.cs` | `SearchPricesByBarcodeAsync` is a placeholder with TODO for real barcode price lookups |
| `ShoppingRepository.Reports.cs` | `GetShoppingSummaryAsync` has TODO: "Get most used store" — returns null for that field |
| `IngredientRepository.cs` (ProductService) | `GetByCategory`, `Update`, `Delete` all return empty/true with TODO to add to `IIngredientServiceClient` |
| `BatchRecipeProcessor.cs` | `ParseAsync` throws `NotImplementedException` (intentional — callers must use `ProcessStagedRecipesAsync`) |
| `CookbookApiClient.cs` | `ExportPdfAsync` and `ExportWordAsync` return null with TODO |

---

## 3. UI Pages with Incomplete Functionality (TODO Comments in Code)

### MealPlanning

- `MealPlanning.razor`: Nutrition summary modal not implemented (TODO line 479)
- `MealPlanning.razor`: Click handlers for "Mark Meal" and "Add Meal" don't have the item context to act on (lines 514–528 — lambdas need the meal ID)

### Notifications

- `Notifications.razor`: All event handlers are TODOs: `DeleteNotification`, `HandleNotificationClick`, `MarkAsRead`, `SetFilter`, `ChangePage` (lines 302–322)

### Products / Scanner

- `BarcodeScan.razor` (Products): Camera access is TODO — JavaScript interop for camera/barcode not implemented (lines 257, 273)
- `Products.razor`: Click handler for product item doesn't have context for product ID (line 535)
- `BarcodeScanner.razor` (Scanner): "Report missing product" dialog is TODO (line 306)

### Recipes

- `RecipeDetails.razor`: "Add to Meal Plan" (line 456) and confirmation dialog (line 449) are TODOs
- `Recipes.razor`: Save recent searches to localStorage is TODO (line 452)
- `ImportRecipe.razor`: URL preview/validation is TODO (line 382)

### Admin

- `UserManagement.razor`: `LoadUsers()` and `LoadStats()` call no real API — hardcoded mock data (lines 185, 197)
- `UserManagement.razor`: "View User" and "Edit User" actions are TODOs (lines 225, 231)
- `ImportDashboard.razor`: Import status polling uses a placeholder timer, not a real API call (line 209)

### Other

- `ProductDetails.razor`: "Find Recipes Using This Product" search is TODO (line 349)
- `RecallAlerts.razor`: "View Recall Details" and notification action handlers are TODOs (lines 457, 517–545)
- `GlobalSearch.razor`: "Select Suggestion", "Toggle Filter", and "View Details" are all TODOs (lines 396–406)
- `Settings/ProfileSettings.razor`: Delete account has no confirmation dialog (line 758)
- `Inventory/AddInventoryItem.razor`: Product selection from search results doesn't pass the product ID (line 365)
- `Community/Discover.razor`: Recipe click handler doesn't have the recipe ID context (line 247)
- `Shopping/ShoppingLists.razor`: List click handler doesn't pass list ID (line 463)
- `MainLayout.razor`: Notification badge count is not loaded (line 32 — "when NotificationService is available")

### Shared Components

- `DietaryRestrictionsFilter.razor`: Line 248 — "TODO: Load from UserProfileApiClient" — dietary restrictions are hardcoded, not loaded from user profile
- `UnifiedDietaryFilter.razor`: "Save filters to user profile" API call commented out (lines 618, 845)

---

## 4. Missing Test Projects (8 Services With Zero Tests)

| Service | Status |
|---|---|
| `ExpressRecipe.AnalyticsService` | ❌ No test project |
| `ExpressRecipe.CommunityService` | ❌ No test project |
| `ExpressRecipe.MealPlanningService` | ❌ No test project |
| `ExpressRecipe.NotificationService` | ❌ No test project |
| `ExpressRecipe.RecallService` | ❌ No test project |
| `ExpressRecipe.ScannerService` | ❌ No test project |
| `ExpressRecipe.SearchService` | ❌ No test project |
| `ExpressRecipe.SyncService` | ❌ No test project |

### Under-tested Services (Existing Test Projects With Very Few Tests)

| Service | Test Files |
|---|---|
| `AIService.Tests` | 3 |
| `AuthService.Tests` | 3 |
| `GroceryStoreLocationService.Tests` | 3 |
| `IngredientService.Tests` | 3 |
| `ShoppingService.Tests` | 2 (no integration tests) |

### Missing Test Coverage in Existing Projects

- **MealPlanning:** No tests for `MealPlanningController`, `MealPlanningRepository`, goals/nutrition endpoints
- **Shopping:** `ShoppingRepository.Integration.cs` stubs have no tests; `ShoppingRepository.Reports.cs` has no tests
- **Inventory:** `ExpirationAlertWorker` is untested; Equipment/Storage controllers have no tests
- **Notification:** `NotificationController`, `NotificationRepository`, and the `[AllowAnonymous]` internal endpoint are untested
- **RecallService:** `RecallMonitorWorker` and `FDARecallImportService` import logic is untested
- **Analytics:** No tests at all for any analytics tracking, dashboard generation, or reporting

---

## 5. Security / JWT / Auth Gaps

### 5.1 JWT Validation Weaknesses (Across All 19 Services)

Every service uses:
```csharp
ValidateAudience = false,
RequireHttpsMetadata = false,
```
`ValidateAudience = false` means any token issued by the authority is accepted regardless of which service it was intended for — a token meant for `ProductService` works on `InventoryService`. This is acceptable in an internal service mesh but should be documented and revisited if services are ever exposed externally.

`RequireHttpsMetadata = false` is acceptable for development but must be `true` in production.

### 5.2 `GetUserId()` Throws on Unauthenticated Calls

Multiple controllers use:
```csharp
private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
```
The `!` null-forgiving operator means an unauthenticated request (no token) will throw a `NullReferenceException` rather than returning `401 Unauthorized`. This is caught by `ExceptionHandlingMiddleware` but produces a 500 instead of 401. Affected:
- `MealPlanningController`, `AnalyticsController`, `CommunityController`, `SearchController`, `SyncController`

### 5.3 `DatabaseImport.razor` Lacks Admin Role Guard

As noted in §1.7 — any authenticated user can trigger USDA/FDA/OpenFoodFacts imports. Should use `Roles="Admin"` consistently.

### 5.4 `RestaurantsController.cs` — Missing Admin Role on Import Endpoint

```csharp
[Authorize] // TODO: Add admin role check
[HttpPost("import")]
public async Task<IActionResult> ImportRestaurants(...)
```
Restaurant data import is protected by authentication only, not by an admin role. This could allow any registered user to bulk-import restaurant data.

### 5.5 Blazor Program.cs — `.Result` Deadlock Risk

```csharp
// Line 163 & 174:
var token = tokenProvider.GetAccessTokenAsync().Result; // Get auth token
```
Using `.Result` on an async method in Blazor Server can cause thread pool starvation and deadlocks. Should use `GetAwaiter().GetResult()` (still blocking but safer on thread pool) or restructure initialization to be async.

### 5.6 ProductsController — `.Result` After `Task.WhenAll`

```csharp
await Task.WhenAll(productsTask, countTask);
var result = new ProductSearchResult
{
    Products = productsTask.Result,  // line 70
    TotalCount = countTask.Result,   // line 71
};
```
Calling `.Result` after `await Task.WhenAll(...)` is safe (tasks are complete), but is a code smell. Should use `await productsTask` / `await countTask`.

---

## 6. Error Handling / Logging / Resiliency Gaps

### 6.1 API Clients Silently Swallow All Exceptions

All API clients in `ExpressRecipe.Client.Shared/Services/` use:
```csharp
catch
{
    return null; // or false, or empty list
}
```
No exception is logged, no error is surfaced to the UI beyond returning null. Errors are invisible in diagnostics. Applies to: `MealPlanApiClient`, `InventoryApiClient`, `ShoppingListApiClient`, `NotificationApiClient`, `RecipeApiClient`, `AnalyticsApiClient`, `CommunityApiClient`, `PriceApiClient`, `RecallApiClient`, `ScannerApiClient`, `SyncApiClient`, etc.

`AdminApiClient` has the worst case — bare `catch { }` (empty catch, no return).

### 6.2 API Clients Have No ILogger

None of the API clients (except `MessagingIngredientServiceClient`) inject or use `ILogger`. HTTP errors, network timeouts, deserialization failures are silently swallowed with no diagnostic information.

### 6.3 API Clients Have No CancellationToken Support

No API client method accepts a `CancellationToken`, meaning Blazor page navigation (which cancels in-progress renders) cannot cancel outstanding HTTP calls. This can cause unnecessary load and confusing UI state.

### 6.4 ExpirationAlertWorker Is a Placeholder

`ExpirationAlertWorker.ProcessExpirationAlertsAsync` logs that it is processing but takes no action. Expiring items will never trigger notifications.

### 6.5 SubscriptionRenewalService Is a Placeholder

The entire renewal logic is in a commented-out block. Subscriptions will never auto-renew.

### 6.6 RecallMonitorWorker — User Alert Notification Is Unimplemented

```csharp
// TODO: Check user subscriptions and send alerts
// This would require cross-service communication with NotificationService
```
Recalls are imported from FDA but users are never notified.

### 6.7 Empty Catch Blocks in Service Layer

```
AdminApiClient.cs:61,73,94,108    → catch { }
OpenFoodFactsImportService.cs:641 → catch { }
OpenPricesImportService.cs:461    → catch { }
```

### 6.8 ShoppingRepository.Reports.cs Missing Logging

`ShoppingRepository.Reports.cs` and several repository classes (e.g., `AllergenRepository`, `DietaryRestrictionRepository`, `UserFavoritesRepository`) do not inject `ILogger` and have no logging.

---

## 7. Missing Features (Not Yet Implemented)

| Feature | Location | Status |
|---|---|---|
| Meal Plan week/calendar view | `MealPlanningController` | ❌ Endpoint missing |
| Meal Plan shopping list generation | `MealPlanningController` | ❌ Endpoint missing |
| Meal Plan quick-plan / archiving | `MealPlanningController` | ❌ Endpoint missing |
| Add recipe ingredients to shopping list | `ShoppingRepository.Integration.cs` | ❌ Stub — returns empty |
| Add low-stock items to shopping list | `ShoppingRepository.Integration.cs` | ❌ Stub — returns empty |
| Auto-add purchased items to inventory | `ShoppingRepository.Integration.cs` | ❌ Stub — nothing posted |
| Barcode → product lookup in scanning session | `InventoryRepository.Scanning.cs` | ❌ Stub — null productId |
| Camera-based barcode scanning in browser | `BarcodeScan.razor`, `BarcodeScanner.razor` | ❌ JS interop TODO |
| Recall alert → user notification | `RecallMonitorWorker.cs` | ❌ TODO |
| Expiration alerts → send notifications | `ExpirationAlertWorker.cs` | ❌ Placeholder body |
| Subscription auto-renewal | `SubscriptionRenewalService.cs` | ❌ Commented out |
| Shopping optimization (AI) | `AIController.cs` | ❌ Returns empty result |
| Barcode-based price lookup | `PriceScraper Service.cs` | ❌ Placeholder |
| Analytics reports (spending/nutrition/waste/inventory) | `AnalyticsController` | ❌ Endpoints missing |
| User-submitted product review (community) | `IngredientRepository.cs` (ProductService) | ❌ Update/Delete stubs |
| PDF/Word export from cookbook | `CookbookApiClient.cs` | ❌ Returns null (TODO) |
| Swagger/OpenAPI | AuthService, ProductService, RecipeService, UserService | ❌ Disabled (TODO) |
| Dietary restrictions → saved to user profile | `UnifiedDietaryFilter.razor` | ❌ API call commented out |
| Admin user management (search/stats/actions) | `UserManagement.razor` | ❌ Mock data, no real API calls |
| Add to meal plan from recipe details page | `RecipeDetails.razor` | ❌ TODO |
| Recipe search by product | `ProductDetails.razor` | ❌ TODO |
| Notification actions (mark read, delete, filter) | `Notifications.razor` | ❌ All handlers are TODOs |

---

## 8. Code Quality / Structural Issues

### 8.1 Files With Spaces in Names (Build/Tooling Risk)

```
src/Services/ExpressRecipe.PriceService/Services/PriceScraper Service.cs
src/Services/ExpressRecipe.ScannerService/Services/BarcodeScanner Service.cs
```
Spaces in file names can break certain CI pipelines, shell scripts, and static analysis tools. Should be renamed to `PriceScraperService.cs` and `BarcodeScannerService.cs`.

### 8.2 MAUI ViewModel File Name / Class Name Inconsistency

ViewModel files use shortened names (`MealPlanViewModel.cs`) but the class inside uses the `*PageViewModel` convention (`MealPlanPageViewModel`). This is a minor cosmetic inconsistency but can confuse navigation by file name.

### 8.3 MealPlanningService — Very Thin Controller vs. Rich Client Expectations

`MealPlanningController` has only 8 endpoints. The `MealPlanApiClient` expects 17+ endpoints. This represents a large gap between what the backend delivers and what the UI/client layer was designed to consume.

### 8.4 RecipesController — In-Memory Filter Is a Performance Issue

```csharp
// Apply additional filters in memory (TODO: Move to SQL for better performance)
```
Large recipe datasets will cause this to be slow and memory-intensive.

### 8.5 IngredientRepository (ProductService) — Interface and Implementation Drift

```csharp
// TODO: Add GetByCategory to IIngredientServiceClient if needed
// TODO: Add Update to IIngredientServiceClient
// TODO: Add Delete to IIngredientServiceClient
```
`IngredientRepository` in `ProductService` implements `IIngredientRepository` but is backed by HTTP calls to `IngredientService` — and several interface methods return stub values rather than making proper service calls.

### 8.6 `EmptyTokenProvider` — Placeholder Security Risk

`ExpressRecipe.Shared/Services/EmptyTokenProvider.cs` documents itself as returning `null` tokens. If wired up in production contexts, all auth-gated API calls silently fail. Needs clear documentation that this should never be used in production.

---

## 9. Swagger / Developer Experience

Four services have Swagger disabled with TODO comments:

```
AuthService/Program.cs:61      // TODO: Re-add Swagger after resolving OpenApi 2.0 compatibility
ProductService/Program.cs:146  // TODO: Re-add Swagger
RecipeService/Program.cs:131   // TODO: Re-add Swagger
UserService/Program.cs:93      // TODO: Re-add Swagger
```

Swagger is needed for API discoverability, contract testing, and onboarding. The underlying OpenAPI 2.0 compatibility issue should be resolved (likely by upgrading `Swashbuckle.AspNetCore` or using `Microsoft.AspNetCore.OpenApi`).

---

## 10. Summary — Priority Tiers

### 🔴 Critical (Breaks Core Flows)
1. **MealPlan route mismatch** — entire meal planning feature returns 404
2. **Notification route mismatch** — all notification operations fail
3. **Shopping integration stubs** — add-from-recipe, low-stock, auto-inventory are no-ops
4. **Admin page missing role guard** — any user can trigger production data imports
5. **Expiration alerts are no-ops** — users never warned of expiring inventory

### 🟠 High (Major Feature Gaps)
6. Analytics reporting endpoints completely missing from backend
7. Search route mismatches (5+ endpoints)
8. Price alerts/budgets/transactions endpoints missing from backend
9. Meal plan week/calendar/shopping-list generation endpoints missing
10. Recall → user notification pipeline unimplemented
11. Notification action handlers all are TODOs in Notifications.razor

### 🟡 Medium (Quality / Reliability)
12. API clients swallow all exceptions silently (no logging, no user feedback)
13. API clients have no ILogger, no CancellationToken
14. `GetUserId()` throws instead of returning 401
15. `.Result` usage in Blazor `Program.cs` (deadlock risk)
16. 8 services with zero test coverage
17. Subscription renewal service is commented out
18. Dietary restrictions filter hardcoded (not loaded from profile)

### 🔵 Low (Polish / Code Quality)
19. Files with spaces in names
20. `ValidateAudience = false` / `RequireHttpsMetadata = false` need production hardening
21. Swagger disabled on 4 services
22. ViewModel file names don't match class names (cosmetic)
23. In-memory recipe filter (performance debt)
24. Various UI TODO handlers (context missing for item clicks)
25. Barcode camera interop not implemented in web scanner
