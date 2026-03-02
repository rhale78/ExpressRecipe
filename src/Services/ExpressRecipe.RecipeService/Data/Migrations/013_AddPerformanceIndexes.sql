-- Migration 013: Add Performance Indexes for Recipe Completeness Queries
-- Optimizes GetAllRecipeTitlesCompletenessAsync performance

-- Index for RecipeIngredient lookups by RecipeId (if not exists)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeIngredient_RecipeId_IsDeleted' AND object_id = OBJECT_ID('RecipeIngredient'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RecipeIngredient_RecipeId_IsDeleted 
    ON RecipeIngredient(RecipeId, IsDeleted) 
    INCLUDE (Id)
    WITH (ONLINE = ON, FILLFACTOR = 90);
    PRINT 'Created index: IX_RecipeIngredient_RecipeId_IsDeleted';
END
ELSE
BEGIN
    PRINT 'Index IX_RecipeIngredient_RecipeId_IsDeleted already exists';
END
GO

-- Index for RecipeInstruction lookups by RecipeId (if not exists)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeInstruction_RecipeId' AND object_id = OBJECT_ID('RecipeInstruction'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RecipeInstruction_RecipeId 
    ON RecipeInstruction(RecipeId) 
    INCLUDE (Id)
    WITH (ONLINE = ON, FILLFACTOR = 90);
    PRINT 'Created index: IX_RecipeInstruction_RecipeId';
END
ELSE
BEGIN
    PRINT 'Index IX_RecipeInstruction_RecipeId already exists';
END
GO

-- Optimize Recipe table filtering on IsDeleted (if not already covered by existing index)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Recipe_IsDeleted_Name' AND object_id = OBJECT_ID('Recipe'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Recipe_IsDeleted_Name 
    ON Recipe(IsDeleted, Name) 
    INCLUDE (Id)
    WITH (ONLINE = ON, FILLFACTOR = 90);
    PRINT 'Created index: IX_Recipe_IsDeleted_Name';
END
ELSE
BEGIN
    PRINT 'Index IX_Recipe_IsDeleted_Name already exists';
END
GO

PRINT 'Migration 013 completed successfully - Performance indexes added';
