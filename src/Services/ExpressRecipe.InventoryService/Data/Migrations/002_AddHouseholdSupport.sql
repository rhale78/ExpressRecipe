-- Migration: 002_AddHouseholdSupport
-- Description: Add household/family support with multi-location addresses
-- Date: 2026-02-16

-- Household: Family/group management
CREATE TABLE Household (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE INDEX IX_Household_CreatedBy ON Household(CreatedBy);
GO

-- HouseholdMember: Members of a household
CREATE TABLE HouseholdMember (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Member', -- Owner, Admin, Member, Viewer
    CanManageInventory BIT NOT NULL DEFAULT 1,
    CanManageShopping BIT NOT NULL DEFAULT 1,
    CanManageMembers BIT NOT NULL DEFAULT 0,
    JoinedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    InvitedBy UNIQUEIDENTIFIER NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    
    CONSTRAINT FK_HouseholdMember_Household FOREIGN KEY (HouseholdId)
        REFERENCES Household(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_HouseholdMember_User UNIQUE (HouseholdId, UserId)
);

CREATE INDEX IX_HouseholdMember_UserId ON HouseholdMember(UserId);
CREATE INDEX IX_HouseholdMember_HouseholdId ON HouseholdMember(HouseholdId);
GO

-- Address: Physical addresses for households
CREATE TABLE Address (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(100) NOT NULL, -- Home, Vacation Home, Office, etc.
    Street NVARCHAR(200) NOT NULL,
    City NVARCHAR(100) NOT NULL,
    State NVARCHAR(50) NOT NULL,
    ZipCode NVARCHAR(20) NOT NULL,
    Country NVARCHAR(100) NOT NULL DEFAULT 'USA',
    Latitude DECIMAL(10, 7) NULL,
    Longitude DECIMAL(10, 7) NULL,
    IsPrimary BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    
    CONSTRAINT FK_Address_Household FOREIGN KEY (HouseholdId)
        REFERENCES Household(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Address_HouseholdId ON Address(HouseholdId);
CREATE INDEX IX_Address_Location ON Address(Latitude, Longitude) WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL;
GO

-- Update StorageLocation to support addresses
ALTER TABLE StorageLocation ADD HouseholdId UNIQUEIDENTIFIER NULL;
ALTER TABLE StorageLocation ADD AddressId UNIQUEIDENTIFIER NULL;
ALTER TABLE StorageLocation ADD 
    CONSTRAINT FK_StorageLocation_Household FOREIGN KEY (HouseholdId)
        REFERENCES Household(Id);
ALTER TABLE StorageLocation ADD
    CONSTRAINT FK_StorageLocation_Address FOREIGN KEY (AddressId)
        REFERENCES Address(Id);

GO

CREATE INDEX IX_StorageLocation_HouseholdId ON StorageLocation(HouseholdId);
CREATE INDEX IX_StorageLocation_AddressId ON StorageLocation(AddressId);
GO

-- Update InventoryItem to support household sharing
ALTER TABLE InventoryItem ADD HouseholdId UNIQUEIDENTIFIER NULL;
ALTER TABLE InventoryItem ADD AddedBy UNIQUEIDENTIFIER NULL;
ALTER TABLE InventoryItem ADD PreferredStore NVARCHAR(200) NULL;
ALTER TABLE InventoryItem ADD StoreLocation NVARCHAR(500) NULL; -- Store address for easy reorder
ALTER TABLE InventoryItem ADD 
    CONSTRAINT FK_InventoryItem_Household FOREIGN KEY (HouseholdId)
        REFERENCES Household(Id);

GO

CREATE INDEX IX_InventoryItem_HouseholdId ON InventoryItem(HouseholdId);
CREATE INDEX IX_InventoryItem_AddedBy ON InventoryItem(AddedBy);
GO

-- Update InventoryHistory to track who made changes and disposal reasons
ALTER TABLE InventoryHistory ADD ChangedBy UNIQUEIDENTIFIER NULL;
ALTER TABLE InventoryHistory ADD DisposalReason NVARCHAR(100) NULL; -- Bad, CausedAllergy, Expired, Other
ALTER TABLE InventoryHistory ADD AllergenDetected NVARCHAR(500) NULL; -- Comma-separated list of allergens

GO

CREATE INDEX IX_InventoryHistory_ChangedBy ON InventoryHistory(ChangedBy);
CREATE INDEX IX_InventoryHistory_DisposalReason ON InventoryHistory(DisposalReason) WHERE DisposalReason IS NOT NULL;
GO

-- AllergenDiscovery: Track allergens discovered from disposed items
CREATE TABLE AllergenDiscovery (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    HouseholdId UNIQUEIDENTIFIER NULL,
    InventoryHistoryId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NULL,
    AllergenName NVARCHAR(200) NOT NULL,
    Severity NVARCHAR(50) NOT NULL DEFAULT 'Unknown', -- Mild, Moderate, Severe, Unknown
    AddedToProfile BIT NOT NULL DEFAULT 0,
    AddedToProfileAt DATETIME2 NULL,
    Notes NVARCHAR(MAX) NULL,
    DiscoveredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_AllergenDiscovery_InventoryHistory FOREIGN KEY (InventoryHistoryId)
        REFERENCES InventoryHistory(Id),
    CONSTRAINT FK_AllergenDiscovery_Household FOREIGN KEY (HouseholdId)
        REFERENCES Household(Id)
);

CREATE INDEX IX_AllergenDiscovery_UserId ON AllergenDiscovery(UserId);
CREATE INDEX IX_AllergenDiscovery_HouseholdId ON AllergenDiscovery(HouseholdId);
CREATE INDEX IX_AllergenDiscovery_ProductId ON AllergenDiscovery(ProductId);
GO

-- InventoryScanSession: Track scanning sessions for lock mode
CREATE TABLE InventoryScanSession (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    HouseholdId UNIQUEIDENTIFIER NULL,
    SessionType NVARCHAR(50) NOT NULL, -- Adding, Using, Disposing, Purchasing
    StorageLocationId UNIQUEIDENTIFIER NULL,
    StartedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    EndedAt DATETIME2 NULL,
    ItemsScanned INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    
    CONSTRAINT FK_InventoryScanSession_Household FOREIGN KEY (HouseholdId)
        REFERENCES Household(Id),
    CONSTRAINT FK_InventoryScanSession_StorageLocation FOREIGN KEY (StorageLocationId)
        REFERENCES StorageLocation(Id)
);

CREATE INDEX IX_InventoryScanSession_UserId ON InventoryScanSession(UserId);
CREATE INDEX IX_InventoryScanSession_IsActive ON InventoryScanSession(IsActive) WHERE IsActive = 1;
GO
