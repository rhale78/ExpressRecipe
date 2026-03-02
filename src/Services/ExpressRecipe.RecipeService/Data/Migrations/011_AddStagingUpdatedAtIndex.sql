-- Add index on UpdatedAt for RecipeStaging to support efficient staleness checks
CREATE INDEX IX_RecipeStaging_UpdatedAt ON RecipeStaging(UpdatedAt) WHERE IsDeleted = 0 AND UpdatedAt IS NOT NULL;
GO
