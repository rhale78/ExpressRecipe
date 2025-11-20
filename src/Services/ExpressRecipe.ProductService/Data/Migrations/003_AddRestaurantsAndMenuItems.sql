-- Restaurant table
CREATE TABLE Restaurant (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Brand NVARCHAR(200) NULL, -- Chain name if applicable
    Description NVARCHAR(MAX) NULL,
    CuisineType NVARCHAR(100) NULL,
    Address NVARCHAR(500) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(50) NULL,
    ZipCode NVARCHAR(20) NULL,
    Country NVARCHAR(100) NULL,
    Latitude DECIMAL(9,6) NULL,
    Longitude DECIMAL(9,6) NULL,
    PhoneNumber NVARCHAR(20) NULL,
    Website NVARCHAR(500) NULL,
    ImageUrl NVARCHAR(500) NULL,
    PriceRange NVARCHAR(10) NULL, -- $, $$, $$$, $$$$
    IsChain BIT NOT NULL DEFAULT 0,
    ApprovalStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedBy UNIQUEIDENTIFIER NULL,
    ApprovedAt DATETIME2 NULL,
    SubmittedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION
);
GO

CREATE INDEX IX_Restaurant_Name ON Restaurant(Name) WHERE IsDeleted = 0;
CREATE INDEX IX_Restaurant_Brand ON Restaurant(Brand) WHERE IsDeleted = 0;
CREATE INDEX IX_Restaurant_CuisineType ON Restaurant(CuisineType) WHERE IsDeleted = 0;
CREATE INDEX IX_Restaurant_City ON Restaurant(City) WHERE IsDeleted = 0;
CREATE INDEX IX_Restaurant_ApprovalStatus ON Restaurant(ApprovalStatus) WHERE IsDeleted = 0;
CREATE INDEX IX_Restaurant_Location ON Restaurant(Latitude, Longitude) WHERE IsDeleted = 0 AND Latitude IS NOT NULL AND Longitude IS NOT NULL;
GO

-- MenuItem table
CREATE TABLE MenuItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RestaurantId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(300) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NULL, -- Appetizer, Entree, Dessert, Beverage, etc.
    Price DECIMAL(10,2) NULL,
    Currency NVARCHAR(10) NOT NULL DEFAULT 'USD',
    ServingSize NVARCHAR(100) NULL,
    ImageUrl NVARCHAR(500) NULL,
    IsAvailable BIT NOT NULL DEFAULT 1,
    IsSeasonalItem BIT NOT NULL DEFAULT 0,
    ApprovalStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedBy UNIQUEIDENTIFIER NULL,
    ApprovedAt DATETIME2 NULL,
    SubmittedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_MenuItem_Restaurant FOREIGN KEY (RestaurantId)
        REFERENCES Restaurant(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_MenuItem_RestaurantId ON MenuItem(RestaurantId) WHERE IsDeleted = 0;
CREATE INDEX IX_MenuItem_Name ON MenuItem(Name) WHERE IsDeleted = 0;
CREATE INDEX IX_MenuItem_Category ON MenuItem(Category) WHERE IsDeleted = 0;
CREATE INDEX IX_MenuItem_ApprovalStatus ON MenuItem(ApprovalStatus) WHERE IsDeleted = 0;
GO

-- MenuItem Ingredient Association
CREATE TABLE MenuItemIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MenuItemId UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NOT NULL,
    OrderIndex INT NOT NULL DEFAULT 0,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_MenuItemIngredient_MenuItem FOREIGN KEY (MenuItemId)
        REFERENCES MenuItem(Id) ON DELETE CASCADE,
    CONSTRAINT FK_MenuItemIngredient_Ingredient FOREIGN KEY (IngredientId)
        REFERENCES Ingredient(Id),
    CONSTRAINT UQ_MenuItemIngredient_MenuItem_Ingredient UNIQUE (MenuItemId, IngredientId)
);
GO

CREATE INDEX IX_MenuItemIngredient_MenuItemId ON MenuItemIngredient(MenuItemId) WHERE IsDeleted = 0;
CREATE INDEX IX_MenuItemIngredient_IngredientId ON MenuItemIngredient(IngredientId) WHERE IsDeleted = 0;
GO

-- MenuItem Nutritional Information
CREATE TABLE MenuItemNutrition (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MenuItemId UNIQUEIDENTIFIER NOT NULL,
    Calories DECIMAL(10,2) NULL,
    TotalFat DECIMAL(10,2) NULL,
    SaturatedFat DECIMAL(10,2) NULL,
    TransFat DECIMAL(10,2) NULL,
    Cholesterol DECIMAL(10,2) NULL,
    Sodium DECIMAL(10,2) NULL,
    TotalCarbohydrate DECIMAL(10,2) NULL,
    DietaryFiber DECIMAL(10,2) NULL,
    Sugars DECIMAL(10,2) NULL,
    Protein DECIMAL(10,2) NULL,
    AdditionalNutrients NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_MenuItemNutrition_MenuItem FOREIGN KEY (MenuItemId)
        REFERENCES MenuItem(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_MenuItemNutrition_MenuItemId ON MenuItemNutrition(MenuItemId) WHERE IsDeleted = 0;
GO

-- User Restaurant Ratings (many-to-many)
CREATE TABLE UserRestaurantRating (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    RestaurantId UNIQUEIDENTIFIER NOT NULL,
    Rating INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Review NVARCHAR(MAX) NULL,
    VisitDate DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_UserRestaurantRating_Restaurant FOREIGN KEY (RestaurantId)
        REFERENCES Restaurant(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserRestaurantRating_User_Restaurant UNIQUE (UserId, RestaurantId)
);
GO

CREATE INDEX IX_UserRestaurantRating_UserId ON UserRestaurantRating(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserRestaurantRating_RestaurantId ON UserRestaurantRating(RestaurantId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserRestaurantRating_Rating ON UserRestaurantRating(Rating) WHERE IsDeleted = 0;
GO

-- User MenuItem Ratings (many-to-many)
CREATE TABLE UserMenuItemRating (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    MenuItemId UNIQUEIDENTIFIER NOT NULL,
    Rating INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Review NVARCHAR(MAX) NULL,
    WouldOrderAgain BIT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_UserMenuItemRating_MenuItem FOREIGN KEY (MenuItemId)
        REFERENCES MenuItem(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserMenuItemRating_User_MenuItem UNIQUE (UserId, MenuItemId)
);
GO

CREATE INDEX IX_UserMenuItemRating_UserId ON UserMenuItemRating(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserMenuItemRating_MenuItemId ON UserMenuItemRating(MenuItemId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserMenuItemRating_Rating ON UserMenuItemRating(Rating) WHERE IsDeleted = 0;
GO
