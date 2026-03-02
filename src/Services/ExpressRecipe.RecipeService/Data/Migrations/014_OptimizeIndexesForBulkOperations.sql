-- Migration 014: Optimize Indexes for Bulk Operations
-- Description: Drop redundant indexes and add FILLFACTOR to reduce page contention

-- 1. Drop redundant index on RecipeTagMapping.RecipeId (covered by unique constraint)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeTagMapping_RecipeId' AND object_id = OBJECT_ID('RecipeTagMapping'))
BEGIN
    DROP INDEX IX_RecipeTagMapping_RecipeId ON RecipeTagMapping;
    PRINT 'Dropped redundant index: IX_RecipeTagMapping_RecipeId (covered by unique constraint)';
END
GO

-- 2. Drop rarely-used BaseIngredientId index to reduce bulk insert overhead
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeIngredient_BaseIngredientId' AND object_id = OBJECT_ID('RecipeIngredient'))
BEGIN
    DROP INDEX IX_RecipeIngredient_BaseIngredientId ON RecipeIngredient;
    PRINT 'Dropped index: IX_RecipeIngredient_BaseIngredientId (rarely used, high bulk insert cost)';
END
GO

-- 3. Rebuild RecipeIngredient.RecipeId index with FILLFACTOR to reduce page splits
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeIngredient_RecipeId' AND object_id = OBJECT_ID('RecipeIngredient'))
BEGIN
    DROP INDEX IX_RecipeIngredient_RecipeId ON RecipeIngredient;
END

CREATE NONCLUSTERED INDEX IX_RecipeIngredient_RecipeId 
ON RecipeIngredient(RecipeId) 
WITH (FILLFACTOR = 70, ONLINE = ON);
PRINT 'Rebuilt IX_RecipeIngredient_RecipeId with FILLFACTOR=70';
GO

-- 4. Rebuild RecipeInstruction.RecipeId index with FILLFACTOR
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeInstruction_RecipeId' AND object_id = OBJECT_ID('RecipeInstruction'))
BEGIN
    DROP INDEX IX_RecipeInstruction_RecipeId ON RecipeInstruction;
END

CREATE NONCLUSTERED INDEX IX_RecipeInstruction_RecipeId 
ON RecipeInstruction(RecipeId) 
WITH (FILLFACTOR = 70, ONLINE = ON);
PRINT 'Rebuilt IX_RecipeInstruction_RecipeId with FILLFACTOR=70';
GO

-- 5. Rebuild RecipeImage.RecipeId index with FILLFACTOR
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeImage_RecipeId' AND object_id = OBJECT_ID('RecipeImage'))
BEGIN
    DROP INDEX IX_RecipeImage_RecipeId ON RecipeImage;
END

CREATE NONCLUSTERED INDEX IX_RecipeImage_RecipeId 
ON RecipeImage(RecipeId) 
WITH (FILLFACTOR = 70, ONLINE = ON);
PRINT 'Rebuilt IX_RecipeImage_RecipeId with FILLFACTOR=70';
GO

-- 6. Rebuild RecipeTagMapping.TagId index with FILLFACTOR (keep this one, needed for reverse lookups)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeTagMapping_TagId' AND object_id = OBJECT_ID('RecipeTagMapping'))
BEGIN
    DROP INDEX IX_RecipeTagMapping_TagId ON RecipeTagMapping;
END

CREATE NONCLUSTERED INDEX IX_RecipeTagMapping_TagId 
ON RecipeTagMapping(TagId) 
WITH (FILLFACTOR = 70, ONLINE = ON);
PRINT 'Rebuilt IX_RecipeTagMapping_TagId with FILLFACTOR=70';
GO

PRINT 'Migration 014 completed - Indexes optimized for bulk operations';
PRINT 'Benefits: Reduced page contention, eliminated redundant indexes, 30% free space per page';
