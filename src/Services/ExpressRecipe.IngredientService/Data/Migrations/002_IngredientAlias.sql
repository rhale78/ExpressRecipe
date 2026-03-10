CREATE TABLE IngredientAlias (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IngredientId    UNIQUEIDENTIFIER NOT NULL,
    AliasText       NVARCHAR(300) NOT NULL,
    NormalizedAlias NVARCHAR(300) NOT NULL,
    Source          NVARCHAR(50) NOT NULL DEFAULT 'AdminSeeded',
    MatchCount      INT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_IngredientAlias_Ingredient FOREIGN KEY (IngredientId) REFERENCES Ingredient(Id),
    CONSTRAINT UQ_IngredientAlias_NormalizedAlias UNIQUE (NormalizedAlias)
);
CREATE INDEX IX_IngredientAlias_IngredientId  ON IngredientAlias(IngredientId);
CREATE INDEX IX_IngredientAlias_NormalizedAlias ON IngredientAlias(NormalizedAlias);
GO

-- Seed from existing AlternativeNames column (comma-separated) — set-based, no cursor
INSERT INTO IngredientAlias (Id, IngredientId, AliasText, NormalizedAlias, Source, CreatedAt)
SELECT NEWID(), i.Id, LTRIM(RTRIM(s.value)), LOWER(LTRIM(RTRIM(s.value))), 'AdminSeeded', GETUTCDATE()
FROM Ingredient i
CROSS APPLY STRING_SPLIT(i.AlternativeNames, ',') s
WHERE i.AlternativeNames IS NOT NULL
  AND LEN(LTRIM(RTRIM(s.value))) > 1
  AND LOWER(LTRIM(RTRIM(s.value))) != LOWER(i.Name)
  AND NOT EXISTS (SELECT 1 FROM IngredientAlias a WHERE a.NormalizedAlias = LOWER(LTRIM(RTRIM(s.value))));
GO
