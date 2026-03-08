-- MenuItem table
CREATE TABLE MenuItem (
    Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RestaurantId   UNIQUEIDENTIFIER NOT NULL,
    Name           NVARCHAR(300) NOT NULL,
    Description    NVARCHAR(MAX) NULL,
    Category       NVARCHAR(100) NULL,
    Price          DECIMAL(10,2) NULL,
    Currency       NVARCHAR(10) NOT NULL DEFAULT 'USD',
    ServingSize    NVARCHAR(100) NULL,
    ServingUnit    NVARCHAR(50) NULL,
    ImageUrl       NVARCHAR(500) NULL,
    IsAvailable    BIT NOT NULL DEFAULT 1,
    IsSeasonalItem BIT NOT NULL DEFAULT 0,
    ApprovalStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedBy     UNIQUEIDENTIFIER NULL,
    ApprovedAt     DATETIME2 NULL,
    SubmittedBy    UNIQUEIDENTIFIER NULL,
    AverageRating  DECIMAL(3,2) NULL,
    RatingCount    INT NOT NULL DEFAULT 0,
    CreatedAt      DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy      UNIQUEIDENTIFIER NULL,
    UpdatedAt      DATETIME2 NULL,
    UpdatedBy      UNIQUEIDENTIFIER NULL,
    IsDeleted      BIT NOT NULL DEFAULT 0,
    DeletedAt      DATETIME2 NULL,
    RowVersion     ROWVERSION,
    CONSTRAINT CK_MenuItem_ApprovalStatus CHECK (ApprovalStatus IN ('Pending','Approved','Rejected'))
);
GO

CREATE INDEX IX_MenuItem_Name           ON MenuItem(Name) WHERE IsDeleted = 0;
CREATE INDEX IX_MenuItem_Category       ON MenuItem(Category) WHERE IsDeleted = 0;
CREATE INDEX IX_MenuItem_RestaurantId   ON MenuItem(RestaurantId) WHERE IsDeleted = 0;
CREATE INDEX IX_MenuItem_ApprovalStatus ON MenuItem(ApprovalStatus) WHERE IsDeleted = 0;
GO

-- MenuItem Ingredient Association
CREATE TABLE MenuItemIngredient (
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MenuItemId          UNIQUEIDENTIFIER NOT NULL,
    IngredientId        UNIQUEIDENTIFIER NOT NULL,
    OrderIndex          INT NOT NULL DEFAULT 0,
    Notes               NVARCHAR(MAX) NULL,
    IngredientListString NVARCHAR(MAX) NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           UNIQUEIDENTIFIER NULL,
    UpdatedAt           DATETIME2 NULL,
    UpdatedBy           UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT NOT NULL DEFAULT 0,
    DeletedAt           DATETIME2 NULL,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_MenuItemIngredient_MenuItem FOREIGN KEY (MenuItemId)
        REFERENCES MenuItem(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_MenuItemIngredient_MenuItem_Ingredient UNIQUE (MenuItemId, IngredientId)
);
GO

CREATE INDEX IX_MenuItemIngredient_MenuItemId   ON MenuItemIngredient(MenuItemId) WHERE IsDeleted = 0;
CREATE INDEX IX_MenuItemIngredient_IngredientId ON MenuItemIngredient(IngredientId) WHERE IsDeleted = 0;
GO

-- MenuItem Nutritional Information
CREATE TABLE MenuItemNutrition (
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MenuItemId          UNIQUEIDENTIFIER NOT NULL UNIQUE,
    Calories            DECIMAL(10,2) NULL,
    TotalFat            DECIMAL(10,2) NULL,
    SaturatedFat        DECIMAL(10,2) NULL,
    TransFat            DECIMAL(10,2) NULL,
    Cholesterol         DECIMAL(10,2) NULL,
    Sodium              DECIMAL(10,2) NULL,
    TotalCarbohydrates  DECIMAL(10,2) NULL,
    DietaryFiber        DECIMAL(10,2) NULL,
    Sugars              DECIMAL(10,2) NULL,
    Protein             DECIMAL(10,2) NULL,
    VitaminD            DECIMAL(10,2) NULL,
    Calcium             DECIMAL(10,2) NULL,
    Iron                DECIMAL(10,2) NULL,
    Potassium           DECIMAL(10,2) NULL,
    AdditionalNutrients NVARCHAR(MAX) NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           UNIQUEIDENTIFIER NULL,
    UpdatedAt           DATETIME2 NULL,
    UpdatedBy           UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT NOT NULL DEFAULT 0,
    DeletedAt           DATETIME2 NULL,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_MenuItemNutrition_MenuItem FOREIGN KEY (MenuItemId)
        REFERENCES MenuItem(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_MenuItemNutrition_MenuItemId ON MenuItemNutrition(MenuItemId) WHERE IsDeleted = 0;
GO

-- User MenuItem Ratings
CREATE TABLE UserMenuItemRating (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    MenuItemId      UNIQUEIDENTIFIER NOT NULL,
    Rating          INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Review          NVARCHAR(MAX) NULL,
    WouldOrderAgain BIT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       UNIQUEIDENTIFIER NULL,
    UpdatedAt       DATETIME2 NULL,
    UpdatedBy       UNIQUEIDENTIFIER NULL,
    IsDeleted       BIT NOT NULL DEFAULT 0,
    DeletedAt       DATETIME2 NULL,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_UserMenuItemRating_MenuItem FOREIGN KEY (MenuItemId)
        REFERENCES MenuItem(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserMenuItemRating_User_MenuItem UNIQUE (UserId, MenuItemId)
);
GO

CREATE INDEX IX_UserMenuItemRating_UserId     ON UserMenuItemRating(UserId)     WHERE IsDeleted = 0;
CREATE INDEX IX_UserMenuItemRating_MenuItemId ON UserMenuItemRating(MenuItemId) WHERE IsDeleted = 0;
GO
