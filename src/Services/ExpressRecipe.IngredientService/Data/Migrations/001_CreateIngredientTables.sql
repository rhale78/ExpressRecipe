-- Ingredient Service Initial Schema

CREATE TABLE Ingredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    AlternativeNames NVARCHAR(MAX) NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NULL,
    IsCommonAllergen BIT NOT NULL DEFAULT 0,
    IngredientListString NVARCHAR(MAX) NULL,
    
    -- Standard tracking
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    
    CONSTRAINT UQ_Ingredient_Name UNIQUE (Name)
);

CREATE INDEX IX_Ingredient_Name ON Ingredient(Name) WHERE IsDeleted = 0;
CREATE INDEX IX_Ingredient_Category ON Ingredient(Category) WHERE IsDeleted = 0;
GO

CREATE TABLE BaseIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_BaseIngredient_Name UNIQUE (Name)
);

CREATE INDEX IX_BaseIngredient_Name ON BaseIngredient(Name) WHERE IsDeleted = 0;
GO

-- Mapping table for complex ingredients to their base components
CREATE TABLE IngredientBaseComponent (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IngredientId UNIQUEIDENTIFIER NOT NULL,
    BaseIngredientId UNIQUEIDENTIFIER NOT NULL,
    Percentage DECIMAL(5,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_IBC_Ingredient FOREIGN KEY (IngredientId) REFERENCES Ingredient(Id),
    CONSTRAINT FK_IBC_BaseIngredient FOREIGN KEY (BaseIngredientId) REFERENCES BaseIngredient(Id),
    CONSTRAINT UQ_IBC_Mapping UNIQUE (IngredientId, BaseIngredientId)
);
GO
