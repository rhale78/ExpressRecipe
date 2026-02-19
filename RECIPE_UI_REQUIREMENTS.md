# Recipe Management UI - Feature Requirements

**Created**: 2026-02-16  
**Priority**: High  
**Status**: Not Started  
**Related PR**: #[PR Number] - Recipe Enhancement Backend Implementation  
**Branch**: copilot/modify-recipe-parsing-feature

---

## Overview

Create a comprehensive, user-friendly UI for recipe management that integrates with the backend APIs implemented in the recipe enhancement work. The UI must match the existing ExpressRecipe design language and provide seamless recipe creation, editing, import/export, and integration with shopping cart and inventory systems.

---

## Core Requirements

### 1. Recipe CRUD Operations

#### 1.1 Create Recipe
- **WYSIWYG Editor** for recipe instructions
  - Rich text formatting (bold, italic, lists)
  - Automatic step numbering
  - Support for embedded images in steps
  - Real-time preview mode
  
- **Ingredient Management**
  - Free-form text input (primary) - users can type any ingredient
  - Auto-linking to products (optional) - suggests matching products as user types
  - Quick add ingredient button
  - Simple up/down buttons for reordering (NO drag-and-drop)
  - Unit field (text input with common suggestions)
  - Quantity validation
  - Optional ingredient checkbox
  - Ingredient grouping (e.g., "For the sauce", "For the dough")
  - Preparation notes field
  - Visual indicator when ingredient is linked to a product
  - Never block or restrict free-form text entry
  
- **Recipe Metadata**
  - Title (required)
  - Description (required)
  - Category dropdown (Breakfast, Lunch, Dinner, Dessert, Snack, etc.)
  - Cuisine type
  - Difficulty level (Easy, Medium, Hard)
  - Prep time (minutes)
  - Cook time (minutes)
  - Servings
  - Tags (multi-select or comma-separated)
  - Source/attribution
  - Image upload
  
- **Validation**
  - Required field indicators
  - Real-time validation feedback
  - Duplicate recipe name detection
  
#### 1.2 Edit Recipe
- Load existing recipe into form
- Preserve all original data
- Show modification timestamp
- Confirm before discarding changes
- Save draft functionality

#### 1.3 View Recipe
- Clean, print-friendly layout
- Serving size adjuster with real-time quantity updates
- Fractional display (1/2, 1/4, 2 1/2, etc.)
- "Add to Shopping List" button
- Rating display and input
- Share options
- Print recipe button
- Export options

#### 1.4 Delete Recipe
- Soft delete with confirmation dialog
- Option to permanently delete or archive
- Show warning if recipe is in meal plans or favorites

### 2. Recipe Import/Export (Critical)

#### 2.1 Import Sources
- **YouTube URL**: Parse video description
- **Website URL**: Scrape from supported sites (AllRecipes, Food Network, etc.)
- **Paste Text**: Intelligent parsing of plain text recipes
- **File Upload**: 
  - Markdown (.md)
  - JSON (.json)
  - MealMaster (.mmf)
  - MasterCook (.mx2, .mxp)
  - Paprika (.paprikarecipe)
- **From Disk**: Auto-load user's saved recipes (see 2.3)

#### 2.2 Import UI Flow
1. Select import method (tabs or dropdown)
2. Provide source (URL, file, text)
3. Preview parsed recipe with edit capability
4. Validate and save
5. Show success/error feedback

#### 2.3 Export to Disk (Critical Feature)

**Requirement**: Recipes must be automatically saved to disk for data preservation and portability.

**File Format**: Markdown (.md)
- Human-readable
- Easy to version control
- Can be edited in any text editor
- Preserves formatting

**File Naming Convention**:
```
[username_or_email]/[safe_recipe_name]_[recipe_id].md
```

**Example**:
```
john.doe@example.com/chocolate_chip_cookies_a1b2c3d4.md
jane_smith/beef_stew_e5f6g7h8.md
```

**Character Sanitization**:
- Replace invalid filesystem characters: `\ / : * ? " < > |`
- Replace with underscores or hyphens
- Handle Unicode characters (normalize or transliterate)
- Limit filename length (max 255 characters)
- Handle duplicate names with numeric suffix

**Storage Location**:
```
[AppDataDirectory]/RecipeExports/[username_or_email]/
```

