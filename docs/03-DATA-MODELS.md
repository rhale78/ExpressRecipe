# ExpressRecipe - Data Models & Database Schemas

## Database Strategy

### Per-Service Databases
Each microservice owns its database to ensure:
- Service independence
- Schema evolution autonomy
- Technology flexibility
- Fault isolation
- Scalability per service

### Naming Convention
- Database: `ExpressRecipe.{ServiceName}`
- Tables: PascalCase, singular (e.g., `Product`, `Recipe`)
- Columns: PascalCase
- Indexes: `IX_{TableName}_{ColumnNames}`
- Foreign Keys: `FK_{ParentTable}_{ChildTable}_{Column}`
- Primary Keys: `PK_{TableName}`

### Common Patterns

**Base Entity Pattern:**
```sql
-- Every table includes these audit columns
Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
CreatedBy UNIQUEIDENTIFIER NULL,
UpdatedAt DATETIME2 NULL,
UpdatedBy UNIQUEIDENTIFIER NULL,
IsDeleted BIT NOT NULL DEFAULT 0,
DeletedAt DATETIME2 NULL,
RowVersion ROWVERSION
```

**Soft Delete Pattern:**
- Use `IsDeleted` flag instead of physical deletes
- Filter `WHERE IsDeleted = 0` in queries
- Retain data for audit/sync purposes

**Optimistic Concurrency:**
- Use `RowVersion` for conflict detection
- Check version before updates
- Handle conflicts in application layer

---

## Auth Service Database

### Database: `ExpressRecipe.Auth`

#### User Table
```sql
CREATE TABLE [User] (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(256) NOT NULL,
    EmailConfirmed BIT NOT NULL DEFAULT 0,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    SecurityStamp NVARCHAR(MAX) NOT NULL,
    PhoneNumber NVARCHAR(50) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    LockoutEnd DATETIME2 NULL,
    LockoutEnabled BIT NOT NULL DEFAULT 1,
    AccessFailedCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION,

    CONSTRAINT UQ_User_Email UNIQUE (Email)
);

CREATE INDEX IX_User_Email ON [User](Email) WHERE IsDeleted = 0;
```

#### ExternalLogin Table
```sql
CREATE TABLE ExternalLogin (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    LoginProvider NVARCHAR(128) NOT NULL,
    ProviderKey NVARCHAR(128) NOT NULL,
    ProviderDisplayName NVARCHAR(256) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_ExternalLogin_User FOREIGN KEY (UserId)
        REFERENCES [User](Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ExternalLogin_Provider UNIQUE (LoginProvider, ProviderKey)
);
```

#### RefreshToken Table
```sql
CREATE TABLE RefreshToken (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Token NVARCHAR(MAX) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedByIp NVARCHAR(50) NULL,
    RevokedAt DATETIME2 NULL,
    RevokedByIp NVARCHAR(50) NULL,
    ReplacedByToken NVARCHAR(MAX) NULL,
    ReasonRevoked NVARCHAR(256) NULL,

    CONSTRAINT FK_RefreshToken_User FOREIGN KEY (UserId)
        REFERENCES [User](Id) ON DELETE CASCADE
);

CREATE INDEX IX_RefreshToken_UserId ON RefreshToken(UserId);
CREATE INDEX IX_RefreshToken_ExpiresAt ON RefreshToken(ExpiresAt);
```

---

## User Profile Service Database

### Database: `ExpressRecipe.Users`

#### UserProfile Table
```sql
CREATE TABLE UserProfile (
    Id UNIQUEIDENTIFIER PRIMARY KEY, -- Same as Auth.User.Id
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    DisplayName NVARCHAR(200) NULL,
    DateOfBirth DATE NULL,
    Gender NVARCHAR(50) NULL,
    Country NVARCHAR(100) NULL,
    Region NVARCHAR(100) NULL,
    PostalCode NVARCHAR(20) NULL,
    PreferredLanguage NVARCHAR(10) NOT NULL DEFAULT 'en-US',
    SubscriptionTier NVARCHAR(50) NOT NULL DEFAULT 'Free', -- Free, Premium, Family
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION
);
```

