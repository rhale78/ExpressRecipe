-- Alter ProductStaging column sizes to accommodate longer data from OpenFoodFacts
-- GenericName often contains full ingredient lists in the source data

-- GenericName: Increase from NVARCHAR(500) to NVARCHAR(MAX)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'GenericName')
BEGIN
    ALTER TABLE ProductStaging ALTER COLUMN GenericName NVARCHAR(MAX) NULL;
END
GO

-- ProductName: Increase from NVARCHAR(500) to NVARCHAR(MAX) to be safe
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'ProductName')
BEGIN
    ALTER TABLE ProductStaging ALTER COLUMN ProductName NVARCHAR(MAX) NULL;
END
GO

-- Brands: Increase from NVARCHAR(500) to NVARCHAR(MAX) to handle multiple brands
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'Brands')
BEGIN
    ALTER TABLE ProductStaging ALTER COLUMN Brands NVARCHAR(MAX) NULL;
END
GO

-- ImageUrl: Increase from NVARCHAR(500) to NVARCHAR(MAX) for long URLs
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'ImageUrl')
BEGIN
    ALTER TABLE ProductStaging ALTER COLUMN ImageUrl NVARCHAR(MAX) NULL;
END
GO

-- ImageSmallUrl: Increase from NVARCHAR(500) to NVARCHAR(MAX) for long URLs
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'ImageSmallUrl')
BEGIN
    ALTER TABLE ProductStaging ALTER COLUMN ImageSmallUrl NVARCHAR(MAX) NULL;
END
GO

-- Countries: Increase from NVARCHAR(200) to NVARCHAR(MAX) for many countries
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'ProductStaging' AND COLUMN_NAME = 'Countries')
BEGIN
    ALTER TABLE ProductStaging ALTER COLUMN Countries NVARCHAR(MAX) NULL;
END
GO