**Markdown Format**:
```markdown
---
id: a1b2c3d4-5678-90ab-cdef-1234567890ab
title: Chocolate Chip Cookies
author: john.doe@example.com
created: 2026-02-15T10:30:00Z
updated: 2026-02-16T14:20:00Z
category: Dessert
cuisine: American
difficulty: Easy
servings: 24
prep_time: 15
cook_time: 12
tags: [cookies, dessert, baking, chocolate]
---

# Chocolate Chip Cookies

A classic recipe for chewy, delicious chocolate chip cookies.

## Ingredients

- 2 1/4 cups all-purpose flour
- 1 tsp baking soda
- 1 tsp salt
- 1 cup butter, softened
- 3/4 cup sugar
- 3/4 cup brown sugar
- 2 large eggs
- 2 tsp vanilla extract
- 2 cups chocolate chips

## Instructions

1. Preheat oven to 375°F (190°C).

2. Mix flour, baking soda, and salt in a bowl.

3. Beat butter and sugars until creamy. Add eggs and vanilla.

4. Gradually blend in flour mixture. Stir in chocolate chips.

5. Drop rounded tablespoons onto ungreased cookie sheets.

6. Bake 9-11 minutes or until golden brown.

## Notes

Store in airtight container. Cookies stay fresh for up to 5 days.

## Nutrition (per serving)

- Calories: 150
- Fat: 8g
- Carbs: 18g
- Protein: 2g

## Ratings

- Overall: 4.5/5 (12 ratings)
- John: 5/5 - "Best cookies ever!"
- Mary: 4/5 - "Very good, would reduce sugar slightly"
```

#### 2.4 Auto-Save/Auto-Load Functionality

**Auto-Save Triggers**:
- On recipe creation (immediate save to disk)
- On recipe update (overwrite existing file)
- On recipe deletion (move to `_deleted/` subfolder with timestamp)
- Periodic background save (every 5 minutes if changes detected)

**Auto-Load on Startup**:
- Scan user's export directory on login
- Compare disk files with database
- If disk file is newer → prompt user to restore
- If disk file doesn't exist in DB → prompt to import
- If disk file is older → sync from database
- Background sync check every 30 minutes while app is open

**Conflict Resolution**:
- Detect conflicts: disk modified + DB modified since last sync
- Show diff view with side-by-side comparison
- Allow user to choose: Keep disk version, Keep DB version, Merge manually
- Create backup of both versions

**File Watcher**:
- Watch export directory for external changes
- If file added → prompt to import
- If file modified externally → prompt to reload
- If file deleted → flag in database as "deleted externally"

**Batch Operations**:
- "Export All Recipes" button → save all user recipes to disk
- "Import All from Disk" button → scan and import all markdown files
- "Sync with Disk" button → reconcile all differences

### 3. Recipe Browsing & Search

#### 3.1 List View
- Grid or list layout toggle
- Recipe cards with:
  - Image thumbnail
  - Title
  - Rating stars
  - Prep/cook time
  - Serving size
  - Quick actions (view, edit, delete, favorite)

#### 3.2 Filtering
- Category dropdown
- Cuisine dropdown
- Difficulty level
- Prep time range slider
- Cook time range slider
- Dietary filters (Vegetarian, Vegan, Gluten-Free, etc.)
- Ingredient inclusion/exclusion
- Tags (multi-select)

#### 3.3 Search
- Full-text search box
- Search by recipe name, ingredients, tags, instructions
- Real-time results as you type
- Search history
- Saved searches

#### 3.4 Sorting
- Name (A-Z, Z-A)
- Date created (newest, oldest)
- Rating (highest, lowest)
- Prep time (shortest, longest)
- Cook time (shortest, longest)
- Most viewed
- Most favorited

### 4. Rating System UI

#### 4.1 Family Member Management
- "Manage Family Members" page/modal
- Add family member form:
  - Name (required)
  - Nickname (optional)
  - Birth date (optional)
  - Active/inactive toggle
  - Display order (drag to reorder)
- Edit/delete family members
- List view with avatars

#### 4.2 Rating Input
- **Star Widget**:
  - 5 stars with half-star support
  - Interactive hover states
  - Click to rate (0.5 increments)
  - Visual feedback on selection
  - Display current rating
  
- **Family Member Selector**:
  - Dropdown or button group
  - "Me" option (default)
  - List of family members
  - Show existing ratings for each member
  
