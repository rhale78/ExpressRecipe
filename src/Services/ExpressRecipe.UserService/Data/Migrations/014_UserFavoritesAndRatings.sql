-- Migration: 014_UserFavoritesAndRatings
-- Description: Create UserProductRating, UserFavoriteRecipe, and UserFavoriteProduct tables
-- Date: 2026-03-03

-- UserProductRating: Stores user ratings and reviews for products
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('UserProductRating') AND type = 'U')
BEGIN
    CREATE TABLE UserProductRating (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        ProductId UNIQUEIDENTIFIER NOT NULL, -- References ProductService.Product.Id
        Rating INT NOT NULL,
        ReviewText NVARCHAR(2000) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        CONSTRAINT CK_UserProductRating_Rating CHECK (Rating BETWEEN 1 AND 5),
        CONSTRAINT UQ_UserProductRating_User_Product UNIQUE (UserId, ProductId)
    );

    CREATE INDEX IX_UserProductRating_UserId ON UserProductRating(UserId) WHERE IsDeleted = 0;
    CREATE INDEX IX_UserProductRating_ProductId ON UserProductRating(ProductId) WHERE IsDeleted = 0;
END
GO

-- UserFavoriteRecipe: Stores user's favorite recipe selections
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('UserFavoriteRecipe') AND type = 'U')
BEGIN
    CREATE TABLE UserFavoriteRecipe (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        RecipeId UNIQUEIDENTIFIER NOT NULL, -- References RecipeService.Recipe.Id
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_UserFavoriteRecipe_User_Recipe UNIQUE (UserId, RecipeId)
    );

    CREATE INDEX IX_UserFavoriteRecipe_UserId ON UserFavoriteRecipe(UserId) WHERE IsDeleted = 0;
    CREATE INDEX IX_UserFavoriteRecipe_RecipeId ON UserFavoriteRecipe(RecipeId) WHERE IsDeleted = 0;
END
GO

-- UserFavoriteProduct: Stores user's favorite product selections
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('UserFavoriteProduct') AND type = 'U')
BEGIN
    CREATE TABLE UserFavoriteProduct (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        ProductId UNIQUEIDENTIFIER NOT NULL, -- References ProductService.Product.Id
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_UserFavoriteProduct_User_Product UNIQUE (UserId, ProductId)
    );

    CREATE INDEX IX_UserFavoriteProduct_UserId ON UserFavoriteProduct(UserId) WHERE IsDeleted = 0;
    CREATE INDEX IX_UserFavoriteProduct_ProductId ON UserFavoriteProduct(ProductId) WHERE IsDeleted = 0;
END
GO
