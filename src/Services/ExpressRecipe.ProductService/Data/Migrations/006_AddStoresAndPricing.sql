-- Migration: 006_AddStoresAndPricing
-- Description: Add store locations, pricing, and coupon management
-- Date: 2024-11-19

-- Store: Retail chains and individual store locations
CREATE TABLE Store (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ChainName NVARCHAR(200) NOT NULL, -- Walmart, Target, Kroger, etc.
    StoreName NVARCHAR(200) NULL, -- Specific store name if different from chain
    StoreNumber NVARCHAR(50) NULL,
    Address NVARCHAR(500) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(50) NULL,
    ZipCode NVARCHAR(20) NULL,
    Country NVARCHAR(100) NULL DEFAULT 'USA',
    Latitude DECIMAL(10, 7) NULL,
    Longitude DECIMAL(10, 7) NULL,
    Phone NVARCHAR(20) NULL,
    Email NVARCHAR(200) NULL,
    Website NVARCHAR(500) NULL,
    StoreHours NVARCHAR(MAX) NULL, -- JSON format for hours by day
    HasPharmacy BIT NOT NULL DEFAULT 0,
    HasDeli BIT NOT NULL DEFAULT 0,
    HasBakery BIT NOT NULL DEFAULT 0,
    AcceptsManufacturerCoupons BIT NOT NULL DEFAULT 1,
    AllowsCouponDoubling BIT NOT NULL DEFAULT 0,
    CouponDoublingLimit DECIMAL(10, 2) NULL, -- Max value for doubling (e.g., $0.50)
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION
);

CREATE INDEX IX_Store_ChainName ON Store(ChainName);
CREATE INDEX IX_Store_City_State ON Store(City, State);
CREATE INDEX IX_Store_ZipCode ON Store(ZipCode);
CREATE INDEX IX_Store_StoreNumber ON Store(StoreNumber);
GO