#### DietaryRestriction Table
```sql
CREATE TABLE DietaryRestriction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    RestrictionType NVARCHAR(50) NOT NULL, -- Medical, Religious, Health, Preference
    Name NVARCHAR(100) NOT NULL, -- e.g., "Celiac Disease", "Kosher", "Vegan"
    Severity NVARCHAR(50) NOT NULL, -- Critical, High, Medium, Low
    Notes NVARCHAR(MAX) NULL,
    VerifiedByProfessional BIT NOT NULL DEFAULT 0,
    VerificationDate DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION,

    CONSTRAINT FK_DietaryRestriction_UserProfile FOREIGN KEY (UserId)
        REFERENCES UserProfile(Id) ON DELETE CASCADE
);

CREATE INDEX IX_DietaryRestriction_UserId ON DietaryRestriction(UserId);
```

#### Allergen Table
```sql
CREATE TABLE Allergen (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    AllergenName NVARCHAR(100) NOT NULL,
    AllergenCategory NVARCHAR(50) NOT NULL, -- Food, Environmental, etc.
    ReactionType NVARCHAR(100) NULL, -- Anaphylaxis, Hives, Digestive, etc.
    Severity NVARCHAR(50) NOT NULL, -- LifeThreatening, Severe, Moderate, Mild
    DiagnosedBy NVARCHAR(100) NULL, -- Doctor, Self, etc.
    DiagnosisDate DATE NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION,

    CONSTRAINT FK_Allergen_UserProfile FOREIGN KEY (UserId)
        REFERENCES UserProfile(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Allergen_UserId ON Allergen(UserId);
CREATE INDEX IX_Allergen_Severity ON Allergen(Severity, UserId);
```

#### UserPreference Table
```sql
CREATE TABLE UserPreference (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    EntityType NVARCHAR(50) NOT NULL, -- Product, Ingredient, Recipe, Brand
    EntityId UNIQUEIDENTIFIER NOT NULL,
    PreferenceType NVARCHAR(50) NOT NULL, -- Like, Dislike, Neutral
    Reason NVARCHAR(MAX) NULL,
    LearnedFrom NVARCHAR(100) NULL, -- Purchase, Scan, Explicit
    Confidence DECIMAL(3,2) NOT NULL DEFAULT 1.0, -- 0.0 to 1.0
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,

    CONSTRAINT FK_UserPreference_UserProfile FOREIGN KEY (UserId)
        REFERENCES UserProfile(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserPreference UNIQUE (UserId, EntityType, EntityId)
);

CREATE INDEX IX_UserPreference_UserId_Type ON UserPreference(UserId, EntityType);
```

#### FamilyMember Table
```sql
CREATE TABLE FamilyMember (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PrimaryUserId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Relationship NVARCHAR(50) NULL, -- Child, Spouse, Parent, etc.
    DateOfBirth DATE NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION,

    CONSTRAINT FK_FamilyMember_UserProfile FOREIGN KEY (PrimaryUserId)
        REFERENCES UserProfile(Id) ON DELETE CASCADE
);

-- Family members can have their own restrictions/allergens
CREATE TABLE FamilyMemberAllergen (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FamilyMemberId UNIQUEIDENTIFIER NOT NULL,
    AllergenName NVARCHAR(100) NOT NULL,
    Severity NVARCHAR(50) NOT NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_FamilyMemberAllergen_FamilyMember FOREIGN KEY (FamilyMemberId)
        REFERENCES FamilyMember(Id) ON DELETE CASCADE
);
```

---

## Product Service Database

### Database: `ExpressRecipe.Products`

