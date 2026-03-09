-- Migration: 007_LivestockTables
-- Description: Livestock/flock management, daily production logging, and harvest/processing events
-- Date: 2026-03-09

CREATE TABLE LivestockAnimal (
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId         UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(200) NOT NULL,           -- "Hen House A", "Bessie", "Meat Rabbits"
    AnimalType          NVARCHAR(50) NOT NULL,            -- Chicken, Duck, Turkey, Goose, Cow, Goat,
                                                          -- Sheep, Pig, Rabbit, Bee (hive), Other
    ProductionCategory  NVARCHAR(30) NOT NULL,            -- Egg, Dairy, Meat, Dual, Honey
    IsFlockOrHerd       BIT NOT NULL DEFAULT 0,           -- 1 = group; 0 = individual animal
    Count               INT NOT NULL DEFAULT 1,           -- animals in group (1 if individual)
    AcquiredDate        DATE NULL,
    BreedNotes          NVARCHAR(200) NULL,
    IsActive            BIT NOT NULL DEFAULT 1,
    Notes               NVARCHAR(MAX) NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt           DATETIME2 NULL
);
CREATE INDEX IX_LivestockAnimal_HouseholdId ON LivestockAnimal(HouseholdId, IsActive);
GO

CREATE TABLE LivestockProduction (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AnimalId         UNIQUEIDENTIFIER NOT NULL,
    ProductionDate   DATE NOT NULL,
    ProductType      NVARCHAR(50) NOT NULL,   -- Eggs, Milk, Honey, Fiber (wool/mohair)
    Quantity         DECIMAL(10, 3) NOT NULL,
    Unit             NVARCHAR(30) NOT NULL,   -- count, gallon, liter, lb, oz, fl_oz
    AddedToInventory BIT NOT NULL DEFAULT 0,
    InventoryItemId  UNIQUEIDENTIFIER NULL,
    Notes            NVARCHAR(500) NULL,
    CreatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_LivestockProduction_Animal FOREIGN KEY (AnimalId)
        REFERENCES LivestockAnimal(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_LivestockProduction_AnimalDate UNIQUE (AnimalId, ProductionDate, ProductType)
);
CREATE INDEX IX_LivestockProduction_AnimalId ON LivestockProduction(AnimalId, ProductionDate DESC);
CREATE INDEX IX_LivestockProduction_Date     ON LivestockProduction(ProductionDate DESC);
GO

CREATE TABLE LivestockHarvest (
    Id                 UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AnimalId           UNIQUEIDENTIFIER NOT NULL,
    HarvestDate        DATE NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
    CountHarvested     INT NOT NULL DEFAULT 1,
    LiveWeightLbs      DECIMAL(10, 3) NULL,
    ProcessedWeightLbs DECIMAL(10, 3) NULL,
    YieldItemsJson     NVARCHAR(MAX) NULL,    -- JSON: [{cut, weightLbs, unit, inventoryItemId}]
    AddedToInventory   BIT NOT NULL DEFAULT 0,
    ProcessedBy        NVARCHAR(200) NULL,    -- "Self", "Processor: Acme Meats", etc.
    Notes              NVARCHAR(500) NULL,
    CreatedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_LivestockHarvest_Animal FOREIGN KEY (AnimalId)
        REFERENCES LivestockAnimal(Id) ON DELETE CASCADE
);
CREATE INDEX IX_LivestockHarvest_AnimalId ON LivestockHarvest(AnimalId, HarvestDate DESC);
GO
