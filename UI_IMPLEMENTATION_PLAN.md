# UI Implementation Plan - Inventory & Shopping Enhancements

## Status: Ready to Begin UI Development

### Current State ✅
- **Backend Services**: 100% Complete (5,300 LOC, 86+ endpoints)
- **Client API Layer**: 100% Complete (3,200 LOC, 91 methods, 43 DTOs)
- **Total Infrastructure**: 8,500+ LOC, 0 build errors
- **Basic UI Pages**: Exist but need enhancement for new features

### Infrastructure Complete - No Backend Work Needed

All backend endpoints and client API methods are functional and tested:
- Household management with multi-tenancy
- GPS-based address detection (Haversine formula)
- Lock mode barcode scanning
- Allergen discovery
- Favorites management
- Multi-store price comparison
- Shopping list templates
- Comprehensive reports

## Phase 1: Shared Components (Start Here) 🎯

### Priority 1.1: HouseholdSwitcher Component
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Shared/HouseholdSwitcher.razor`

**Purpose**: Allow users to switch between households (solo, family, roommates)

**Features**:
- Dropdown selector for active household
- Display user's role badge (Owner, Admin, Member, Viewer)
- Show household name with member count
- "Create New Household" action
- Persist selection in state/local storage

**API Methods Used**:
- `InventoryApiClient.GetUserHouseholdsAsync()`
- `InventoryApiClient.GetHouseholdMembersAsync(householdId)`

**Estimated LOC**: ~150

**Integration Points**:
- Header/Layout component
- All Inventory pages
- All Shopping pages

---

### Priority 1.2: AddressSelector Component
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Shared/AddressSelector.razor`

**Purpose**: Select storage address with GPS detection

**Features**:
- "Detect My Location" button (GPS)
- Dropdown of household addresses
- Distance display (e.g., "1.2 km away")
- Manual address selection
- "Add New Address" action
- Filter storage locations by selected address

**API Methods Used**:
- `InventoryApiClient.GetHouseholdAddressesAsync(householdId)`
- `InventoryApiClient.DetectCurrentAddressAsync(latitude, longitude)`
- `InventoryApiClient.CreateAddressAsync(request)`

**Estimated LOC**: ~200

**Integration Points**:
- Inventory pages (filter by address)
- Shopping pages (store location context)
- Add/Edit item forms

---

### Priority 1.3: StorageLocationSelector Component
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Shared/StorageLocationSelector.razor`

**Purpose**: Select specific storage location (Fridge, Pantry, Freezer, etc.)

**Features**:
- Hierarchical display: Address → Location
- Filter by selected address
- Common location icons (🧊 Freezer, 🥫 Pantry, 🧀 Fridge)
- "Add New Location" quick action
- Search/filter locations

**API Methods Used**:
- `InventoryApiClient.GetStorageLocationsAsync(addressId)`
- `InventoryApiClient.CreateStorageLocationAsync(request)`

**Estimated LOC**: ~150

**Integration Points**:
- Add/Edit inventory item forms
- Inventory list filters
- Scanner mode (auto-assign location)

---

## Phase 2: Inventory UI Enhancements 🥫

### Priority 2.1: Inventory Scanner Mode
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Inventory/InventoryScannerMode.razor`

**Purpose**: Lock mode barcode scanning for rapid inventory management

**Features**:
- **Three Modes**:
  - 🟢 Adding Items (new purchases)
  - 🟡 Using Items (cooking/consumption)
  - 🔴 Disposing Items (expired/thrown away)
- Lock mode (stays on screen until ended)
- Barcode input with auto-submit
- Running counter (items scanned)
- Session results display
- Quick storage location selector
- Allergen discovery prompts (dispose mode)
- "End Session" with summary

