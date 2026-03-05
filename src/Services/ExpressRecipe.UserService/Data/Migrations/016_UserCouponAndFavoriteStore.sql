-- Migration: 016_UserCouponAndFavoriteStore
-- Description: Add UserFavoriteStore and UserCoupon to UserService.
--   These tables track user-specific preferences and activity.
--   They reference external IDs (StoreId from GroceryStoreLocationService,
--   CouponId from ProductService) without cross-DB foreign key constraints,
--   as per the microservice ownership principle.
-- Date: 2026-03-05

-- UserFavoriteStore: A user's list of preferred grocery stores
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('UserFavoriteStore') AND type = 'U')
BEGIN
    CREATE TABLE UserFavoriteStore (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        UserId      UNIQUEIDENTIFIER NOT NULL,
        StoreId     UNIQUEIDENTIFIER NOT NULL, -- References GroceryStoreLocationService.GroceryStore.Id (external key, no FK)
        IsPrimary   BIT NOT NULL DEFAULT 0,    -- User's primary shopping store
        Nickname    NVARCHAR(100) NULL,         -- User's custom name for this store
        Notes       NVARCHAR(500) NULL,
        CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt   DATETIME2 NULL,
        IsDeleted   BIT NOT NULL DEFAULT 0,
        DeletedAt   DATETIME2 NULL,
        RowVersion  ROWVERSION,
        CONSTRAINT CK_UserFavoriteStore_UserId  CHECK (UserId  <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT CK_UserFavoriteStore_StoreId CHECK (StoreId <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT UQ_UserFavoriteStore_User_Store UNIQUE (UserId, StoreId)
    );

    CREATE INDEX IX_UserFavoriteStore_UserId  ON UserFavoriteStore(UserId)  WHERE IsDeleted = 0;
    CREATE INDEX IX_UserFavoriteStore_StoreId ON UserFavoriteStore(StoreId) WHERE IsDeleted = 0;

    PRINT 'Created UserFavoriteStore table';
END
ELSE
BEGIN
    PRINT 'UserFavoriteStore table already exists – skipping';
END
GO

-- UserCoupon: Tracks coupons a user has clipped/saved and their usage
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('UserCoupon') AND type = 'U')
BEGIN
    CREATE TABLE UserCoupon (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        UserId          UNIQUEIDENTIFIER NOT NULL,
        CouponId        UNIQUEIDENTIFIER NOT NULL, -- References ProductService.Coupon.Id (external key, no FK)
        ClippedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsedAt          DATETIME2 NULL,
        UsedAtStoreId   UNIQUEIDENTIFIER NULL,     -- References GroceryStoreLocationService.GroceryStore.Id (external key, no FK)
        SavedAmount     DECIMAL(10, 2) NULL,        -- Actual savings recorded at redemption
        Notes           NVARCHAR(500) NULL,
        IsDeleted       BIT NOT NULL DEFAULT 0,
        DeletedAt       DATETIME2 NULL,
        RowVersion      ROWVERSION,
        CONSTRAINT CK_UserCoupon_UserId   CHECK (UserId   <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT CK_UserCoupon_CouponId CHECK (CouponId <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT UQ_UserCoupon_User_Coupon UNIQUE (UserId, CouponId)
    );

    CREATE INDEX IX_UserCoupon_UserId   ON UserCoupon(UserId)   WHERE IsDeleted = 0;
    CREATE INDEX IX_UserCoupon_CouponId ON UserCoupon(CouponId) WHERE IsDeleted = 0;

    PRINT 'Created UserCoupon table';
END
ELSE
BEGIN
    PRINT 'UserCoupon table already exists – skipping';
END
GO
