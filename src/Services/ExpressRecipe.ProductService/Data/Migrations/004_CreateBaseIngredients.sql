-- Migration: 004_CreateBaseIngredients
-- Description: Create base ingredients table for atomic ingredient components
-- Date: 2024-11-19

-- BaseIngredient: Fundamental ingredient building blocks that cannot be decomposed further
CREATE TABLE BaseIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    ScientificName NVARCHAR(200) NULL,
    Category NVARCHAR(100) NULL, -- Grain, Vegetable, Fruit, Protein, Dairy, Fat/Oil, Sweetener, Spice, Additive, etc.
    Description NVARCHAR(MAX) NULL,
    Purpose NVARCHAR(500) NULL, -- Common uses (e.g., "Leavening agent", "Preservative", "Flavor enhancer")
    CommonNames NVARCHAR(500) NULL, -- Alternative names (comma-separated for searching)
    IsAllergen BIT NOT NULL DEFAULT 0,
    AllergenType NVARCHAR(100) NULL, -- If IsAllergen=1: Gluten, Dairy, Nuts, Soy, etc.
    IsAdditive BIT NOT NULL DEFAULT 0,
    AdditiveCode NVARCHAR(50) NULL, -- E.g., "E300" for Ascorbic Acid
    NutritionalHighlights NVARCHAR(MAX) NULL, -- JSON or text describing key nutrients
    IsApproved BIT NOT NULL DEFAULT 0,
    ApprovedBy UNIQUEIDENTIFIER NULL,
    ApprovedAt DATETIME2 NULL,
    RejectionReason NVARCHAR(MAX) NULL,
    SubmittedBy UNIQUEIDENTIFIER NULL,
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_BaseIngredient_Name UNIQUE (Name)
);

CREATE INDEX IX_BaseIngredient_Category ON BaseIngredient(Category);
CREATE INDEX IX_BaseIngredient_IsAllergen ON BaseIngredient(IsAllergen);
CREATE INDEX IX_BaseIngredient_IsAdditive ON BaseIngredient(IsAdditive);
CREATE INDEX IX_BaseIngredient_Name ON BaseIngredient(Name);
GO

-- IngredientBaseComponent: Links complex Ingredients to their base components
-- Example: "Enriched Wheat Flour" Ingredient -> BaseIngredients (Wheat Flour, Niacin, Iron, Thiamine)
CREATE TABLE IngredientBaseComponent (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IngredientId UNIQUEIDENTIFIER NOT NULL, -- References Ingredient.Id
    BaseIngredientId UNIQUEIDENTIFIER NOT NULL, -- References BaseIngredient.Id
    OrderIndex INT NOT NULL DEFAULT 0, -- Order in which component appears in ingredient list
    Percentage DECIMAL(5, 2) NULL, -- Percentage of base ingredient if known (0-100)
    IsMainComponent BIT NOT NULL DEFAULT 0, -- True if this is the primary component
    Notes NVARCHAR(MAX) NULL,
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CONSTRAINT FK_IngredientBaseComponent_Ingredient FOREIGN KEY (IngredientId)
        REFERENCES Ingredient(Id) ON DELETE CASCADE,
    CONSTRAINT FK_IngredientBaseComponent_BaseIngredient FOREIGN KEY (BaseIngredientId)
        REFERENCES BaseIngredient(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_IngredientBaseComponent_Ingredient_BaseIngredient
        UNIQUE (IngredientId, BaseIngredientId)
);

CREATE INDEX IX_IngredientBaseComponent_IngredientId ON IngredientBaseComponent(IngredientId);
CREATE INDEX IX_IngredientBaseComponent_BaseIngredientId ON IngredientBaseComponent(BaseIngredientId);
GO

-- Add IngredientListString column to Ingredient table to store raw ingredient text
-- This allows parsing of complex ingredient strings like "Enriched Wheat Flour (Wheat Flour, Niacin, Iron)"
ALTER TABLE Ingredient ADD IngredientListString NVARCHAR(MAX) NULL;
GO

-- Add IngredientListString to ProductIngredient for product label strings
ALTER TABLE ProductIngredient ADD IngredientListString NVARCHAR(MAX) NULL;
GO

-- Add IngredientListString to MenuItemIngredient for menu item ingredient strings
ALTER TABLE MenuItemIngredient ADD IngredientListString NVARCHAR(MAX) NULL;
GO