**API Methods Used**:
- `InventoryApiClient.StartInventoryScanSessionAsync(request)`
- `InventoryApiClient.GetActiveInventoryScanSessionAsync(userId)`
- `InventoryApiClient.ScanAddItemAsync(sessionId, request)`
- `InventoryApiClient.ScanUseItemAsync(sessionId, request)`
- `InventoryApiClient.ScanDisposeItemAsync(sessionId, request)`
- `InventoryApiClient.EndInventoryScanSessionAsync(sessionId)`

**Estimated LOC**: ~400

**UI Layout**:
```
┌─────────────────────────────────────┐
│ 🟢 ADDING MODE                     │
│ Session Active - 15 items scanned   │
├─────────────────────────────────────┤
│ [Barcode Input Field          ] 🔍 │
├─────────────────────────────────────┤
│ Last Scanned:                       │
│ ✓ Kraft Ketchup 32oz               │
│   Added to: Kitchen Pantry          │
│                                     │
│ Recent Items:                       │
│ 1. Milk (Fridge) ✓                 │
│ 2. Bread (Pantry) ✓                │
│ 3. Eggs (Fridge) ✓                 │
├─────────────────────────────────────┤
│ [Switch Mode ▼] [End Session]      │
└─────────────────────────────────────┘
```

**Integration Points**:
- Link from main Inventory page ("Scanner Mode" button)
- Barcode scanner component integration
- Product API for UPC lookup
- Storage location selector integration

---

### Priority 2.2: Enhance Inventory.razor
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Inventory/Inventory.razor` (Existing)

**Enhancements Needed**:
1. Add HouseholdSwitcher component to header
2. Add AddressSelector filter
3. Add StorageLocationSelector filter
4. Add "Scanner Mode" prominent button
5. Add "Allergen Discoveries" alert banner (if any)
6. Filter by household context
7. Show household member activity (who added/used items)

**New Sections**:
```razor
<!-- Add to header -->
<div class="household-context">
    <HouseholdSwitcher @bind-SelectedHouseholdId="_selectedHouseholdId" 
                       OnHouseholdChanged="HandleHouseholdChanged" />
</div>

<!-- Add to filters -->
<div class="location-filters">
    <AddressSelector @bind-SelectedAddressId="_selectedAddressId"
                     HouseholdId="_selectedHouseholdId" />
    <StorageLocationSelector @bind-SelectedLocationId="_selectedLocationId"
                            AddressId="_selectedAddressId" />
</div>

<!-- Add to actions -->
<button class="btn btn-scanner" @onclick="NavigateToScanner">
    📷 Scanner Mode
</button>
```

**Estimated LOC**: +200 (enhancements)

---

### Priority 2.3: Inventory Reports Dashboard
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Inventory/InventoryReportsDashboard.razor`

**Purpose**: Comprehensive inventory analytics and reports

**Sections**:
1. **Low Stock Items**
   - Items below reorder point
   - Sorted by urgency
   - "Add to Shopping List" quick action

2. **Expiring Soon**
   - Items expiring in next 7 days
   - Color-coded urgency (3 days = red, 7 days = yellow)
   - "Use in Recipe" suggestions

3. **Running Out Predictions**
   - Based on usage patterns
   - Estimated days remaining
   - AI-powered (ready for Ollama integration)

4. **By Location Summary**
   - Items per address
   - Items per storage location
   - Value estimates

5. **Household Activity**
   - Who added what
   - Who used what
   - Recent disposals

**API Methods Used**:
- `InventoryApiClient.GetLowStockItemsAsync(householdId)`
- `InventoryApiClient.GetExpiringItemsAsync(householdId, days)`
- `InventoryApiClient.GetRunningOutItemsAsync(householdId, days)`
- `InventoryApiClient.GetInventoryReportAsync(householdId)`

**Estimated LOC**: ~500

**UI Layout**:
```
┌─────────────────────────────────────┐
│ 📊 Inventory Reports                │
├─────────────────────────────────────┤
│ [Low Stock] [Expiring] [Running Out]│
├─────────────────────────────────────┤
│ 📉 Low Stock Items (12)            │
│                                     │
│ ● Milk - Only 1 left               │
│   Last purchased: 3 days ago        │
│   [Add to Shopping List]            │
│                                     │
│ ● Bread - Only 2 slices left       │
│   [Add to Shopping List]            │
└─────────────────────────────────────┘
```

