-- Migration: 006_LongTermStorageColumns
-- Description: Add long-term storage and homestead source columns to InventoryItem
-- Date: 2026-03-09

-- Add homestead/source tracking and long-term storage fields to InventoryItem
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryItem') AND name = 'Source')
BEGIN
    ALTER TABLE InventoryItem
        ADD Source NVARCHAR(50) NULL;
    -- 'Purchased' | 'Homestead' | 'Garden' | 'Gift' | 'Other'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryItem') AND name = 'StorageMethod')
BEGIN
    ALTER TABLE InventoryItem
        ADD StorageMethod NVARCHAR(50) NULL;
    -- NULL (fresh) | 'Frozen' | 'FrozenMeal' | 'Canned' | 'FreezeDried' | 'Dehydrated' | 'Fermented' | 'Pickled'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryItem') AND name = 'IsLongTermStorage')
BEGIN
    ALTER TABLE InventoryItem
        ADD IsLongTermStorage BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryItem') AND name = 'BatchLabel')
BEGIN
    ALTER TABLE InventoryItem
        ADD BatchLabel NVARCHAR(200) NULL;
    -- e.g. "2026-02 Rabbit Whole", "Spring 2026 Honey"
END
GO

CREATE INDEX IX_InventoryItem_Source ON InventoryItem(Source) WHERE Source IS NOT NULL;
GO
