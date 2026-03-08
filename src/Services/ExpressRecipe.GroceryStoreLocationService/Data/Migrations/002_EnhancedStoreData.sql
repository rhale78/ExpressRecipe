-- Migration 002: Enhanced Store Data
-- Extends GroceryStore with cross-source IDs, service flags, verification, and chain normalization.
-- Adds StoreHours (structured hours) and StoreChain (canonical chain lookup) tables.

-- Extend GroceryStore with new columns
ALTER TABLE GroceryStore
    ADD OsmId             BIGINT NULL,
        GersId            NVARCHAR(100) NULL,
        SnapStoreId       NVARCHAR(50) NULL,
        HifldId           NVARCHAR(50) NULL,
        IsOnline          BIT NOT NULL DEFAULT 0,
        DeliveryAvailable BIT NOT NULL DEFAULT 0,
        PickupAvailable   BIT NOT NULL DEFAULT 0,
        BaseDeliveryFee   DECIMAL(10,2) NULL,
        FreeDeliveryMin   DECIMAL(10,2) NULL,
        AvgDeliveryDays   DECIMAL(4,1) NULL,
        IsVerified        BIT NOT NULL DEFAULT 0,
        VerifiedAt        DATETIME2 NULL,
        VerifiedSource    NVARCHAR(100) NULL,
        NormalizedChain   NVARCHAR(200) NULL;
GO

CREATE UNIQUE INDEX IX_GroceryStore_OsmId  ON GroceryStore(OsmId)  WHERE OsmId IS NOT NULL;
GO

CREATE UNIQUE INDEX IX_GroceryStore_GersId ON GroceryStore(GersId) WHERE GersId IS NOT NULL;
GO

CREATE UNIQUE INDEX IX_GroceryStore_SnapId ON GroceryStore(SnapStoreId) WHERE SnapStoreId IS NOT NULL;
GO

CREATE INDEX IX_GroceryStore_NormalizedChain ON GroceryStore(NormalizedChain) WHERE NormalizedChain IS NOT NULL;
GO

-- Structured hours (replaces free-text OpeningHours)
CREATE TABLE StoreHours (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    StoreId     UNIQUEIDENTIFIER NOT NULL,
    DayOfWeek   TINYINT NOT NULL,       -- 0=Sunday ... 6=Saturday
    OpenTime    TIME NULL,
    CloseTime   TIME NULL,
    IsClosed    BIT NOT NULL DEFAULT 0,
    IsHoliday   BIT NOT NULL DEFAULT 0,
    HolidayDate DATE NULL,
    CONSTRAINT FK_StoreHours_Store FOREIGN KEY (StoreId) REFERENCES GroceryStore(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_StoreHours UNIQUE (StoreId, DayOfWeek)
);
GO

CREATE INDEX IX_StoreHours_StoreId ON StoreHours(StoreId);
GO

-- Chain canonical lookup (dedup across sources)
CREATE TABLE StoreChain (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CanonicalName NVARCHAR(200) NOT NULL,
    Aliases       NVARCHAR(MAX) NULL,
    LogoUrl       NVARCHAR(500) NULL,
    Website       NVARCHAR(500) NULL,
    IsNational    BIT NOT NULL DEFAULT 0,
    IsOnlineOnly  BIT NOT NULL DEFAULT 0,
    CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_StoreChain_CanonicalName UNIQUE (CanonicalName)
);
GO

-- Seed StoreChain with common chains and their known aliases
INSERT INTO StoreChain (CanonicalName, Aliases, IsNational) VALUES
('Walmart',       '["WAL-MART","Walmart Supercenter","Walmart Neighborhood Market","WALMART","Walmart Superctr"]', 1),
('Kroger',        '["KROGER","Kroger Food & Drug","Kroger Marketplace","KROGER FOOD & DRUG"]', 1),
('Publix',        '["PUBLIX","Publix Super Markets","PUBLIX SUPER MARKETS"]', 1),
('Target',        '["TARGET","Target Grocery","TARGET GROCERY"]', 1),
('Costco',        '["COSTCO","Costco Wholesale","COSTCO WHOLESALE"]', 1),
('Aldi',          '["ALDI","ALDI Foods","ALDI INC"]', 1),
('Whole Foods',   '["Whole Foods Market","Amazon Fresh","WHOLE FOODS","WHOLE FOODS MARKET"]', 1),
('Food Lion',     '["FOOD LION","Food Lion LLC"]', 1),
('Safeway',       '["SAFEWAY","Safeway Inc","SAFEWAY INC"]', 1),
('Trader Joe''s', '["TRADER JOES","Trader Joe''s","TRADER JOE''S"]', 1),
('Sam''s Club',   '["SAMS CLUB","SAM''S CLUB","Sams Club"]', 1);
GO
