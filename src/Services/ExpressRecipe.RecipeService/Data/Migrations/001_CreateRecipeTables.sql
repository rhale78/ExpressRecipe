-- Migration: 001_CreateRecipeTables
-- Description: Create recipe management tables
-- Date: 2024-11-19

-- Recipe: User-created or community recipes
CREATE TABLE Recipe (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(300) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NULL, -- Breakfast, Lunch, Dinner, Dessert, Snack, etc.
    Cuisine NVARCHAR(100) NULL, -- Italian, Mexican, Asian, etc.
    DifficultyLevel NVARCHAR(50) NULL, -- Easy, Medium, Hard
    PrepTimeMinutes INT NULL,
    CookTimeMinutes INT NULL,
    TotalTimeMinutes INT NULL,
    Servings INT NULL,
    ImageUrl NVARCHAR(500) NULL,
    VideoUrl NVARCHAR(500) NULL,
    Instructions NVARCHAR(MAX) NULL, -- Step-by-step cooking instructions
    Notes NVARCHAR(MAX) NULL,
    IsPublic BIT NOT NULL DEFAULT 0, -- Public recipes visible to community
    IsApproved BIT NOT NULL DEFAULT 0, -- Approval for community recipes
    ApprovedBy UNIQUEIDENTIFIER NULL,
    ApprovedAt DATETIME2 NULL,
    RejectionReason NVARCHAR(MAX) NULL,
    SourceUrl NVARCHAR(500) NULL, -- Original recipe source if imported
    AuthorId UNIQUEIDENTIFIER NOT NULL, -- User who created/submitted recipe
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION
);

CREATE INDEX IX_Recipe_Category ON Recipe(Category);
CREATE INDEX IX_Recipe_Cuisine ON Recipe(Cuisine);
CREATE INDEX IX_Recipe_AuthorId ON Recipe(AuthorId);
CREATE INDEX IX_Recipe_IsPublic ON Recipe(IsPublic);
CREATE INDEX IX_Recipe_IsApproved ON Recipe(IsApproved);
GO

-- RecipeIngredient: Ingredients required for a recipe
CREATE TABLE RecipeIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NULL, -- References ProductService.Ingredient.Id (can be null if using raw ingredient string)
    BaseIngredientId UNIQUEIDENTIFIER NULL, -- References ProductService.BaseIngredient.Id
    IngredientName NVARCHAR(200) NULL, -- Free-form ingredient name if not in database
    Quantity DECIMAL(10, 2) NULL,
    Unit NVARCHAR(50) NULL, -- cup, tbsp, tsp, oz, g, kg, lb, etc.
    OrderIndex INT NOT NULL DEFAULT 0,
    PreparationNote NVARCHAR(500) NULL, -- "diced", "minced", "chopped", "melted", etc.
    IsOptional BIT NOT NULL DEFAULT 0,
    SubstituteNotes NVARCHAR(500) NULL, -- Alternative ingredients
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CONSTRAINT FK_RecipeIngredient_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RecipeIngredient_RecipeId ON RecipeIngredient(RecipeId);
CREATE INDEX IX_RecipeIngredient_IngredientId ON RecipeIngredient(IngredientId);
CREATE INDEX IX_RecipeIngredient_BaseIngredientId ON RecipeIngredient(BaseIngredientId);
GO

-- RecipeNutrition: Nutritional information per serving
CREATE TABLE RecipeNutrition (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    ServingSize NVARCHAR(100) NULL,
    Calories DECIMAL(10, 2) NULL,
    TotalFat DECIMAL(10, 2) NULL,
    SaturatedFat DECIMAL(10, 2) NULL,
    TransFat DECIMAL(10, 2) NULL,
    Cholesterol DECIMAL(10, 2) NULL,
    Sodium DECIMAL(10, 2) NULL,
    TotalCarbohydrates DECIMAL(10, 2) NULL,
    DietaryFiber DECIMAL(10, 2) NULL,
    Sugars DECIMAL(10, 2) NULL,
    Protein DECIMAL(10, 2) NULL,
    VitaminD DECIMAL(10, 2) NULL,
    Calcium DECIMAL(10, 2) NULL,
    Iron DECIMAL(10, 2) NULL,
    Potassium DECIMAL(10, 2) NULL,
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_RecipeNutrition_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RecipeNutrition_RecipeId UNIQUE (RecipeId)
);
GO

-- RecipeTag: Tags for recipe categorization and search
CREATE TABLE RecipeTag (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    CONSTRAINT UQ_RecipeTag_Name UNIQUE (Name)
);

CREATE INDEX IX_RecipeTag_Name ON RecipeTag(Name);
GO

-- RecipeTagMapping: Many-to-many relationship between recipes and tags
CREATE TABLE RecipeTagMapping (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    TagId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeTagMapping_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RecipeTagMapping_Tag FOREIGN KEY (TagId)
        REFERENCES RecipeTag(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RecipeTagMapping_Recipe_Tag UNIQUE (RecipeId, TagId)
);

CREATE INDEX IX_RecipeTagMapping_RecipeId ON RecipeTagMapping(RecipeId);
CREATE INDEX IX_RecipeTagMapping_TagId ON RecipeTagMapping(TagId);
GO

-- UserRecipeRating: User ratings and reviews for recipes
CREATE TABLE UserRecipeRating (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL, -- References UserService.User.Id
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    Rating INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Review NVARCHAR(MAX) NULL,
    WouldMakeAgain BIT NULL,
    MadeItCount INT NOT NULL DEFAULT 0, -- How many times user has made this recipe
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_UserRecipeRating_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserRecipeRating_User_Recipe UNIQUE (UserId, RecipeId)
);

CREATE INDEX IX_UserRecipeRating_UserId ON UserRecipeRating(UserId);
CREATE INDEX IX_UserRecipeRating_RecipeId ON UserRecipeRating(RecipeId);
GO

-- UserFavoriteRecipe: User's favorite/bookmarked recipes
CREATE TABLE UserFavoriteRecipe (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL, -- References UserService.User.Id
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserFavoriteRecipe_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserFavoriteRecipe_User_Recipe UNIQUE (UserId, RecipeId)
);

CREATE INDEX IX_UserFavoriteRecipe_UserId ON UserFavoriteRecipe(UserId);
CREATE INDEX IX_UserFavoriteRecipe_RecipeId ON UserFavoriteRecipe(RecipeId);
GO

-- RecipeAllergenWarning: Automatically detected allergens in recipe (denormalized for performance)
CREATE TABLE RecipeAllergenWarning (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    AllergenId UNIQUEIDENTIFIER NOT NULL, -- References UserService.Allergen.Id
    AllergenName NVARCHAR(200) NOT NULL,
    SourceIngredientId UNIQUEIDENTIFIER NULL, -- Which ingredient contains this allergen
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeAllergenWarning_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RecipeAllergenWarning_Recipe_Allergen UNIQUE (RecipeId, AllergenId)
);

CREATE INDEX IX_RecipeAllergenWarning_RecipeId ON RecipeAllergenWarning(RecipeId);
CREATE INDEX IX_RecipeAllergenWarning_AllergenId ON RecipeAllergenWarning(AllergenId);
GO