-- ProductStorePrice: Product pricing at specific stores (user-submitted)
CREATE TABLE ProductStorePrice (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    StoreId UNIQUEIDENTIFIER NOT NULL,
    Price DECIMAL(10, 2) NOT NULL,
    SalePrice DECIMAL(10, 2) NULL,
    SaleStartDate DATETIME2 NULL,
    SaleEndDate DATETIME2 NULL,
    UnitSize NVARCHAR(100) NULL, -- "16 oz", "1 lb", "500g", etc.
    PricePerUnit DECIMAL(10, 2) NULL, -- Calculated price per standard unit
    StandardUnit NVARCHAR(50) NULL, -- "oz", "lb", "g", "kg", "each"
    IsOnClearance BIT NOT NULL DEFAULT 0,
    InStock BIT NOT NULL DEFAULT 1,
    SubmittedBy UNIQUEIDENTIFIER NOT NULL, -- User who reported this price
    VerifiedBy UNIQUEIDENTIFIER NULL, -- Admin or other user who verified
    VerifiedAt DATETIME2 NULL,
    VerificationCount INT NOT NULL DEFAULT 0, -- How many users confirmed this price
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CONSTRAINT FK_ProductStorePrice_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ProductStorePrice_Store FOREIGN KEY (StoreId)
        REFERENCES Store(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ProductStorePrice_ProductId ON ProductStorePrice(ProductId);
CREATE INDEX IX_ProductStorePrice_StoreId ON ProductStorePrice(StoreId);
CREATE INDEX IX_ProductStorePrice_SaleEndDate ON ProductStorePrice(SaleEndDate);
GO

-- Coupon: Digital and paper coupons
CREATE TABLE Coupon (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Code NVARCHAR(100) NULL, -- Digital coupon code
    Description NVARCHAR(500) NOT NULL,
    CouponType NVARCHAR(50) NOT NULL, -- Manufacturer, Store, Digital, Printable, Mail-In
    DiscountType NVARCHAR(50) NOT NULL, -- FixedAmount, Percentage, BOGO, BuyXGetY
    DiscountAmount DECIMAL(10, 2) NULL, -- $1.00 off, or percentage value
    MinimumPurchaseAmount DECIMAL(10, 2) NULL,
    MinimumQuantity INT NULL,
    MaximumQuantity INT NULL, -- Purchase limit per transaction
    MaxUsesPerUser INT NULL DEFAULT 1,
    ProductId UNIQUEIDENTIFIER NULL, -- Specific product (if applicable)
    StoreId UNIQUEIDENTIFIER NULL, -- Specific store (if store coupon)
    ManufacturerName NVARCHAR(200) NULL,
    ImageUrl NVARCHAR(500) NULL,
    SourceUrl NVARCHAR(500) NULL,
    CanBeDoubled BIT NOT NULL DEFAULT 0,
    CanBeCombined BIT NOT NULL DEFAULT 1, -- Can combine with other coupons
    RequiresLoyaltyCard BIT NOT NULL DEFAULT 0,
    StartDate DATETIME2 NULL,
    ExpirationDate DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    SubmittedBy UNIQUEIDENTIFIER NOT NULL,
    IsApproved BIT NOT NULL DEFAULT 0,
    ApprovedBy UNIQUEIDENTIFIER NULL,
    ApprovedAt DATETIME2 NULL,
    RejectionReason NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CONSTRAINT FK_Coupon_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE SET NULL,
    CONSTRAINT FK_Coupon_Store FOREIGN KEY (StoreId)
        REFERENCES Store(Id) ON DELETE SET NULL
);

CREATE INDEX IX_Coupon_ProductId ON Coupon(ProductId);
CREATE INDEX IX_Coupon_StoreId ON Coupon(StoreId);
CREATE INDEX IX_Coupon_ExpirationDate ON Coupon(ExpirationDate);
CREATE INDEX IX_Coupon_Code ON Coupon(Code);
GO

-- UserCoupon: Tracks user's saved/clipped coupons and usage
CREATE TABLE UserCoupon (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    CouponId UNIQUEIDENTIFIER NOT NULL,
    ClippedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UsedAt DATETIME2 NULL,
    UsedAtStoreId UNIQUEIDENTIFIER NULL,
    SavedAmount DECIMAL(10, 2) NULL, -- Actual savings when used
    Notes NVARCHAR(500) NULL,
    CONSTRAINT FK_UserCoupon_Coupon FOREIGN KEY (CouponId)
        REFERENCES Coupon(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserCoupon_Store FOREIGN KEY (UsedAtStoreId)
        REFERENCES Store(Id) ON DELETE SET NULL,
    CONSTRAINT UQ_UserCoupon_User_Coupon UNIQUE (UserId, CouponId)
);

CREATE INDEX IX_UserCoupon_UserId ON UserCoupon(UserId);
CREATE INDEX IX_UserCoupon_CouponId ON UserCoupon(CouponId);
GO

-- UserFavoriteStore: User's preferred stores
CREATE TABLE UserFavoriteStore (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    StoreId UNIQUEIDENTIFIER NOT NULL,
    IsPrimary BIT NOT NULL DEFAULT 0, -- User's primary shopping store
    Nickname NVARCHAR(100) NULL, -- User's custom name for this store
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserFavoriteStore_Store FOREIGN KEY (StoreId)
        REFERENCES Store(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserFavoriteStore_User_Store UNIQUE (UserId, StoreId)
);

CREATE INDEX IX_UserFavoriteStore_UserId ON UserFavoriteStore(UserId);
CREATE INDEX IX_UserFavoriteStore_StoreId ON UserFavoriteStore(StoreId);
GO

-- PriceVerification: Track price verification by users (crowdsourced accuracy)
CREATE TABLE PriceVerification (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductStorePriceId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    IsAccurate BIT NOT NULL, -- True if price is accurate, false if incorrect
    ReportedPrice DECIMAL(10, 2) NULL, -- If inaccurate, what's the correct price?
    VerifiedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PriceVerification_ProductStorePrice FOREIGN KEY (ProductStorePriceId)
        REFERENCES ProductStorePrice(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_PriceVerification_Price_User UNIQUE (ProductStorePriceId, UserId)
);

CREATE INDEX IX_PriceVerification_ProductStorePriceId ON PriceVerification(ProductStorePriceId);
CREATE INDEX IX_PriceVerification_UserId ON PriceVerification(UserId);
GO
