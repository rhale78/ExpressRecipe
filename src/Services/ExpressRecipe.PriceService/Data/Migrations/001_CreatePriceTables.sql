-- Migration: 001_CreatePriceTables
-- Description: Create price tracking and deal management tables
-- Date: 2024-11-19

CREATE TABLE Store (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Chain NVARCHAR(200) NULL,
    Address NVARCHAR(500) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(50) NULL,
    ZipCode NVARCHAR(20) NULL,
    Latitude DECIMAL(10, 7) NULL,
    Longitude DECIMAL(10, 7) NULL,
    PhoneNumber NVARCHAR(50) NULL,
    Website NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_Store_City_State ON Store(City, State);
CREATE INDEX IX_Store_ZipCode ON Store(ZipCode);
GO

CREATE TABLE PriceObservation (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    StoreId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL, -- Who reported
    Price DECIMAL(10, 2) NOT NULL,
    SalePrice DECIMAL(10, 2) NULL,
    IsOnSale BIT NOT NULL DEFAULT 0,
    Unit NVARCHAR(50) NULL,
    Quantity DECIMAL(10, 2) NULL,
    ObservedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsVerified BIT NOT NULL DEFAULT 0,
    VerificationCount INT NOT NULL DEFAULT 0,
    
    CONSTRAINT FK_PriceObservation_Store FOREIGN KEY (StoreId)
        REFERENCES Store(Id) ON DELETE CASCADE
);

CREATE INDEX IX_PriceObservation_ProductId ON PriceObservation(ProductId);
CREATE INDEX IX_PriceObservation_StoreId ON PriceObservation(StoreId);
CREATE INDEX IX_PriceObservation_ObservedAt ON PriceObservation(ObservedAt);
GO

CREATE TABLE Deal (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    StoreId UNIQUEIDENTIFIER NOT NULL,
    Title NVARCHAR(300) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    RegularPrice DECIMAL(10, 2) NOT NULL,
    DealPrice DECIMAL(10, 2) NOT NULL,
    DiscountPercentage DECIMAL(5, 2) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    DealType NVARCHAR(50) NOT NULL, -- Sale, Coupon, BOGO, Clearance
    RequiresMemberCard BIT NOT NULL DEFAULT 0,
    SubmittedBy UNIQUEIDENTIFIER NOT NULL,
    SubmittedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    
    CONSTRAINT FK_Deal_Store FOREIGN KEY (StoreId)
        REFERENCES Store(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Deal_ProductId ON Deal(ProductId);
CREATE INDEX IX_Deal_StoreId ON Deal(StoreId);
CREATE INDEX IX_Deal_EndDate ON Deal(EndDate);
GO

CREATE TABLE PricePrediction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    StoreId UNIQUEIDENTIFIER NOT NULL,
    PredictedLowPrice DECIMAL(10, 2) NOT NULL,
    PredictedHighPrice DECIMAL(10, 2) NOT NULL,
    AveragePrice DECIMAL(10, 2) NOT NULL,
    BestDayToBuy NVARCHAR(20) NULL,
    ConfidenceScore DECIMAL(5, 4) NOT NULL,
    CalculatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    BasedOnObservations INT NOT NULL
);

CREATE INDEX IX_PricePrediction_ProductId_StoreId ON PricePrediction(ProductId, StoreId);
GO
