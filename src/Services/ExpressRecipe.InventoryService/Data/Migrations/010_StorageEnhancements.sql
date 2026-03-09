-- Migration: 010_StorageEnhancements
-- Description: Add storage type, equipment link, outage tracking, and food categories to StorageLocation
-- Date: 2026-03-09

ALTER TABLE StorageLocation ADD StorageType         NVARCHAR(50) NULL;
    -- Pantry|Freezer|Refrigerator|RootCellar|Cabinet|Counter|Basement|Garage|Cellar|ColdRoom|Other
ALTER TABLE StorageLocation ADD EquipmentInstanceId UNIQUEIDENTIFIER NULL;
ALTER TABLE StorageLocation ADD CONSTRAINT FK_StorageLocation_EquipmentInstance
    FOREIGN KEY (EquipmentInstanceId) REFERENCES EquipmentInstance(Id) ON DELETE SET NULL;
ALTER TABLE StorageLocation ADD OutageActive        BIT NOT NULL DEFAULT 0;
ALTER TABLE StorageLocation ADD OutageStartedAt     DATETIME2 NULL;
ALTER TABLE StorageLocation ADD OutageType          NVARCHAR(50) NULL;
    -- PowerOutage|EquipmentFailure|MaintenanceDown
ALTER TABLE StorageLocation ADD OutageNotes         NVARCHAR(500) NULL;
CREATE INDEX IX_StorageLocation_Outage ON StorageLocation(OutageActive) WHERE OutageActive=1;
GO

CREATE TABLE StorageLocationFoodCategory (
    Id                UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    StorageLocationId UNIQUEIDENTIFIER NOT NULL,
    FoodCategory      NVARCHAR(50) NOT NULL,
    -- Produce|Dairy|Meat|Poultry|Seafood|Frozen|Canned|DryGoods|
    -- Beverages|Condiments|Spices|Baked|Eggs|Homestead
    CONSTRAINT FK_SLFC_Storage FOREIGN KEY (StorageLocationId)
        REFERENCES StorageLocation(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_SLFC UNIQUE (StorageLocationId, FoodCategory)
);
GO
