-- Migration: 005_StorageLocationExtended
-- Description: Add StorageType, outage tracking, equipment link, and food category defaults to StorageLocation (EQ2)
-- Date: 2026-03-09

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'StorageType' AND Object_ID = Object_ID(N'StorageLocation')
)
BEGIN
    ALTER TABLE StorageLocation ADD StorageType NVARCHAR(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'EquipmentInstanceId' AND Object_ID = Object_ID(N'StorageLocation')
)
BEGIN
    ALTER TABLE StorageLocation ADD EquipmentInstanceId UNIQUEIDENTIFIER NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'DefaultFoodCategories' AND Object_ID = Object_ID(N'StorageLocation')
)
BEGIN
    ALTER TABLE StorageLocation ADD DefaultFoodCategories NVARCHAR(MAX) NULL; -- JSON array
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'OutageActive' AND Object_ID = Object_ID(N'StorageLocation')
)
BEGIN
    ALTER TABLE StorageLocation ADD OutageActive BIT NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'OutageType' AND Object_ID = Object_ID(N'StorageLocation')
)
BEGIN
    ALTER TABLE StorageLocation ADD OutageType NVARCHAR(50) NULL; -- PowerOutage|EquipmentFailure|MaintenanceDown
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'OutageStartedAt' AND Object_ID = Object_ID(N'StorageLocation')
)
BEGIN
    ALTER TABLE StorageLocation ADD OutageStartedAt DATETIME2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'OutageNotes' AND Object_ID = Object_ID(N'StorageLocation')
)
BEGIN
    ALTER TABLE StorageLocation ADD OutageNotes NVARCHAR(MAX) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_StorageLocation_EquipmentInstance'
      AND parent_object_id = Object_ID(N'StorageLocation')
)
BEGIN
    ALTER TABLE StorageLocation ADD
        CONSTRAINT FK_StorageLocation_EquipmentInstance FOREIGN KEY (EquipmentInstanceId)
            REFERENCES EquipmentInstance(Id);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_StorageLocation_OutageActive'
      AND object_id = Object_ID(N'StorageLocation')
)
BEGIN
    CREATE INDEX IX_StorageLocation_OutageActive ON StorageLocation(OutageActive, HouseholdId)
        WHERE OutageActive = 1;
END;
GO

-- Backfill: map existing Temperature values to StorageType
UPDATE StorageLocation SET StorageType = 'Freezer'      WHERE Temperature = 'Frozen'  AND StorageType IS NULL;
UPDATE StorageLocation SET StorageType = 'Refrigerator' WHERE Temperature = 'Cold'    AND StorageType IS NULL;
UPDATE StorageLocation SET StorageType = 'Pantry'       WHERE Temperature = 'Room'    AND StorageType IS NULL;
GO