- **Review Form**:
  - Text area for review (optional)
  - "Would make again?" checkbox
  - "I made this" date picker
  - "Made it count" counter with +/- buttons
  - Submit/cancel buttons

#### 4.3 Rating Display
- Overall average rating (large, prominent)
- Total number of ratings
- Star distribution chart (5-star: X, 4-star: Y, etc.)
- Individual family ratings section:
  - Avatar/name
  - Star rating
  - Review text (if provided)
  - Date rated
  - "Would make again" badge
- Filter to show only your family's ratings

### 5. Serving Size Adjuster

#### 5.1 UI Components
- **Serving Size Selector**:
  - Number input with +/- buttons
  - Quick select buttons (2, 4, 6, 8, 12)
  - Shows original serving size
  - Real-time calculation

#### 5.2 Ingredient Display
- Show both original and scaled quantities
- Toggle between decimal and fractional display
- Highlight changed values
- Example: `1 cup` → `2 cups` (doubled)
- Example: `1/2 tsp` → `1 tsp` (doubled)

#### 5.3 Time Adjustment
- Show estimated prep time adjustment
- Show estimated cook time adjustment
- Display note about time scaling (for large batches)

### 6. Shopping List Integration

#### 6.1 "Add to Shopping List" Button
- Prominent button on recipe view page
- Opens modal/dialog with options
- Select serving size for shopping
- Show ingredient list with quantities
- Checkboxes to include/exclude ingredients

#### 6.2 Inventory Check
- Query InventoryService for on-hand items
- Gray out or mark ingredients already in inventory
- Show quantity in stock vs. quantity needed
- "Skip items I already have" checkbox

#### 6.3 Product Matching
- For each ingredient, show suggested products
- Display package size optimization
  - Example: "Need 10oz, recommend 16oz bottle"
  - Show excess amount
- Allow user to select different product
- Show price if available

#### 6.4 Confirmation
- Review list before adding
- Show total estimated cost
- "Add to Shopping List" confirmation
- Success message with link to shopping list

### 7. Category Management

#### 7.1 Category CRUD
- View all categories (admin/power user feature)
- Add new category
- Edit category name/description
- Delete category (with warning if recipes exist)
- Reorder categories (simple up/down buttons)

#### 7.2 Category Assignment
- Assign category during recipe creation
- Change category in recipe edit
- Bulk category assignment for multiple recipes
- Suggest category based on recipe content (ML)

### 8. Unit & Conversion Management

#### 8.1 Unit Selector
- Dropdown with common units grouped:
  - Volume: cup, tbsp, tsp, fl oz, ml, liter
  - Weight: oz, lb, g, kg
  - Count: piece, whole, clove, slice
  - Special: pinch, dash, to taste

#### 8.2 Unit Conversion
- "Convert units" button on ingredient
- Select target unit from dropdown
- Automatic conversion calculation
- Handle volume/weight/count conversions
- Show conversion note (e.g., "approximately")

#### 8.3 Custom Units
- Allow users to add custom units
- Define conversion factors
- Validate against standard units

### 9. UI Design Consistency

#### 9.1 Design System
- Use existing ExpressRecipe design tokens:
  - Colors (primary, secondary, accent, neutral)
  - Typography (fonts, sizes, weights)
  - Spacing (margins, padding, gaps)
  - Border radius, shadows, animations
  
#### 9.2 Components
- Reuse existing Blazor components:
  - Buttons (primary, secondary, outline, icon)
  - Form inputs (text, number, select, textarea)
  - Cards, modals, alerts, toasts
  - Navigation (breadcrumbs, tabs, menu)
  - Loading spinners, skeletons
  
#### 9.3 Responsive Design
- Mobile-first approach
- Breakpoints: mobile (< 768px), tablet (768-1024px), desktop (> 1024px)
- Touch-friendly targets (minimum 44x44px)
- Swipe gestures for mobile

#### 9.4 Accessibility
- WCAG 2.1 Level AA compliance
- Keyboard navigation support
- Screen reader compatibility
- Focus indicators
- Color contrast ratios
- Alt text for images

### 10. Validation & Error Handling

#### 10.1 Form Validation
- Required field indicators (*)
- Real-time validation feedback
- Error messages below fields
- Disable submit until valid
- Show validation summary at top

