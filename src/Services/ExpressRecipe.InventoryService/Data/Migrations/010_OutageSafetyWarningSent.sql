-- Migration: 010_OutageSafetyWarningSent
-- Description: Add OutageSafetyWarningSent tracking column to StorageLocation (EQ3)
-- Date: 2026-03-09

ALTER TABLE StorageLocation
    ADD OutageSafetyWarningSent BIT NOT NULL DEFAULT 0;
GO