---

### Priority 2.4: Allergen Discovery Panel
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Shared/AllergenDiscoveryPanel.razor`

**Purpose**: Show discovered allergens from disposed items

**Features**:
- Alert banner on main pages when discoveries exist
- List of discovered allergens
- Product that caused issue
- "Add to My Allergies" action
- "Not an allergen" dismiss action
- Date/user who discovered it

**API Methods Used**:
- `InventoryApiClient.GetAllergenDiscoveriesAsync(userId)`
- `UserApiClient.AddAllergenAsync(userId, allergenId)` (exists)

**Estimated LOC**: ~150

**UI Layout**:
```
┌─────────────────────────────────────┐
│ ⚠️ Allergen Discovered              │
├─────────────────────────────────────┤
│ Product: Skippy Peanut Butter       │
│ Discovered by: John (2 days ago)    │
│ Reason: Caused allergy reaction     │
│                                     │
│ [Add to My Allergies] [Dismiss]    │
└─────────────────────────────────────┘
```

---

## Phase 3: Shopping UI Enhancements 🛒

### Priority 3.1: Favorites Panel
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Shopping/FavoritesPanel.razor`

**Purpose**: Manage favorite items for quick shopping list adds

**Features**:
- Two tabs: "My Favorites" and "Household Favorites"
- Star rating / usage count display
- Quick-add to current list (one-click)
- Add new favorite
- Edit/remove favorites
- Search favorites
- Sort by: Most Used, Alphabetical, Recently Added

**API Methods Used**:
- `ShoppingListApiClient.GetFavoritesAsync(userId)`
- `ShoppingListApiClient.GetHouseholdFavoritesAsync(householdId)`
- `ShoppingListApiClient.AddFavoriteAsync(request)`
- `ShoppingListApiClient.RemoveFavoriteAsync(favoriteId)`
- `ShoppingListApiClient.AddFavoriteToListAsync(favoriteId, listId)`

**Estimated LOC**: ~300

**UI Layout**:
```
┌─────────────────────────────────────┐
│ ⭐ Favorite Items                   │
│ [My Favorites] [Household]          │
├─────────────────────────────────────┤
│ [Search favorites...]               │
├─────────────────────────────────────┤
│ ⭐⭐⭐⭐⭐ Milk (Whole)              │
│ Used 47 times                       │
│ [Quick Add to List] [Remove]        │
│                                     │
│ ⭐⭐⭐⭐ Bread (Wheat)                │
│ Used 32 times                       │
│ [Quick Add to List] [Remove]        │
└─────────────────────────────────────┘
```

---

### Priority 3.2: Store Selector with GPS
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Shared/StoreSelector.razor`

**Purpose**: Select store with nearby detection

**Features**:
- "Find Nearby Stores" button (GPS)
- List stores with distance
- Set preferred store (star icon)
- Store hours display
- Store layout preview link
- Manual store selection
- Add new store

**API Methods Used**:
- `ShoppingListApiClient.GetNearbyStoresAsync(request)`
- `ShoppingListApiClient.GetStoresAsync()`
- `ShoppingListApiClient.SetPreferredStoreAsync(storeId)`
- `ShoppingListApiClient.CreateStoreAsync(request)`

**Estimated LOC**: ~250

---

### Priority 3.3: Price Comparison Panel
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Shopping/PriceComparisonPanel.razor`

**Purpose**: Compare prices across stores for shopping list items

**Features**:
- Product name with image
- Price per store in table format
- Best price highlighting (green)
- Deal badges (BOGO, 50% OFF, etc.)
- Unit price comparison ($/oz, $/lb)
- "Best Value" indicator
- Last price update date
- Store distance from home

