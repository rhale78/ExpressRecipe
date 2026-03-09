ALTER TABLE Ingredient ADD NormalizedName AS LOWER(Name) PERSISTED;
CREATE INDEX IX_Ingredient_NormalizedName ON Ingredient(NormalizedName) WHERE IsDeleted = 0;
GO
