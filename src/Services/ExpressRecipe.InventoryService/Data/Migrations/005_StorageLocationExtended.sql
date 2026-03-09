-- Migration: 005_StorageLocationExtended
-- Description: Add StorageType, outage tracking, equipment link, and food category defaults to StorageLocation (EQ2)
-- Date: 2026-03-09

ALTER TABLE StorageLocation
    ADD StorageType           NVARCHAR(50)  NULL,
        EquipmentInstanceId   UNIQUEIDENTIFIER NULL,
        DefaultFoodCategories NVARCHAR(MAX) NULL, -- JSON array
        OutageActive          BIT NOT NULL DEFAULT 0,
        OutageType            NVARCHAR(50)  NULL, -- PowerOutage|EquipmentFailure|MaintenanceDown
        OutageStartedAt       DATETIME2     NULL,
        OutageNotes           NVARCHAR(MAX) NULL;
GO

ALTER TABLE StorageLocation ADD
    CONSTRAINT FK_StorageLocation_EquipmentInstance FOREIGN KEY (EquipmentInstanceId)
        REFERENCES EquipmentInstance(Id);
GO

CREATE INDEX IX_StorageLocation_OutageActive ON StorageLocation(OutageActive, HouseholdId)
    WHERE OutageActive = 1;
GO

-- Backfill: map existing Temperature values to StorageType
UPDATE StorageLocation SET StorageType = 'Freezer'      WHERE Temperature = 'Frozen'  AND StorageType IS NULL;
UPDATE StorageLocation SET StorageType = 'Refrigerator' WHERE Temperature = 'Cold'    AND StorageType IS NULL;
UPDATE StorageLocation SET StorageType = 'Pantry'       WHERE Temperature = 'Room'    AND StorageType IS NULL;
GO