**API Methods Used**:
- `ShoppingListApiClient.GetBestPricesAsync(productId)`
- `ShoppingListApiClient.GetPriceComparisonsAsync(itemId)`
- `ShoppingListApiClient.RecordPriceComparisonAsync(request)`

**Estimated LOC**: ~350

**UI Layout**:
```
┌─────────────────────────────────────┐
│ 💰 Price Comparison                 │
│ Kraft Ketchup 32oz                  │
├─────────────────────────────────────┤
│ Store          Price    Unit Price  │
├─────────────────────────────────────┤
│ 🟢 Walmart    $5.99    $0.19/oz    │
│    1.2 mi     BEST PRICE            │
│                                     │
│ Target        $6.49    $0.20/oz     │
│ 2.1 mi                              │
│                                     │
│ Kroger        $5.99    $0.19/oz     │
│ 1.8 mi        BOGO 50% OFF 🎉      │
└─────────────────────────────────────┘
```

---

### Priority 3.4: Templates Manager
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Shopping/TemplatesManager.razor`

**Purpose**: Manage reusable shopping list templates

**Features**:
- List of templates (Weekly, Monthly, Staples, etc.)
- Template usage count
- Create new template
- Edit template (add/remove items)
- Create list from template (one-click)
- Share template with household
- Duplicate template

**API Methods Used**:
- `ShoppingListApiClient.GetTemplatesAsync(userId)`
- `ShoppingListApiClient.GetHouseholdTemplatesAsync(householdId)`
- `ShoppingListApiClient.CreateTemplateAsync(request)`
- `ShoppingListApiClient.GetTemplateItemsAsync(templateId)`
- `ShoppingListApiClient.AddItemToTemplateAsync(templateId, request)`
- `ShoppingListApiClient.CreateListFromTemplateAsync(templateId, request)`

**Estimated LOC**: ~400

---

### Priority 3.5: Shopping Scanner Mode
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Shopping/ShoppingScannerMode.razor`

**Purpose**: Lock mode scanning for purchasing items

**Features**:
- Active shopping list context
- Scan item barcode to check off
- Record actual price paid
- Running total display
- Item checkoff visual feedback
- Session tracking
- "Auto-add to Inventory" toggle
- Receipt summary at end

**API Methods Used**:
- `ShoppingListApiClient.StartShoppingScanSessionAsync(request)`
- `ShoppingListApiClient.GetActiveShoppingScanSessionAsync(userId)`
- `ShoppingListApiClient.ScanPurchaseItemAsync(sessionId, request)`
- `ShoppingListApiClient.EndShoppingScanSessionAsync(sessionId)`
- `ShoppingListApiClient.AddPurchasedToInventoryAsync(request)`

**Estimated LOC**: ~400

**UI Layout**:
```
┌─────────────────────────────────────┐
│ 🛒 SHOPPING MODE                    │
│ Walmart Shopping Trip                │
│ Running Total: $47.23               │
├─────────────────────────────────────┤
│ [Barcode Input Field          ] 🔍 │
├─────────────────────────────────────┤
│ Last Scanned:                       │
│ ✓ Milk $3.99 (Est: $3.79)         │
│                                     │
│ List Items (12 total, 8 checked):   │
│ ✓ Bread $2.49                      │
│ ✓ Eggs $4.29                       │
│ ✓ Milk $3.99                       │
│ ⬜ Cheese                           │
│ ⬜ Butter                           │
├─────────────────────────────────────┤
│ ☑️ Auto-add to Inventory           │
│ [End Shopping] [More Items]         │
└─────────────────────────────────────┘
```

---