#### Product Table
```sql
CREATE TABLE Product (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Brand NVARCHAR(100) NULL,
    ManufacturerId UNIQUEIDENTIFIER NULL,
    Category NVARCHAR(100) NOT NULL,
    SubCategory NVARCHAR(100) NULL,
    Description NVARCHAR(MAX) NULL,
    UPC NVARCHAR(20) NULL,
    EAN NVARCHAR(20) NULL,
    PackageSize DECIMAL(10,2) NULL,
    PackageUnit NVARCHAR(20) NULL, -- oz, g, ml, count, etc.
    ImageUrl NVARCHAR(500) NULL,
    IsVerified BIT NOT NULL DEFAULT 0,
    VerifiedBy UNIQUEIDENTIFIER NULL,
    VerifiedAt DATETIME2 NULL,
    SubmittedBy UNIQUEIDENTIFIER NULL, -- User who submitted
    ApprovalStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION,

    CONSTRAINT UQ_Product_UPC UNIQUE (UPC),
    CONSTRAINT FK_Product_Manufacturer FOREIGN KEY (ManufacturerId)
        REFERENCES Manufacturer(Id)
);

CREATE INDEX IX_Product_Brand ON Product(Brand);
CREATE INDEX IX_Product_Category ON Product(Category, SubCategory);
CREATE INDEX IX_Product_Name ON Product(Name);
CREATE FULLTEXT INDEX ON Product(Name, Description, Brand);
```

#### Ingredient Table
```sql
CREATE TABLE Ingredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Category NVARCHAR(100) NOT NULL, -- Grain, Dairy, Nut, etc.
    Description NVARCHAR(MAX) NULL,
    IsCommonAllergen BIT NOT NULL DEFAULT 0,
    AllergenType NVARCHAR(100) NULL, -- FDA Top 8, etc.
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION,

    CONSTRAINT UQ_Ingredient_Name UNIQUE (Name)
);

CREATE INDEX IX_Ingredient_Category ON Ingredient(Category);
CREATE INDEX IX_Ingredient_CommonAllergen ON Ingredient(IsCommonAllergen)
    WHERE IsCommonAllergen = 1;
```

#### IngredientAlias Table
```sql
CREATE TABLE IngredientAlias (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IngredientId UNIQUEIDENTIFIER NOT NULL,
    AliasName NVARCHAR(100) NOT NULL,
    AliasType NVARCHAR(50) NOT NULL, -- Scientific, Common, Regulatory, etc.
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_IngredientAlias_Ingredient FOREIGN KEY (IngredientId)
        REFERENCES Ingredient(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_IngredientAlias_Name UNIQUE (AliasName)
);

CREATE INDEX IX_IngredientAlias_IngredientId ON IngredientAlias(IngredientId);
```

#### ProductIngredient Table
```sql
CREATE TABLE ProductIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NOT NULL,
    Quantity DECIMAL(10,2) NULL,
    Unit NVARCHAR(20) NULL,
    OrderIndex INT NOT NULL, -- Order on label
    IsMajorIngredient BIT NOT NULL DEFAULT 0,
    MayContain BIT NOT NULL DEFAULT 0, -- "May contain traces"
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,

    CONSTRAINT FK_ProductIngredient_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ProductIngredient_Ingredient FOREIGN KEY (IngredientId)
        REFERENCES Ingredient(Id),
    CONSTRAINT UQ_ProductIngredient UNIQUE (ProductId, IngredientId)
);

CREATE INDEX IX_ProductIngredient_ProductId ON ProductIngredient(ProductId);
CREATE INDEX IX_ProductIngredient_IngredientId ON ProductIngredient(IngredientId);
```

#### NutritionalInfo Table
```sql
CREATE TABLE NutritionalInfo (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    ServingSize DECIMAL(10,2) NOT NULL,
    ServingUnit NVARCHAR(20) NOT NULL,
    Calories DECIMAL(10,2) NULL,
    TotalFat DECIMAL(10,2) NULL,
    SaturatedFat DECIMAL(10,2) NULL,
    TransFat DECIMAL(10,2) NULL,
    Cholesterol DECIMAL(10,2) NULL,
    Sodium DECIMAL(10,2) NULL,
    TotalCarbohydrates DECIMAL(10,2) NULL,
    DietaryFiber DECIMAL(10,2) NULL,
    Sugars DECIMAL(10,2) NULL,
    Protein DECIMAL(10,2) NULL,
    VitaminD DECIMAL(10,2) NULL,
    Calcium DECIMAL(10,2) NULL,
    Iron DECIMAL(10,2) NULL,
    Potassium DECIMAL(10,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,

    CONSTRAINT FK_NutritionalInfo_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_NutritionalInfo_Product UNIQUE (ProductId)
);
```