#### 10.2 API Error Handling
- Display user-friendly error messages
- Retry logic for network failures
- Timeout handling
- Show loading states during API calls
- Success/error toasts

#### 10.3 Data Validation
- Ingredient name required
- Quantity must be positive number
- Unit must be from valid list
- Recipe name uniqueness check
- Maximum length validation
- Sanitize input to prevent XSS

---

## Technical Implementation

### Technology Stack
- **Frontend Framework**: Blazor Server (primary) / Blazor WebAssembly
- **Component Library**: Existing ExpressRecipe components
- **State Management**: Blazor's built-in state management
- **API Client**: IRecipeApiClient, IRatingsApiClient, IShoppingApiClient
- **File I/O**: System.IO with proper error handling
- **Markdown Parsing**: Markdig library
- **Rich Text Editor**: Quill.js or TinyMCE (via JSInterop)

### Project Structure
```
src/Frontends/ExpressRecipe.BlazorWeb/
├── Components/
│   ├── Pages/
│   │   ├── Recipes/
│   │   │   ├── RecipeList.razor
│   │   │   ├── RecipeDetails.razor
│   │   │   ├── CreateRecipe.razor (enhance existing)
│   │   │   ├── EditRecipe.razor
│   │   │   ├── ImportRecipe.razor (enhance existing)
│   │   │   ├── ManageCategories.razor
│   │   │   └── FamilyMembers.razor
│   ├── Shared/
│   │   ├── RecipeCard.razor
│   │   ├── StarRating.razor (new)
│   │   ├── IngredientEditor.razor (new)
│   │   ├── ServingSizeAdjuster.razor (new)
│   │   ├── RecipeSearch.razor (new)
│   │   └── RecipeFilters.razor (new)
├── Services/
│   ├── RecipeFileService.cs (new - disk I/O)
│   ├── RecipeSyncService.cs (new - auto-save/load)
│   ├── RecipeApiClient.cs (enhance)
│   ├── RatingsApiClient.cs (new)
│   └── ShoppingIntegrationService.cs (new)
├── Models/
│   ├── RecipeViewModel.cs
│   ├── RatingViewModel.cs
│   └── FamilyMemberViewModel.cs
├── wwwroot/
│   ├── js/
│   │   ├── recipe-editor.js (Quill integration)
│   │   └── file-watcher.js (optional)
│   └── css/
│       └── recipe-styles.css
```

### API Endpoints (Already Implemented)
```
RecipesController:
- GET    /api/recipes
- GET    /api/recipes/{id}
- POST   /api/recipes
- PUT    /api/recipes/{id}
- DELETE /api/recipes/{id}
- GET    /api/recipes/my-recipes
- GET    /api/recipes/by-category/{category}
- GET    /api/recipes/by-cuisine/{cuisine}
- GET    /api/recipes/by-meal-type/{mealType}
- GET    /api/recipes/by-ingredient?ingredient={name}
- GET    /api/recipes/categories
- GET    /api/recipes/cuisines
- GET    /api/recipes/{id}/scale?servings={n}
- GET    /api/recipes/{id}/serving-suggestions
- POST   /api/recipes/{id}/prepare-shopping-list?servings={n}

RatingsController:
- GET    /api/ratings/family-members
- GET    /api/ratings/family-members/{id}
- POST   /api/ratings/family-members
- PUT    /api/ratings/family-members/{id}
- DELETE /api/ratings/family-members/{id}
- GET    /api/ratings/recipes/{recipeId}/summary
- GET    /api/ratings/recipes/{recipeId}
- POST   /api/ratings/recipes
- GET    /api/ratings/recipes/rating/{id}
- DELETE /api/ratings/recipes/rating/{id}
```

### File Service Implementation (RecipeFileService.cs)

