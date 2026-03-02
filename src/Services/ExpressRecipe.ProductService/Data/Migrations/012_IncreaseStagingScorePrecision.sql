-- Increase length of NutriScore and EcoScore in ProductStaging to handle longer values like 'not-applicable'
ALTER TABLE ProductStaging ALTER COLUMN NutriScore NVARCHAR(20) NULL;
ALTER TABLE ProductStaging ALTER COLUMN EcoScore NVARCHAR(20) NULL;
GO