#### Manufacturer Table
```sql
CREATE TABLE Manufacturer (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Website NVARCHAR(500) NULL,
    ContactEmail NVARCHAR(256) NULL,
    ContactPhone NVARCHAR(50) NULL,
    Address NVARCHAR(MAX) NULL,
    IsVerified BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION,

    CONSTRAINT UQ_Manufacturer_Name UNIQUE (Name)
);
```

---

## Recipe Service Database

### Database: `ExpressRecipe.Recipes`

#### Recipe Table
```sql
CREATE TABLE Recipe (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    AuthorId UNIQUEIDENTIFIER NOT NULL, -- User who created
    PrepTime INT NULL, -- minutes
    CookTime INT NULL, -- minutes
    TotalTime INT NULL, -- minutes
    Servings INT NOT NULL DEFAULT 1,
    Difficulty NVARCHAR(50) NULL, -- Easy, Medium, Hard
    Category NVARCHAR(100) NULL,
    Cuisine NVARCHAR(100) NULL,
    ImageUrl NVARCHAR(500) NULL,
    IsPublic BIT NOT NULL DEFAULT 1,
    IsVerified BIT NOT NULL DEFAULT 0,
    ViewCount INT NOT NULL DEFAULT 0,
    SaveCount INT NOT NULL DEFAULT 0,
    AverageRating DECIMAL(3,2) NULL,
    RatingCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION
);

CREATE INDEX IX_Recipe_AuthorId ON Recipe(AuthorId);
CREATE INDEX IX_Recipe_Category ON Recipe(Category);
CREATE INDEX IX_Recipe_Rating ON Recipe(AverageRating DESC);
CREATE FULLTEXT INDEX ON Recipe(Title, Description);
```

#### RecipeIngredient Table
```sql
CREATE TABLE RecipeIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NULL, -- Link to Product.Ingredient
    IngredientName NVARCHAR(100) NOT NULL, -- Text if not linked
    Quantity DECIMAL(10,2) NULL,
    Unit NVARCHAR(50) NULL,
    Preparation NVARCHAR(200) NULL, -- "diced", "chopped", etc.
    OrderIndex INT NOT NULL,
    IsOptional BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_RecipeIngredient_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RecipeIngredient_RecipeId ON RecipeIngredient(RecipeId);
```

#### RecipeStep Table
```sql
CREATE TABLE RecipeStep (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    StepNumber INT NOT NULL,
    Instruction NVARCHAR(MAX) NOT NULL,
    Duration INT NULL, -- minutes for this step
    ImageUrl NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_RecipeStep_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RecipeStep UNIQUE (RecipeId, StepNumber)
);
```

#### RecipeRating Table
```sql
CREATE TABLE RecipeRating (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Rating INT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
    Review NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,

    CONSTRAINT FK_RecipeRating_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RecipeRating_User UNIQUE (RecipeId, UserId)
);

CREATE INDEX IX_RecipeRating_RecipeId ON RecipeRating(RecipeId);
```

#### SavedRecipe Table
```sql
CREATE TABLE SavedRecipe (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    CollectionName NVARCHAR(100) NULL, -- User-defined collection
    Notes NVARCHAR(MAX) NULL,
    SavedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_SavedRecipe_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_SavedRecipe UNIQUE (RecipeId, UserId)
);

CREATE INDEX IX_SavedRecipe_UserId ON SavedRecipe(UserId);
```

