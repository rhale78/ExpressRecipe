-- Fix ingredient filtering to include raw ingredient strings
-- This ensures products imported with text ingredients (not structured) are also filtered

-- Drop the main view if it exists
IF OBJECT_ID('vw_ProductIngredientFlat', 'V') IS NOT NULL
    DROP VIEW vw_ProductIngredientFlat;
GO

-- Recreate view to include BOTH structured ingredients AND raw ingredient text
-- Using standard view (no indexing) - full-text search will handle performance
CREATE VIEW vw_ProductIngredientFlat
AS
-- Structured ingredients (with IngredientId)
SELECT
    p.Id AS ProductId,
    p.Name AS ProductName,
    p.Brand,
    p.Barcode,
    p.Category,
    p.ApprovalStatus,
    i.Id AS IngredientId,
    LOWER(i.Name) AS IngredientName,
    pi.OrderIndex,
    pi.IngredientListString AS RawIngredientText,
    pi.Notes AS HierarchyInfo,
    CAST(0 AS BIT) AS IsRawText
FROM Product p
INNER JOIN ProductIngredient pi ON p.Id = pi.ProductId
INNER JOIN Ingredient i ON pi.IngredientId = i.Id
WHERE p.IsDeleted = 0 AND pi.IsDeleted = 0 AND i.IsDeleted = 0

UNION ALL

-- Raw ingredient text (IngredientId is null, only IngredientListString populated)
SELECT
    p.Id AS ProductId,
    p.Name AS ProductName,
    p.Brand,
    p.Barcode,
    p.Category,
    p.ApprovalStatus,
    CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER) AS IngredientId, -- Placeholder GUID
    LOWER(pi.IngredientListString) AS IngredientName,
    pi.OrderIndex,
    pi.IngredientListString AS RawIngredientText,
    pi.Notes AS HierarchyInfo,
    CAST(1 AS BIT) AS IsRawText
FROM Product p
INNER JOIN ProductIngredient pi ON p.Id = pi.ProductId
WHERE p.IsDeleted = 0
  AND pi.IsDeleted = 0
  AND pi.IngredientId IS NULL
  AND pi.IngredientListString IS NOT NULL
  AND LTRIM(RTRIM(pi.IngredientListString)) <> '';
GO

-- Add regular indexes for ingredient lookups (using LIKE queries)
-- These will support the LOWER(IngredientName) LIKE '%term%' queries in the search

-- Index for ProductIngredient lookups by ProductId
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ProductIngredient') AND name = 'IX_ProductIngredient_ProductId_IngredientListString')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProductIngredient_ProductId_IngredientListString
        ON ProductIngredient(ProductId)
        INCLUDE (IngredientListString, OrderIndex, IngredientId, IsDeleted)
        WHERE IsDeleted = 0;
END
GO

-- Index on Ingredient.Name for faster structured ingredient lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Ingredient') AND name = 'IX_Ingredient_Name_Lower')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Ingredient_Name_Lower
        ON Ingredient(Name)
        INCLUDE (Id, Category, IsCommonAllergen)
        WHERE IsDeleted = 0;
END
GO

-- Index on Product for view joins
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Product') AND name = 'IX_Product_IsDeleted_Id')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Product_IsDeleted_Id
        ON Product(IsDeleted, Id)
        INCLUDE (Name, Brand, Barcode, Category, ApprovalStatus);
END
GO
