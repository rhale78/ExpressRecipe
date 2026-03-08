-- Migration: 004_PriceHistoryAndEnhancedDeals
-- Description: Append-only price history, enhanced deal columns, store-product linking, online store metadata

-- Append-only historical price rows (never upsert, always insert)
CREATE TABLE PriceHistory (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId        UNIQUEIDENTIFIER NOT NULL,
    Upc              NVARCHAR(100) NULL,
    ProductName      NVARCHAR(300) NOT NULL,
    StoreId          UNIQUEIDENTIFIER NULL,
    StoreName        NVARCHAR(200) NULL,
    StoreChain       NVARCHAR(200) NULL,
    IsOnline         BIT NOT NULL DEFAULT 0,
    BasePrice        DECIMAL(10,4) NOT NULL,
    FinalPrice       DECIMAL(10,4) NOT NULL,
    Currency         NVARCHAR(10) NOT NULL DEFAULT 'USD',
    Unit             NVARCHAR(50) NULL,
    Quantity         DECIMAL(10,4) NULL,
    PricePerOz       DECIMAL(10,6) NULL,
    PricePerHundredG DECIMAL(10,6) NULL,
    DataSource       NVARCHAR(100) NOT NULL,
    ExternalId       NVARCHAR(200) NULL,
    ObservedAt       DATETIME2 NOT NULL,
    ImportedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT CK_PriceHistory_Prices CHECK (BasePrice >= 0 AND FinalPrice >= 0 AND FinalPrice <= BasePrice)
);
CREATE INDEX IX_PriceHistory_ProductId_Observed ON PriceHistory(ProductId, ObservedAt DESC);
CREATE INDEX IX_PriceHistory_Upc_Observed        ON PriceHistory(Upc, ObservedAt DESC);
CREATE INDEX IX_PriceHistory_StoreId_Observed    ON PriceHistory(StoreId, ObservedAt DESC);
CREATE INDEX IX_PriceHistory_Source              ON PriceHistory(DataSource, ExternalId);
GO

-- Extend Deal table with enhanced discount fields (existing data safe)
ALTER TABLE Deal
    ADD DiscountType       NVARCHAR(50) NULL,
        BuyQuantity        INT NULL,
        GetQuantity        INT NULL,
        GetPercentOff      DECIMAL(5,2) NULL,
        CouponCode         NVARCHAR(100) NULL,
        RebateAmount       DECIMAL(10,2) NULL,
        FlyerSource        NVARCHAR(100) NULL,
        FlyerPageRef       NVARCHAR(200) NULL,
        IsDigital          BIT NOT NULL DEFAULT 0,
        IsStackable        BIT NOT NULL DEFAULT 0,
        MaxPerTransaction  INT NULL;
GO

-- Store inventory link — which stores carry which products
CREATE TABLE StoreProductLink (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    StoreId         UNIQUEIDENTIFIER NOT NULL,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    Upc             NVARCHAR(100) NULL,
    IsInStock       BIT NOT NULL DEFAULT 1,
    Aisle           NVARCHAR(50) NULL,
    LastSeenAt      DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastPriceId     UNIQUEIDENTIFIER NULL,
    DataSource      NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_StoreProductLink UNIQUE (StoreId, ProductId)
);
CREATE INDEX IX_StoreProductLink_StoreId    ON StoreProductLink(StoreId);
CREATE INDEX IX_StoreProductLink_ProductId  ON StoreProductLink(ProductId);
CREATE INDEX IX_StoreProductLink_Upc        ON StoreProductLink(Upc);
GO

-- Online store metadata (extends Store)
ALTER TABLE Store
    ADD IsOnline           BIT NOT NULL DEFAULT 0,
        BaseDeliveryFee    DECIMAL(10,2) NULL,
        FreeDeliveryMin    DECIMAL(10,2) NULL,
        AvgDeliveryDays    DECIMAL(4,1) NULL,
        ShippingNotes      NVARCHAR(500) NULL;
GO