### Priority 3.6: Enhance ShoppingLists.razor
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Shopping/ShoppingLists.razor` (Existing)

**Enhancements Needed**:
1. Add HouseholdSwitcher to header
2. Add quick action buttons:
   - "From Favorites" (open favorites panel)
   - "From Template" (open template picker)
   - "From Low Stock" (create list from inventory)
3. Add store filter/assignment
4. Show price totals per list
5. Show store name on list cards
6. Add "Scanner Mode" button

**Estimated LOC**: +200

---

### Priority 3.7: Enhance ShoppingListDetails.razor
**File**: `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Shopping/ShoppingListDetails.razor` (Existing)

**Enhancements Needed**:
1. Store assignment selector
2. Star icon to favorite items
3. Price tracking per item
4. Best price indicator
5. "Scanner Mode" button
6. "Compare Prices" toggle
7. Show price comparison panel
8. Unit price display

**Estimated LOC**: +300

---

## Phase 4: Mobile Optimization (MAUI) 📱

### Priority 4.1: Camera Barcode Scanning
**Platform**: .NET MAUI (Android/iOS)

**Features**:
- Use device camera for barcode scanning
- Real-time detection
- Vibration feedback on successful scan
- Flash/torch control
- Manual entry fallback

**Estimated LOC**: ~200 (MAUI-specific)

---

### Priority 4.2: GPS Services Integration
**Platform**: .NET MAUI (Android/iOS)

**Features**:
- Request location permissions
- Get current location
- Pass to address detection
- Background location (optional)
- Location accuracy controls

**Estimated LOC**: ~150 (MAUI-specific)

---

## Implementation Summary

### Total Estimated LOC: ~4,000

**Phase 1 (Shared Components)**: ~500 LOC
- HouseholdSwitcher: 150
- AddressSelector: 200
- StorageLocationSelector: 150

**Phase 2 (Inventory)**: ~1,250 LOC
- InventoryScannerMode: 400
- Inventory.razor enhancements: 200
- InventoryReportsDashboard: 500
- AllergenDiscoveryPanel: 150

**Phase 3 (Shopping)**: ~2,200 LOC
- FavoritesPanel: 300
- StoreSelector: 250
- PriceComparisonPanel: 350
- TemplatesManager: 400
- ShoppingScannerMode: 400
- ShoppingLists enhancements: 200
- ShoppingListDetails enhancements: 300

**Phase 4 (Mobile)**: ~350 LOC
- Camera scanning: 200
- GPS services: 150

### Implementation Order

**Week 1-2**: Shared Components
1. HouseholdSwitcher
2. AddressSelector
3. StorageLocationSelector

**Week 3-4**: Inventory Enhancements
4. InventoryScannerMode
5. Enhance Inventory.razor
6. InventoryReportsDashboard
7. AllergenDiscoveryPanel

**Week 5-6**: Shopping Enhancements
8. FavoritesPanel
9. StoreSelector
10. PriceComparisonPanel
11. TemplatesManager

**Week 7-8**: Shopping Advanced
12. ShoppingScannerMode
13. Enhance ShoppingLists.razor
14. Enhance ShoppingListDetails.razor

**Week 9**: Mobile Features (MAUI)
15. Camera scanning
16. GPS services

**Week 10**: Testing & Polish
- Component testing
- Responsive design
- Accessibility
- Performance optimization

---

## Success Metrics

### Functionality
- ✅ All 91 API methods integrated
- ✅ All 14 new components created
- ✅ Zero broken existing features
- ✅ All pages enhanced

### Performance
- ⚡ Page load < 2 seconds
- ⚡ API calls < 500ms
- ⚡ Smooth animations (60fps)

### User Experience
- 📱 Mobile-responsive
- ♿ WCAG 2.1 AA compliance
- 🎨 Consistent design language
- 💡 Intuitive navigation

### Quality
- 🧪 Component tests passing
- 🐛 Zero critical bugs
- 📊 Error tracking in place
- 📝 Code documentation complete

---

## Next Steps

1. **Start with HouseholdSwitcher** - It's needed by almost every other component
2. **Then AddressSelector** - Second most used component
3. **Build Scanner Modes** - High-value, user-facing features
4. **Add Reports** - Data visualization and insights
5. **Enhance Shopping** - Price comparison and templates
6. **Mobile Polish** - Camera and GPS
7. **Test Everything** - Comprehensive testing

This plan provides a complete roadmap for building a world-class inventory and shopping management UI! 🚀
