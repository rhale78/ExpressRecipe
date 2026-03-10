-- Migration: 019_AllergyIncidentEngine
-- Description: Create tables for the allergy incident engine (Part 1 of 3).
--   Tracks multi-product, multi-member allergy incidents; suspected allergens derived
--   by differential analysis; and user-confirmed cleared ingredients.
-- Date: 2026-03-10

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
        RowVersion              ROWVERSION,
        CONSTRAINT UQ_SuspectedAllergen UNIQUE (HouseholdId, MemberId, IngredientName)
    );
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
        CONSTRAINT UQ_ClearedIngredient UNIQUE (HouseholdId, MemberId, IngredientName)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ClearedIngredient') AND name = 'IX_CI_Household')
BEGIN
    CREATE INDEX IX_CI_Household ON ClearedIngredient(HouseholdId, MemberId);
END
GO