```csharp
public class RecipeFileService
{
    private readonly string _baseExportPath;
    private readonly ILogger<RecipeFileService> _logger;

    public RecipeFileService(IConfiguration config, ILogger<RecipeFileService> logger)
    {
        _baseExportPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ExpressRecipe",
            "RecipeExports"
        );
        _logger = logger;
        EnsureDirectoryExists(_baseExportPath);
    }

    public async Task<string> ExportRecipeAsync(RecipeDto recipe, string userIdentifier)
    {
        var userDir = GetUserDirectory(userIdentifier);
        EnsureDirectoryExists(userDir);

        var fileName = SanitizeFileName($"{recipe.Name}_{recipe.Id}.md");
        var filePath = Path.Combine(userDir, fileName);

        var markdown = ConvertRecipeToMarkdown(recipe);
        await File.WriteAllTextAsync(filePath, markdown);

        _logger.LogInformation("Exported recipe {RecipeId} to {FilePath}", recipe.Id, filePath);
        return filePath;
    }

    public async Task<RecipeDto?> ImportRecipeFromDiskAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var markdown = await File.ReadAllTextAsync(filePath);
        return ParseMarkdownToRecipe(markdown);
    }

    public async Task<List<string>> ScanUserRecipesAsync(string userIdentifier)
    {
        var userDir = GetUserDirectory(userIdentifier);
        if (!Directory.Exists(userDir))
            return new List<string>();

        return Directory.GetFiles(userDir, "*.md", SearchOption.TopDirectoryOnly).ToList();
    }

    public async Task DeleteRecipeFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        // Move to _deleted subfolder with timestamp
        var deletedDir = Path.Combine(Path.GetDirectoryName(filePath)!, "_deleted");
        EnsureDirectoryExists(deletedDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = Path.GetFileName(filePath);
        var deletedPath = Path.Combine(deletedDir, $"{timestamp}_{fileName}");

        File.Move(filePath, deletedPath);
        _logger.LogInformation("Moved deleted recipe to {DeletedPath}", deletedPath);
    }

    private string SanitizeFileName(string fileName)
    {
        // Remove invalid characters
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        // Additional replacements
        sanitized = sanitized
            .Replace(':', '_')
            .Replace('*', '_')
            .Replace('?', '_')
            .Replace('"', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('|', '_')
            .Replace('/', '_')
            .Replace('\\', '_');

        // Limit length
        if (sanitized.Length > 200)
            sanitized = sanitized.Substring(0, 200);

        return sanitized;
    }

    private string GetUserDirectory(string userIdentifier)
    {
        var sanitizedUser = SanitizeFileName(userIdentifier);
        return Path.Combine(_baseExportPath, sanitizedUser);
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private string ConvertRecipeToMarkdown(RecipeDto recipe)
    {
        var sb = new StringBuilder();
        
        // YAML frontmatter
        sb.AppendLine("---");
        sb.AppendLine($"id: {recipe.Id}");
        sb.AppendLine($"title: {recipe.Name}");
        sb.AppendLine($"created: {recipe.CreatedAt:O}");
        if (recipe.UpdatedAt.HasValue)
            sb.AppendLine($"updated: {recipe.UpdatedAt:O}");
        if (!string.IsNullOrEmpty(recipe.Category))
            sb.AppendLine($"category: {recipe.Category}");
        if (!string.IsNullOrEmpty(recipe.Cuisine))
            sb.AppendLine($"cuisine: {recipe.Cuisine}");
        if (!string.IsNullOrEmpty(recipe.DifficultyLevel))
            sb.AppendLine($"difficulty: {recipe.DifficultyLevel}");
        if (recipe.Servings.HasValue)
            sb.AppendLine($"servings: {recipe.Servings}");
        if (recipe.PrepTimeMinutes.HasValue)
            sb.AppendLine($"prep_time: {recipe.PrepTimeMinutes}");
        if (recipe.CookTimeMinutes.HasValue)
            sb.AppendLine($"cook_time: {recipe.CookTimeMinutes}");
        sb.AppendLine("---");
        sb.AppendLine();

        // Title
        sb.AppendLine($"# {recipe.Name}");
        sb.AppendLine();

        // Description
        if (!string.IsNullOrEmpty(recipe.Description))
        {
            sb.AppendLine(recipe.Description);
            sb.AppendLine();
        }

        // Ingredients
        if (recipe.Ingredients?.Any() == true)
        {
            sb.AppendLine("## Ingredients");
            sb.AppendLine();
            foreach (var ingredient in recipe.Ingredients.OrderBy(i => i.OrderIndex))
            {
                var quantity = ingredient.Quantity.HasValue ? ingredient.Quantity.Value.ToString("0.##") : "";
                var unit = ingredient.Unit ?? "";
                var name = ingredient.IngredientName ?? "";
                var optional = ingredient.IsOptional ? " (optional)" : "";
                sb.AppendLine($"- {quantity} {unit} {name}{optional}".Trim());
            }
            sb.AppendLine();
        }

        // Instructions
        if (!string.IsNullOrEmpty(recipe.Instructions))
        {
            sb.AppendLine("## Instructions");
            sb.AppendLine();
            sb.AppendLine(recipe.Instructions);
            sb.AppendLine();
        }

        // Notes
        if (!string.IsNullOrEmpty(recipe.Notes))
        {
            sb.AppendLine("## Notes");
            sb.AppendLine();
            sb.AppendLine(recipe.Notes);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private RecipeDto? ParseMarkdownToRecipe(string markdown)
    {
        // Parse YAML frontmatter and markdown content
        // Implementation using Markdig or YamlDotNet
        // Extract metadata and content sections
        // Return RecipeDto
        throw new NotImplementedException("Markdown parsing to be implemented");
    }
}
```

