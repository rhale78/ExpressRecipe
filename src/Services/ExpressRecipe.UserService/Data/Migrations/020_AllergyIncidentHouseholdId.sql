-- Migration: 020_AllergyIncidentHouseholdId
-- Description: Add HouseholdId to AllergyIncident table so background workers (which have
--              no HTTP context) can record which household an incident belongs to.
-- Date: 2026-03-10

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'AllergyIncident') AND name = N'HouseholdId')
BEGIN
    ALTER TABLE AllergyIncident ADD HouseholdId UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'AllergyIncident') AND name = N'IX_AllergyIncident_HouseholdId')
BEGIN
    CREATE INDEX IX_AllergyIncident_HouseholdId ON AllergyIncident(HouseholdId) WHERE HouseholdId IS NOT NULL;
END
GO
