-- Migration: 002_UpdateRecallProductLotNumber
-- Description: Expand RecallProduct.LotNumber to NVARCHAR(MAX) to avoid truncation
-- Date: 2025-01-01

IF OBJECT_ID(N'dbo.RecallProduct', N'U') IS NOT NULL
BEGIN
    -- Only alter when LotNumber is not already NVARCHAR(MAX)
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.RecallProduct')
          AND c.name = N'LotNumber'
          AND t.name = N'nvarchar'
          AND c.max_length <> -1 -- -1 means MAX for nvarchar
    )
    BEGIN
        ALTER TABLE dbo.RecallProduct ALTER COLUMN LotNumber NVARCHAR(MAX) NULL;
    END
END
GO