### Sync Service Implementation (RecipeSyncService.cs)

```csharp
public class RecipeSyncService : IHostedService, IDisposable
{
    private Timer? _timer;
    private readonly RecipeFileService _fileService;
    private readonly IRecipeApiClient _apiClient;
    private readonly ILogger<RecipeSyncService> _logger;

    public RecipeSyncService(
        RecipeFileService fileService,
        IRecipeApiClient apiClient,
        ILogger<RecipeSyncService> logger)
    {
        _fileService = fileService;
        _apiClient = apiClient;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recipe Sync Service starting");
        
        // Start periodic sync (every 30 minutes)
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));
        
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        _logger.LogInformation("Running recipe sync...");
        
        try
        {
            // Get current user (from auth context)
            var userIdentifier = GetCurrentUserIdentifier();
            if (string.IsNullOrEmpty(userIdentifier))
                return;

            // Scan disk for recipe files
            var diskFiles = await _fileService.ScanUserRecipesAsync(userIdentifier);
            
            // Get recipes from database
            var dbRecipes = await _apiClient.GetMyRecipesAsync();
            
            // Compare and sync
            await SyncRecipesAsync(userIdentifier, diskFiles, dbRecipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during recipe sync");
        }
    }

    private async Task SyncRecipesAsync(
        string userIdentifier, 
        List<string> diskFiles, 
        List<RecipeDto> dbRecipes)
    {
        // Compare timestamps and sync logic
        // Implementation details...
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recipe Sync Service stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
```

---

## User Stories

### US-1: Create Recipe
**As a** user  
**I want to** create a new recipe with ingredients and instructions  
**So that** I can save my favorite recipes

**Acceptance Criteria**:
- Can enter recipe name, description, category, cuisine, difficulty
- Can add multiple ingredients with quantities and units
- Can add step-by-step instructions
- Can upload recipe image
- Recipe is saved to database
- Recipe is automatically exported to disk in markdown format
- Success message shown after save

### US-2: Import Recipe from YouTube
**As a** user  
**I want to** paste a YouTube URL and have the recipe extracted  
**So that** I can quickly save recipes from cooking videos

**Acceptance Criteria**:
- Can paste YouTube URL in import form
- Parser extracts ingredients and instructions from video description
- Preview shows parsed recipe with ability to edit
- Can save parsed recipe to database
- Recipe is exported to disk automatically

### US-3: Rate Recipe with Family Members
**As a** user  
**I want to** rate a recipe for each family member  
**So that** I can track everyone's preferences

**Acceptance Criteria**:
- Can add family members (name, nickname)
- Can rate recipe 0-5 stars in 0.5 increments for each member
- Can add review text for each rating
- Overall rating calculated and displayed
- Rating breakdown shown by family member

### US-4: Adjust Serving Size
**As a** user  
**I want to** change the serving size and see updated ingredient quantities  
**So that** I can scale recipes for different occasions

**Acceptance Criteria**:
- Can input new serving size
- Ingredient quantities update in real-time
- Displays both decimal and fractional formats
- Shows estimated time adjustments
- Can toggle between formats

### US-5: Add Recipe to Shopping List
**As a** user  
**I want to** add recipe ingredients to my shopping list  
**So that** I can shop for everything I need

**Acceptance Criteria**:
- "Add to Shopping List" button visible on recipe page
- Can select serving size for shopping
- Shows which ingredients are already in inventory
- Suggests product package sizes
- Adds selected items to shopping list
- Success confirmation shown

