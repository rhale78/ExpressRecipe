ALTER TABLE InventoryItem ADD StorageMethod       NVARCHAR(50) NULL;
ALTER TABLE InventoryItem ADD IsLongTermStorage   BIT NOT NULL DEFAULT 0;
ALTER TABLE InventoryItem ADD StorageCapacityUnit NVARCHAR(50) NULL;
ALTER TABLE InventoryItem ADD BatchLabel          NVARCHAR(200) NULL;
ALTER TABLE InventoryItem ADD Source              NVARCHAR(100) NULL;
ALTER TABLE InventoryItem ADD Temperature         NVARCHAR(50) NULL;
CREATE INDEX IX_InventoryItem_IsLongTermStorage ON InventoryItem(IsLongTermStorage) WHERE IsLongTermStorage=1;
GO
