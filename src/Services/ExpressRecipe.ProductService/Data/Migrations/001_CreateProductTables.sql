-- Ingredient Table
CREATE TABLE Ingredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    AlternativeNames NVARCHAR(MAX) NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NULL,
    IsCommonAllergen BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_Ingredient_Name UNIQUE (Name)
);
GO

CREATE INDEX IX_Ingredient_Category ON Ingredient(Category) WHERE IsDeleted = 0;
CREATE INDEX IX_Ingredient_IsCommonAllergen ON Ingredient(IsCommonAllergen) WHERE IsDeleted = 0;
GO

-- Ingredient Allergen Association
CREATE TABLE IngredientAllergen (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IngredientId UNIQUEIDENTIFIER NOT NULL,
    AllergenId UNIQUEIDENTIFIER NOT NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_IngredientAllergen_Ingredient_Allergen UNIQUE (IngredientId, AllergenId)
);
GO

CREATE INDEX IX_IngredientAllergen_IngredientId ON IngredientAllergen(IngredientId) WHERE IsDeleted = 0;
CREATE INDEX IX_IngredientAllergen_AllergenId ON IngredientAllergen(AllergenId) WHERE IsDeleted = 0;
GO

-- Product Table
CREATE TABLE Product (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(300) NOT NULL,
    Brand NVARCHAR(200) NULL,
    Barcode NVARCHAR(50) NULL,
    BarcodeType NVARCHAR(20) NULL, -- UPC, EAN, QR, etc.
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NULL,
    ServingSize NVARCHAR(100) NULL,
    ServingUnit NVARCHAR(50) NULL,
    ImageUrl NVARCHAR(500) NULL,
    ApprovalStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected
    ApprovedBy UNIQUEIDENTIFIER NULL,
    ApprovedAt DATETIME2 NULL,
    RejectionReason NVARCHAR(MAX) NULL,
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

CREATE INDEX IX_Product_Barcode ON Product(Barcode) WHERE IsDeleted = 0 AND Barcode IS NOT NULL;
CREATE INDEX IX_Product_Brand ON Product(Brand) WHERE IsDeleted = 0;
CREATE INDEX IX_Product_Category ON Product(Category) WHERE IsDeleted = 0;
CREATE INDEX IX_Product_ApprovalStatus ON Product(ApprovalStatus) WHERE IsDeleted = 0;
CREATE INDEX IX_Product_Name ON Product(Name) WHERE IsDeleted = 0;
GO

-- Product Ingredient Association
CREATE TABLE ProductIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NOT NULL,
    OrderIndex INT NOT NULL DEFAULT 0,
    Quantity NVARCHAR(100) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductIngredient_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ProductIngredient_Ingredient FOREIGN KEY (IngredientId)
        REFERENCES Ingredient(Id),
    CONSTRAINT UQ_ProductIngredient_Product_Ingredient UNIQUE (ProductId, IngredientId)
);
GO

CREATE INDEX IX_ProductIngredient_ProductId ON ProductIngredient(ProductId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductIngredient_IngredientId ON ProductIngredient(IngredientId) WHERE IsDeleted = 0;
GO

-- Nutritional Information Table
CREATE TABLE ProductNutrition (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
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
    VitaminA DECIMAL(10,2) NULL,
    VitaminC DECIMAL(10,2) NULL,
    Calcium DECIMAL(10,2) NULL,
    Iron DECIMAL(10,2) NULL,
    AdditionalNutrients NVARCHAR(MAX) NULL, -- JSON for other nutrients
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductNutrition_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_ProductNutrition_ProductId ON ProductNutrition(ProductId) WHERE IsDeleted = 0;
GO

-- Product Price Tracking
CREATE TABLE ProductPrice (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    Currency NVARCHAR(10) NOT NULL DEFAULT 'USD',
    Store NVARCHAR(200) NULL,
    Location NVARCHAR(500) NULL,
    Latitude DECIMAL(9,6) NULL,
    Longitude DECIMAL(9,6) NULL,
    ReportedBy UNIQUEIDENTIFIER NOT NULL,
    ReportedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductPrice_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_ProductPrice_ProductId ON ProductPrice(ProductId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductPrice_Store ON ProductPrice(Store) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductPrice_ReportedAt ON ProductPrice(ReportedAt) WHERE IsDeleted = 0;
GO

-- User Product Ratings
CREATE TABLE ProductRating (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Rating INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Review NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductRating_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ProductRating_Product_User UNIQUE (ProductId, UserId)
);
GO

CREATE INDEX IX_ProductRating_ProductId ON ProductRating(ProductId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductRating_UserId ON ProductRating(UserId) WHERE IsDeleted = 0;
GO

-- Product Recall Information
CREATE TABLE ProductRecall (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NULL,
    Barcode NVARCHAR(50) NULL,
    ProductName NVARCHAR(300) NOT NULL,
    Brand NVARCHAR(200) NULL,
    RecallReason NVARCHAR(MAX) NOT NULL,
    RecallDate DATETIME2 NOT NULL,
    RecallSource NVARCHAR(200) NULL,
    SourceUrl NVARCHAR(500) NULL,
    Severity NVARCHAR(50) NOT NULL, -- Low, Medium, High, Critical
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_ProductRecall_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id)
);
GO

CREATE INDEX IX_ProductRecall_ProductId ON ProductRecall(ProductId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductRecall_Barcode ON ProductRecall(Barcode) WHERE IsDeleted = 0 AND Barcode IS NOT NULL;
CREATE INDEX IX_ProductRecall_IsActive ON ProductRecall(IsActive) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductRecall_RecallDate ON ProductRecall(RecallDate) WHERE IsDeleted = 0;
GO
