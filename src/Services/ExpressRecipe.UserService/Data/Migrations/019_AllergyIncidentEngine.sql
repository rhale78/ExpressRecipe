-- Migration: 019_AllergyIncidentEngine
-- Description: Household-based allergy incident tracking tables for the differential analysis engine.
-- Date: 2026-03-10

-- HouseholdAllergyIncident: Records an allergy incident for a household
CREATE TABLE HouseholdAllergyIncident (
    Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId    UNIQUEIDENTIFIER NOT NULL,
    IncidentDate   DATETIME2        NOT NULL,
    Notes          NVARCHAR(MAX)    NULL,
    AnalysisRun    BIT              NOT NULL DEFAULT 0,
    AnalysisRunAt  DATETIME2        NULL,
    CreatedBy      UNIQUEIDENTIFIER NULL,
    CreatedAt      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy      UNIQUEIDENTIFIER NULL,
    UpdatedAt      DATETIME2        NULL,
    IsDeleted      BIT              NOT NULL DEFAULT 0,
    DeletedAt      DATETIME2        NULL
);
GO

CREATE INDEX IX_HouseholdAllergyIncident_HouseholdId
    ON HouseholdAllergyIncident(HouseholdId);
CREATE INDEX IX_HouseholdAllergyIncident_AnalysisRun
    ON HouseholdAllergyIncident(AnalysisRun);
GO

-- HouseholdAllergyIncidentMember: Which household members were affected in an incident
CREATE TABLE HouseholdAllergyIncidentMember (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IncidentId    UNIQUEIDENTIFIER NOT NULL,
    MemberId      UNIQUEIDENTIFIER NULL,   -- NULL = primary account member
    MemberName    NVARCHAR(200)    NOT NULL,
    SeverityLevel NVARCHAR(50)     NOT NULL DEFAULT 'Intolerance',
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_HAIMember_Incident FOREIGN KEY (IncidentId)
        REFERENCES HouseholdAllergyIncident(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_HAIMember_IncidentId  ON HouseholdAllergyIncidentMember(IncidentId);
CREATE INDEX IX_HAIMember_MemberId    ON HouseholdAllergyIncidentMember(MemberId);
GO

-- HouseholdAllergyIncidentProduct: Products present during an incident (with/without reaction)
CREATE TABLE HouseholdAllergyIncidentProduct (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IncidentId  UNIQUEIDENTIFIER NOT NULL,
    ProductId   UNIQUEIDENTIFIER NULL,   -- NULL = product not in catalogue
    HadReaction BIT              NOT NULL DEFAULT 0,
    Notes       NVARCHAR(500)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_HAIProduct_Incident FOREIGN KEY (IncidentId)
        REFERENCES HouseholdAllergyIncident(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_HAIProduct_IncidentId ON HouseholdAllergyIncidentProduct(IncidentId);
GO

-- SuspectedAllergen: Allergen candidates produced by the differential analysis engine.
-- MemberKey is a persisted computed column that maps NULL MemberId to a sentinel GUID so that
-- the unique index correctly prevents duplicate rows for the primary (NULL) member.
CREATE TABLE SuspectedAllergen (
    Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId    UNIQUEIDENTIFIER NOT NULL,
    MemberId       UNIQUEIDENTIFIER NULL,
    MemberName     NVARCHAR(200)    NOT NULL,
    IngredientName NVARCHAR(300)    NOT NULL,
    IngredientId   UNIQUEIDENTIFIER NULL,
    Confidence     DECIMAL(5,4)     NOT NULL,
    IncidentCount  INT              NOT NULL DEFAULT 1,
    IsActive       BIT              NOT NULL DEFAULT 1,
    DetectedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt      DATETIME2        NULL,
    -- Computed sentinel: '00000000-0000-0000-0000-000000000000' stands in for NULL MemberId
    MemberKey      AS ISNULL(MemberId, '00000000-0000-0000-0000-000000000000') PERSISTED,
    CONSTRAINT UQ_SuspectedAllergen_Member_Ingredient
        UNIQUE (HouseholdId, MemberKey, IngredientName)
);
GO

CREATE INDEX IX_SuspectedAllergen_HouseholdId  ON SuspectedAllergen(HouseholdId);
CREATE INDEX IX_SuspectedAllergen_MemberId     ON SuspectedAllergen(MemberId);
GO

-- ClearedIngredient: Ingredients cleared from suspicion after appearing in safe-product history.
-- Unique index prevents duplicate rows across re-analysis runs (INSERT is guarded by IF NOT EXISTS).
CREATE TABLE ClearedIngredient (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId   UNIQUEIDENTIFIER NOT NULL,
    MemberId      UNIQUEIDENTIFIER NULL,
    MemberName    NVARCHAR(200)    NOT NULL,
    IngredientName NVARCHAR(300)   NOT NULL,
    IngredientId  UNIQUEIDENTIFIER NULL,
    ClearedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    -- Same sentinel pattern as SuspectedAllergen to allow a unique index over nullable MemberId
    MemberKey     AS ISNULL(MemberId, '00000000-0000-0000-0000-000000000000') PERSISTED,
    CONSTRAINT UQ_ClearedIngredient_Member_Ingredient
        UNIQUE (HouseholdId, MemberKey, IngredientName)
);
GO

CREATE INDEX IX_ClearedIngredient_HouseholdId ON ClearedIngredient(HouseholdId);
CREATE INDEX IX_ClearedIngredient_MemberId    ON ClearedIngredient(MemberId);
GO