#### IngredientSubstitution Table
```sql
CREATE TABLE IngredientSubstitution (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    OriginalIngredientId UNIQUEIDENTIFIER NOT NULL,
    SubstituteIngredientId UNIQUEIDENTIFIER NOT NULL,
    Ratio DECIMAL(5,2) NOT NULL DEFAULT 1.0, -- Conversion ratio
    Notes NVARCHAR(MAX) NULL,
    DietaryContext NVARCHAR(100) NULL, -- "Vegan", "Gluten-Free", etc.
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT UQ_IngredientSubstitution UNIQUE (OriginalIngredientId, SubstituteIngredientId)
);
```

---

## Additional Service Schemas (Summary)

### Inventory Service (`ExpressRecipe.Inventory`)

**Key Tables:**
- `InventoryItem` - User inventory tracking
- `StorageLocation` - Pantry, fridge, freezer
- `InventoryHistory` - Usage tracking
- `ExpirationAlert` - Expiration notifications
- `UsagePrediction` - ML-based predictions

### Shopping Service (`ExpressRecipe.Shopping`)

**Key Tables:**
- `ShoppingList` - User shopping lists
- `ShoppingListItem` - Items in list
- `ListShare` - Shared access
- `ShoppingHistory` - Completed shopping trips
- `StoreLayout` - Store aisle organization

### Meal Planning Service (`ExpressRecipe.MealPlanning`)

**Key Tables:**
- `MealPlan` - User meal plans
- `PlannedMeal` - Scheduled meals
- `NutritionalGoal` - User nutrition targets
- `PlanTemplate` - Reusable plan templates

### Price Service (`ExpressRecipe.Pricing`)

**Key Tables:**
- `PriceObservation` - Crowdsourced prices
- `Store` - Store locations
- `Deal` - Current deals/coupons
- `PriceHistory` - Historical pricing
- `PricePrediction` - Best buy predictions

### Scanner Service (`ExpressRecipe.Scans`)

**Key Tables:**
- `ScanHistory` - User scan records
- `UnknownProduct` - Products to add
- `OCRResult` - Label scan results
- `ScanAlert` - Allergen alerts triggered

### Recall Service (`ExpressRecipe.Recalls`)

**Key Tables:**
- `Recall` - FDA/USDA recalls
- `RecallProduct` - Affected products
- `RecallAlert` - User notifications
- `RecallSubscription` - User watch list

### Notification Service (`ExpressRecipe.Notifications`)

**Key Tables:**
- `Notification` - All notifications
- `NotificationPreference` - User settings
- `NotificationTemplate` - Message templates
- `DeliveryLog` - Delivery tracking

---

## Sync Metadata Tables (All Databases)

Each service database includes sync tables:

```sql
CREATE TABLE SyncMetadata (
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    LastModified DATETIME2 NOT NULL,
    ChangeVector BIGINT NOT NULL,
    DeviceId UNIQUEIDENTIFIER NULL,

    PRIMARY KEY (EntityType, EntityId, UserId)
);

CREATE TABLE SyncConflict (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    ServerValue NVARCHAR(MAX) NOT NULL,
    ClientValue NVARCHAR(MAX) NOT NULL,
    ConflictDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ResolvedDate DATETIME2 NULL,
    Resolution NVARCHAR(50) NULL, -- ServerWins, ClientWins, Merged
    ResolvedBy UNIQUEIDENTIFIER NULL
);
```

---

## Local SQLite Schema

Client applications use SQLite with simplified schema:

**Key differences:**
- No foreign key cascades (managed in code)
- Simpler data types
- Additional sync columns
- Offline queue tables

**Example:**
```sql
CREATE TABLE Product_Local (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Brand TEXT,
    UPC TEXT,
    IsSynced INTEGER NOT NULL DEFAULT 0,
    NeedsPush INTEGER NOT NULL DEFAULT 0,
    LastSyncDate INTEGER, -- Unix timestamp
    LocalOnlyData TEXT -- JSON for client-specific data
);
```

---

## Next Steps
See API contracts in `04-API-CONTRACTS.md`
