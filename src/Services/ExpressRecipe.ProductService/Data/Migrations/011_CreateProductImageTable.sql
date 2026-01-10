-- Create ProductImage table for storing multiple images per product
-- Supports both external URLs (OpenFoodFacts, etc.) and uploaded files

CREATE TABLE ProductImage (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,

    -- Image source
    ImageType NVARCHAR(50) NOT NULL, -- 'Front', 'Back', 'Side', 'Nutrition', 'Ingredients', 'Other'
    ImageUrl NVARCHAR(1000) NULL, -- External URL (OpenFoodFacts, etc.)
    LocalFilePath NVARCHAR(500) NULL, -- Local server file path for uploaded images
    FileName NVARCHAR(255) NULL, -- Original filename
    FileSize BIGINT NULL, -- File size in bytes
    MimeType NVARCHAR(100) NULL, -- image/jpeg, image/png, etc.

    -- Image metadata
    Width INT NULL,
    Height INT NULL,
    DisplayOrder INT NOT NULL DEFAULT 0, -- Order for display (0 = primary)
    IsPrimary BIT NOT NULL DEFAULT 0, -- Is this the main product image?
    IsUserUploaded BIT NOT NULL DEFAULT 0, -- User uploaded vs imported

    -- Source tracking
    SourceSystem NVARCHAR(100) NULL, -- 'OpenFoodFacts', 'User', 'Admin', etc.
    SourceId NVARCHAR(200) NULL, -- External ID from source system

    -- Standard tracking
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,

    CONSTRAINT FK_ProductImage_Product FOREIGN KEY (ProductId)
        REFERENCES Product(Id) ON DELETE CASCADE,

    -- Ensure at least one of URL or LocalFilePath is provided
    CONSTRAINT CK_ProductImage_SourceRequired
        CHECK (ImageUrl IS NOT NULL OR LocalFilePath IS NOT NULL)
);
GO

-- Index for fast product image lookups
CREATE NONCLUSTERED INDEX IX_ProductImage_ProductId_DisplayOrder
    ON ProductImage(ProductId, DisplayOrder)
    INCLUDE (ImageType, ImageUrl, LocalFilePath, IsPrimary)
    WHERE IsDeleted = 0;
GO

-- Index for primary image lookups
CREATE NONCLUSTERED INDEX IX_ProductImage_Primary
    ON ProductImage(ProductId, IsPrimary)
    INCLUDE (ImageUrl, LocalFilePath, ImageType)
    WHERE IsDeleted = 0 AND IsPrimary = 1;
GO

-- Update existing Product.ImageUrl data to ProductImage table
-- Migrate any existing ImageUrl values to the new ProductImage table
INSERT INTO ProductImage (ProductId, ImageType, ImageUrl, IsPrimary, DisplayOrder, SourceSystem, CreatedAt, IsDeleted)
SELECT
    Id AS ProductId,
    'Front' AS ImageType,
    ImageUrl,
    1 AS IsPrimary,
    0 AS DisplayOrder,
    'Legacy' AS SourceSystem,
    GETUTCDATE() AS CreatedAt,
    0 AS IsDeleted
FROM Product
WHERE ImageUrl IS NOT NULL
  AND ImageUrl <> ''
  AND IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM ProductImage pi
      WHERE pi.ProductId = Product.Id
      AND pi.IsDeleted = 0
  );
GO

-- Optional: You can keep Product.ImageUrl for backward compatibility or drop it later
-- For now, we'll keep it and update it via a trigger or application logic
-- to always reflect the primary image from ProductImage table
