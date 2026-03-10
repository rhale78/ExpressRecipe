CREATE TABLE UnresolvedIngredientQueue (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RawText         NVARCHAR(500) NOT NULL,
    NormalizedText  NVARCHAR(500) NOT NULL,
    SourceService   NVARCHAR(100) NOT NULL,
    SourceEntityId  UNIQUEIDENTIFIER NULL,
    BestMatchId     UNIQUEIDENTIFIER NULL,
    BestMatchName   NVARCHAR(300) NULL,
    BestConfidence  DECIMAL(5,4) NULL,
    BestStrategy    NVARCHAR(50) NULL,
    OccurrenceCount INT NOT NULL DEFAULT 1,
    ResolvedAt      DATETIME2 NULL,
    ResolvedToId    UNIQUEIDENTIFIER NULL,
    Resolution      NVARCHAR(50) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NULL,
    CONSTRAINT UQ_UnresolvedQueue_NormalizedText UNIQUE (NormalizedText)
);
CREATE INDEX IX_UnresolvedQueue_Unresolved     ON UnresolvedIngredientQueue(ResolvedAt) WHERE ResolvedAt IS NULL;
CREATE INDEX IX_UnresolvedQueue_OccurrenceCount ON UnresolvedIngredientQueue(OccurrenceCount DESC) WHERE ResolvedAt IS NULL;
GO
