CREATE TABLE GardenProfile (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId UNIQUEIDENTIFIER NOT NULL,
    HasGarden   BIT NOT NULL DEFAULT 1,
    GardenNotes NVARCHAR(MAX) NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2 NULL,
    CONSTRAINT UQ_GardenProfile_Household UNIQUE (HouseholdId)
);
GO

CREATE TABLE GardenPlanting (
    Id                       UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId              UNIQUEIDENTIFIER NOT NULL,
    IngredientId             UNIQUEIDENTIFIER NULL,
    PlantName                NVARCHAR(200) NOT NULL,
    VarietyNotes             NVARCHAR(200) NULL,
    PlantedDate              DATE NOT NULL,
    ExpectedRipeDate         DATE NULL,
    PlantType                NVARCHAR(100) NOT NULL,
    QuantityPlanted          INT NOT NULL DEFAULT 1,
    IsActive                 BIT NOT NULL DEFAULT 1,
    RipeCheckReminderEnabled BIT NOT NULL DEFAULT 1,
    CreatedAt                DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt                DATETIME2 NULL,
    CONSTRAINT FK_GardenPlanting_Household FOREIGN KEY (HouseholdId) REFERENCES Household(Id) ON DELETE CASCADE
);
CREATE INDEX IX_GardenPlanting_HouseholdId     ON GardenPlanting(HouseholdId);
CREATE INDEX IX_GardenPlanting_ExpectedRipeDate ON GardenPlanting(ExpectedRipeDate);
GO

CREATE TABLE GardenHarvest (
    Id                UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PlantingId        UNIQUEIDENTIFIER NOT NULL,
    HarvestDate       DATE NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
    QuantityHarvested DECIMAL(10,3) NOT NULL,
    Unit              NVARCHAR(50) NOT NULL,
    AddedToInventory  BIT NOT NULL DEFAULT 0,
    InventoryItemId   UNIQUEIDENTIFIER NULL,
    Notes             NVARCHAR(MAX) NULL,
    CreatedAt         DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_GardenHarvest_Planting FOREIGN KEY (PlantingId) REFERENCES GardenPlanting(Id) ON DELETE CASCADE
);
GO
