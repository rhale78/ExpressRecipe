-- Migration: 019_AddFoodSubstituteCatalog
-- Description: Add FoodGroup, FoodGroupMember, and IngredientSubstitutionHistory tables
--              to support structured ingredient substitution catalog.
-- Date: 2026-03-09

-- -------------------------------------------------------------------------
-- 1. FoodGroup – logical groupings of interchangeable ingredients/products
-- -------------------------------------------------------------------------
CREATE TABLE FoodGroup (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name            NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(MAX) NULL,
    FunctionalRole  NVARCHAR(200) NULL,  -- 'Leavening','Fat/Oil','Binding','Liquid','Sweetener','Thickener','Other'
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NULL,
    CONSTRAINT UQ_FoodGroup_Name UNIQUE (Name)
);
GO

CREATE INDEX IX_FoodGroup_FunctionalRole ON FoodGroup(FunctionalRole);
GO

-- -------------------------------------------------------------------------
-- 2. FoodGroupMember – a specific ingredient/product belonging to a group
-- -------------------------------------------------------------------------
CREATE TABLE FoodGroupMember (
    Id                        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FoodGroupId               UNIQUEIDENTIFIER NOT NULL,
    IngredientId              UNIQUEIDENTIFIER NULL,
    ProductId                 UNIQUEIDENTIFIER NULL,
    CustomName                NVARCHAR(200) NULL,
    SubstitutionRatio         NVARCHAR(200) NULL,    -- e.g. '1 tbsp = 3/4 tbsp'
    SubstitutionNotes         NVARCHAR(MAX) NULL,
    BestFor                   NVARCHAR(500) NULL,
    NotSuitableFor            NVARCHAR(500) NULL,
    RankOrder                 TINYINT NOT NULL DEFAULT 1,
    AllergenFreeJson          NVARCHAR(MAX) NULL,    -- JSON array: ["gluten","dairy"]
    IsHomemadeRecipeAvailable BIT NOT NULL DEFAULT 0,
    HomemadeRecipeId          UNIQUEIDENTIFIER NULL,  -- RecipeService.Recipe.Id (no FK – cross-service)
    IsActive                  BIT NOT NULL DEFAULT 1,
    CreatedAt                 DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_FoodGroupMember_Group FOREIGN KEY (FoodGroupId) REFERENCES FoodGroup(Id) ON DELETE CASCADE,
    CONSTRAINT CK_FoodGroupMember_HasIdentity CHECK (
        IngredientId IS NOT NULL OR ProductId IS NOT NULL OR CustomName IS NOT NULL)
);
GO

CREATE INDEX IX_FoodGroupMember_FoodGroupId ON FoodGroupMember(FoodGroupId);
GO

CREATE INDEX IX_FoodGroupMember_IngredientId ON FoodGroupMember(IngredientId);
GO

-- -------------------------------------------------------------------------
-- 3. IngredientSubstitutionHistory – user-level substitution tracking
-- -------------------------------------------------------------------------
CREATE TABLE IngredientSubstitutionHistory (
    Id                      UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId                  UNIQUEIDENTIFIER NOT NULL,
    OriginalIngredientId    UNIQUEIDENTIFIER NULL,
    OriginalCustomName      NVARCHAR(200) NULL,
    SubstituteIngredientId  UNIQUEIDENTIFIER NULL,
    SubstituteCustomName    NVARCHAR(200) NULL,
    RecipeId                UNIQUEIDENTIFIER NULL,
    CookedAt                DATETIME2 NULL,
    UserRating              TINYINT NULL,   -- 1-5: how well did the substitution work?
    Notes                   NVARCHAR(500) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_SubHistory_UserId ON IngredientSubstitutionHistory(UserId);
GO

CREATE INDEX IX_SubHistory_OriginalIngredient ON IngredientSubstitutionHistory(OriginalIngredientId);
GO
