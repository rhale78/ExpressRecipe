-- Diagnostic script to check ProductImage table status
-- Run this to see if migration 011 has been applied

-- Check if ProductImage table exists
IF OBJECT_ID('ProductImage', 'U') IS NOT NULL
    PRINT '? ProductImage table EXISTS'
ELSE
    PRINT '? ProductImage table DOES NOT EXIST - Migration 011 needs to be applied'
GO

-- Check which migrations have been applied
IF OBJECT_ID('__MigrationHistory', 'U') IS NOT NULL
BEGIN
    PRINT ''
    PRINT 'Applied migrations:'
    SELECT MigrationId, AppliedAt 
    FROM __MigrationHistory 
    ORDER BY AppliedAt
END
ELSE
BEGIN
    PRINT '? __MigrationHistory table does not exist'
END
GO

-- If ProductImage exists, check row count
IF OBJECT_ID('ProductImage', 'U') IS NOT NULL
BEGIN
    DECLARE @count INT
    SELECT @count = COUNT(*) FROM ProductImage WHERE IsDeleted = 0
    PRINT ''
    PRINT 'ProductImage table row count: ' + CAST(@count AS VARCHAR(10))
    
    IF @count > 0
    BEGIN
        PRINT ''
        PRINT 'Sample images:'
        SELECT TOP 5 
            Id,
            ProductId,
            ImageType,
            LEFT(ImageUrl, 50) AS ImageUrl_Preview,
            IsPrimary,
            SourceSystem,
            CreatedAt
        FROM ProductImage
        WHERE IsDeleted = 0
        ORDER BY CreatedAt DESC
    END
END
GO

-- Check if any products have ImageUrl set
IF OBJECT_ID('Product', 'U') IS NOT NULL
BEGIN
    DECLARE @productCount INT
    DECLARE @productWithImageCount INT
    
    SELECT @productCount = COUNT(*) FROM Product WHERE IsDeleted = 0
    SELECT @productWithImageCount = COUNT(*) FROM Product WHERE IsDeleted = 0 AND ImageUrl IS NOT NULL AND ImageUrl <> ''
    
    PRINT ''
    PRINT 'Total products: ' + CAST(@productCount AS VARCHAR(10))
    PRINT 'Products with ImageUrl: ' + CAST(@productWithImageCount AS VARCHAR(10))
END
GO
