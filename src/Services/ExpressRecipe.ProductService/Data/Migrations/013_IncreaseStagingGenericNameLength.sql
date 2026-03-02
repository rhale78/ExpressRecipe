-- Increase length of GenericName in ProductStaging to handle longer values from OpenFoodFacts
ALTER TABLE ProductStaging ALTER COLUMN GenericName NVARCHAR(MAX) NULL;
GO
