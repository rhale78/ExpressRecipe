-- Migration: 017_AddUnitPreference
-- Description: Add unit system preference columns to UserProfile.
-- Date: 2026-03-09

ALTER TABLE UserProfile
    ADD UnitSystemPreference NVARCHAR(10) NOT NULL DEFAULT 'US',
        TemperatureUnit      NVARCHAR(5)  NOT NULL DEFAULT 'F',
        WeightUnitOverride   NVARCHAR(10) NULL,
        VolumeUnitOverride   NVARCHAR(10) NULL;
GO
