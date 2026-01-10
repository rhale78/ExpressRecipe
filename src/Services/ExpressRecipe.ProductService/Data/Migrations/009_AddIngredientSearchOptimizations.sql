-- Add optimizations for fast, flexible allergen and ingredient queries

-- 1. Create a flattened ingredient view for fast searching
-- This denormalizes the data for query performance
CREATE VIEW vw_ProductIngredientFlat
WITH SCHEMABINDING
AS
SELECT
    p.Id AS ProductId,
    p.Name AS ProductName,
    p.Brand,
    p.Barcode,
    p.Category,
    p.ApprovalStatus,
    i.Id AS IngredientId,
    LOWER(i.Name) AS IngredientName, -- Lowercase for case-insensitive matching
    pi.OrderIndex,
    pi.IngredientListString AS RawIngredientText,
    pi.Notes AS HierarchyInfo
FROM dbo.Product p
INNER JOIN dbo.ProductIngredient pi ON p.Id = pi.ProductId
INNER JOIN dbo.Ingredient i ON pi.IngredientId = i.Id
WHERE p.IsDeleted = 0 AND pi.IsDeleted = 0 AND i.IsDeleted = 0;
GO

-- 2. Create clustered index on the flattened view for fast ingredient lookups
CREATE UNIQUE CLUSTERED INDEX IX_ProductIngredientFlat_IngredientProduct
    ON vw_ProductIngredientFlat(IngredientName, ProductId, IngredientId);
GO

-- 3. Add non-clustered index for product-based lookups
CREATE NONCLUSTERED INDEX IX_ProductIngredientFlat_ProductId
    ON vw_ProductIngredientFlat(ProductId)
    INCLUDE (IngredientName, OrderIndex);
GO

-- 4. Create helper function for flexible allergen matching
-- Supports partial matching and multiple variations
GO
CREATE FUNCTION dbo.fn_ContainsIngredient
(
    @ProductId UNIQUEIDENTIFIER,
    @IngredientName NVARCHAR(200)
)
RETURNS BIT
AS
BEGIN
    DECLARE @Result BIT = 0;

    -- Check if any ingredient contains the search term (case-insensitive)
    IF EXISTS (
        SELECT 1
        FROM vw_ProductIngredientFlat
        WHERE ProductId = @ProductId
            AND (
                IngredientName LIKE '%' + LOWER(@IngredientName) + '%'
                OR LOWER(@IngredientName) LIKE '%' + IngredientName + '%'
            )
    )
    BEGIN
        SET @Result = 1;
    END

    RETURN @Result;
END;
GO

-- 5. Create table for user allergen/restriction profiles (for caching common searches)
CREATE TABLE UserAllergenProfile (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    AllergenName NVARCHAR(200) NOT NULL,
    Severity NVARCHAR(50) NULL, -- 'Severe', 'Moderate', 'Mild', 'Preference'
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_UserAllergenProfile_User_Allergen UNIQUE (UserId, AllergenName)
);
GO

CREATE INDEX IX_UserAllergenProfile_UserId ON UserAllergenProfile(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserAllergenProfile_AllergenName ON UserAllergenProfile(AllergenName) WHERE IsDeleted = 0;
GO

-- 6. Create materialized cache table for common allergen searches
-- This will speed up repeated searches for the same allergen
CREATE TABLE ProductAllergenCache (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    AllergenName NVARCHAR(200) NOT NULL,
    ContainsAllergen BIT NOT NULL,
    LastChecked DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProductAllergenCache_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ProductAllergenCache_Product_Allergen UNIQUE (ProductId, AllergenName)
);
GO

CREATE INDEX IX_ProductAllergenCache_AllergenName_Contains
    ON ProductAllergenCache(AllergenName, ContainsAllergen)
    INCLUDE (ProductId)
    WHERE ContainsAllergen = 0; -- Only index safe products for fast filtering
GO

-- 7. Additional indexes for performance
CREATE INDEX IX_Ingredient_Name_Lower
    ON Ingredient(Name)
    INCLUDE (Category, IsCommonAllergen)
    WHERE IsDeleted = 0;
GO

-- Drop existing index if it exists from previous migration
DROP INDEX IF EXISTS IX_ProductAllergen_AllergenName ON ProductAllergen;
GO

-- Create enhanced index with additional columns
CREATE INDEX IX_ProductAllergen_AllergenName
    ON ProductAllergen(AllergenName, ProductId)
    INCLUDE (AllergenType, Severity)
    WHERE IsDeleted = 0;
GO