### US-6: Auto-Save to Disk
**As a** user  
**I want to** have my recipes automatically saved to disk  
**So that** I don't lose my data if the database is reset

**Acceptance Criteria**:
- Recipes saved to disk immediately on creation
- Recipes updated on disk when edited
- Deleted recipes moved to _deleted folder
- Can see file path where recipe is saved
- Recipes organized by username/email

### US-7: Auto-Load from Disk
**As a** user  
**I want to** have my recipes loaded from disk on startup  
**So that** I can restore my recipes easily

**Acceptance Criteria**:
- App scans disk directory on login
- Prompts if disk files are newer than database
- Can choose to restore from disk
- Can import new recipes found on disk
- Shows sync status indicator

### US-8: Manage Categories
**As a** power user  
**I want to** create and manage recipe categories  
**So that** I can organize recipes my way

**Acceptance Criteria**:
- Can view all categories
- Can add new category
- Can edit category name
- Can delete category (with warning)
- Can reorder categories
- Categories appear in dropdown when creating recipe

---

## Testing Requirements

### Unit Tests
- RecipeFileService: Export/import, sanitization, directory management
- RecipeSyncService: Sync logic, conflict detection
- Markdown parsing: Frontmatter, content sections
- Filename sanitization: Special characters, length limits

### Integration Tests
- Create recipe → verify disk export
- Edit recipe → verify disk update
- Delete recipe → verify moved to _deleted
- Auto-load → scan disk, detect new files
- Sync conflict → user prompt, resolution

### UI Tests
- Recipe form validation
- Ingredient autocomplete/auto-linking
- Serving size real-time update
- Rating star interaction
- Shopping list integration
- Import flow end-to-end

### Performance Tests
- Large recipe list (1000+ recipes)
- Bulk export (100+ recipes)
- Sync with many conflicts
- Real-time search/filter responsiveness

---

## Security Considerations

- Sanitize all user input to prevent XSS
- Validate file paths to prevent directory traversal
- Limit file sizes for uploads
- Rate limit API calls
- Encrypt sensitive data in exported files (optional)
- Validate user permissions before file operations
- Use authentication for all recipe operations

---

## Accessibility Requirements

- Keyboard navigation for all forms and controls
- Screen reader support (ARIA labels)
- Focus management for modals and dialogs
- Color contrast ratios meet WCAG 2.1 AA
- Touch targets minimum 44x44px
- Alternative text for all images
- Form validation announcements

---

## Performance Goals

- Recipe list loads < 1 second
- Real-time serving size update < 100ms
- Export to disk < 500ms per recipe
- Import parsing < 2 seconds
- Search results < 500ms
- Page transitions < 300ms

---

## Deployment Checklist

- [ ] Implement RecipeFileService
- [ ] Implement RecipeSyncService
- [ ] Create UI components
- [ ] Add markdown parsing (Markdig)
- [ ] Add rich text editor (Quill.js)
- [ ] Implement ingredient auto-linking
- [ ] Register services in DI
- [ ] Add file I/O permissions
- [ ] Configure export directory path
- [ ] Add background sync service
- [ ] Write unit tests
- [ ] Write integration tests
- [ ] Perform UI/UX testing
- [ ] Accessibility audit
- [ ] Performance testing
- [ ] Security review
- [ ] Documentation
- [ ] User training materials

---

## Future Enhancements

- Cloud sync (Azure Blob Storage, AWS S3)
- Recipe versioning with git-like history
- Collaborative editing with real-time updates
- Recipe recommendations based on preferences
- Meal planning integration
- Nutritional analysis with USDA database
- Voice input for hands-free cooking
- AR overlay for cooking instructions
- Social sharing and recipe communities
- Recipe collections/cookbooks
- Print-optimized layouts
- Multi-language support

---

## Questions for Product Owner

1. Should export directory be configurable by user?
2. What happens if disk is full during export?
3. Should we support cloud backup as well as local?
4. Maximum number of family members per user?
5. Should ratings be exportable too?
6. Import conflict resolution strategy preference?
7. File watcher needed for external edits?
8. Support for importing recipe images from markdown?

---

**End of Requirements Document**

This document should be reviewed and approved before implementation begins. Updates and refinements are expected as requirements are clarified.
