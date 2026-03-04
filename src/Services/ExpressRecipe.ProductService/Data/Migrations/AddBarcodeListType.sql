-- Migration script to add user-defined table type for bulk barcode lookups
-- This improves performance of batched product lookups from PriceService

-- Check if type already exists and drop it
IF EXISTS (SELECT * FROM sys.types WHERE name = 'BarcodeListType' AND is_table_type = 1)
BEGIN
    DROP TYPE dbo.BarcodeListType;
END
GO

-- Create user-defined table type for passing lists of barcodes
CREATE TYPE dbo.BarcodeListType AS TABLE
(
    Barcode NVARCHAR(50) NOT NULL
);
GO

-- Grant execute permission to allow using the type
-- GRANT EXECUTE ON TYPE::dbo.BarcodeListType TO [YourApplicationUser];
-- GO

PRINT 'BarcodeListType created successfully';
GO
