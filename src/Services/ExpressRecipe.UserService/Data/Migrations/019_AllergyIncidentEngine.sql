-- Migration: 019_AllergyIncidentEngine
-- Description: Create tables for the allergy incident engine (Part 1 of 3).
--   Tracks multi-product, multi-member allergy incidents; suspected allergens derived
--   by differential analysis; and user-confirmed cleared ingredients.
-- Date: 2026-03-10
-- Note: ISNULL(MemberId, '00000000-0000-0000-0000-000000000000') is used in unique indexes
--       so that NULL MemberId values (= primary user) are treated as equal, not distinct.

-- AllergyIncident: A reaction event (replaces the simpler legacy AllergyIncident row).
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('AllergyIncident2') AND type = 'U')
BEGIN
    CREATE TABLE AllergyIncident2 (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        HouseholdId     UNIQUEIDENTIFIER NOT NULL,          -- = primary user Id
        IncidentDate    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ExposureType    NVARCHAR(20)     NOT NULL DEFAULT 'Ingestion', -- Ingestion | Touch | Smell
        ReactionLatency NVARCHAR(20)     NULL,               -- Immediate | Minutes | Hours | Delayed
        Notes           NVARCHAR(MAX)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy       UNIQUEIDENTIFIER NULL,
        UpdatedAt       DATETIME2        NULL,
        UpdatedBy       UNIQUEIDENTIFIER NULL,
        IsDeleted       BIT              NOT NULL DEFAULT 0,
        DeletedAt       DATETIME2        NULL,
        RowVersion      ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AllergyIncident2') AND name = 'IX_AllergyIncident2_Household')
BEGIN
    CREATE INDEX IX_AllergyIncident2_Household ON AllergyIncident2(HouseholdId, IncidentDate DESC) WHERE IsDeleted = 0;
END
GO

-- AllergyIncidentProduct: Products consumed/touched during the incident.
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('AllergyIncidentProduct') AND type = 'U')
BEGIN
    CREATE TABLE AllergyIncidentProduct (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        IncidentId      UNIQUEIDENTIFIER NOT NULL,
        ProductId       UNIQUEIDENTIFIER NULL,               -- FK to Product catalogue (optional)
        ProductName     NVARCHAR(200)    NOT NULL,
        HadReaction     BIT              NOT NULL DEFAULT 1, -- 1=reaction, 0=control (no reaction)
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_AIP_Incident FOREIGN KEY (IncidentId)
            REFERENCES AllergyIncident2(Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AllergyIncidentProduct') AND name = 'IX_AIP_Incident')
BEGIN
    CREATE INDEX IX_AIP_Incident ON AllergyIncidentProduct(IncidentId);
END
GO

-- AllergyIncidentMember: Which household members were affected.
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('AllergyIncidentMember') AND type = 'U')
BEGIN
    CREATE TABLE AllergyIncidentMember (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        IncidentId      UNIQUEIDENTIFIER NOT NULL,
        MemberId        UNIQUEIDENTIFIER NULL,               -- NULL = primary user ("Me")
        MemberName      NVARCHAR(100)    NOT NULL,
        Severity        NVARCHAR(30)     NOT NULL,           -- Intolerance|Itchy|Rash|ThroatClosing|ERVisit
        ReactionTypes   NVARCHAR(500)    NULL,               -- comma-separated AllergenReactionType names
        TreatmentType   NVARCHAR(100)    NULL,
        TreatmentDose   NVARCHAR(50)     NULL,
        ResolutionTimeMinutes INT        NULL,
        RequiredEpipen  BIT              NOT NULL DEFAULT 0,
        RequiredER      BIT              NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_AIM_Incident FOREIGN KEY (IncidentId)
            REFERENCES AllergyIncident2(Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AllergyIncidentMember') AND name = 'IX_AIM_Incident')
BEGIN
    CREATE INDEX IX_AIM_Incident ON AllergyIncidentMember(IncidentId);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AllergyIncidentMember') AND name = 'IX_AIM_Member')
BEGIN
    CREATE INDEX IX_AIM_Member ON AllergyIncidentMember(MemberId) WHERE MemberId IS NOT NULL;
END
GO

-- SuspectedAllergen: Derived by AllergyDifferentialAnalyzer after each incident.
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SuspectedAllergen') AND type = 'U')
BEGIN
    CREATE TABLE SuspectedAllergen (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        HouseholdId             UNIQUEIDENTIFIER NOT NULL,
        MemberId                UNIQUEIDENTIFIER NULL,       -- NULL = primary user
        IngredientName          NVARCHAR(200)    NOT NULL,
        ConfidenceScore         DECIMAL(5,4)     NOT NULL DEFAULT 0,  -- 0.0000 – 1.0000
        IncidentCount           INT              NOT NULL DEFAULT 1,
        FirstSeenAt             DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        LastUpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsPromotedToConfirmed   BIT              NOT NULL DEFAULT 0,
        PromotedAt              DATETIME2        NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0,
        DeletedAt               DATETIME2        NULL,
        RowVersion              ROWVERSION
    );
END
GO

-- Filtered unique index to allow soft-deleted rows to be re-inserted without a constraint violation.
-- ISNULL(@MemberId, '00000000...') ensures NULL values are treated as equal for uniqueness.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('SuspectedAllergen') AND name = 'UQ_SuspectedAllergen_Active')
BEGIN
    CREATE UNIQUE INDEX UQ_SuspectedAllergen_Active
        ON SuspectedAllergen(HouseholdId, ISNULL(MemberId, '00000000-0000-0000-0000-000000000000'), IngredientName)
        WHERE IsDeleted = 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('SuspectedAllergen') AND name = 'IX_SA_Household')
BEGIN
    CREATE INDEX IX_SA_Household ON SuspectedAllergen(HouseholdId, MemberId, ConfidenceScore DESC) WHERE IsDeleted = 0;
END
GO

-- ClearedIngredient: User-confirmed safe ingredients (analysis engine will not re-suspect these).
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ClearedIngredient') AND type = 'U')
BEGIN
    CREATE TABLE ClearedIngredient (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        HouseholdId         UNIQUEIDENTIFIER NOT NULL,
        MemberId            UNIQUEIDENTIFIER NULL,
        IngredientName      NVARCHAR(200)    NOT NULL,
        ClearedByUserId     UNIQUEIDENTIFIER NOT NULL,
        ClearingIncidentId  UNIQUEIDENTIFIER NULL,           -- NULL = user-initiated (not from analysis)
        ClearedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_ClearedIngredient UNIQUE (HouseholdId, ISNULL(MemberId, '00000000-0000-0000-0000-000000000000'), IngredientName)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ClearedIngredient') AND name = 'IX_CI_Household')
BEGIN
    CREATE INDEX IX_CI_Household ON ClearedIngredient(HouseholdId, MemberId);
END
GO

-- ── Engine tables from PR #62 (differential analysis) ──────────────────────────

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
