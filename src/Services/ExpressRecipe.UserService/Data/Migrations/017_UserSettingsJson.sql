-- Migration: 017_UserSettingsJson
-- Description: Add DisplaySettingsJson column to UserProfile and create UserSettingsSchema table
--              for schema-driven settings management.
-- Date: 2026-03-09

-- Add DisplaySettingsJson column to UserProfile if it doesn't already exist
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('UserProfile') AND name = 'DisplaySettingsJson'
)
BEGIN
    ALTER TABLE UserProfile ADD DisplaySettingsJson NVARCHAR(MAX) NULL;
END
GO

-- Create UserSettingsSchema table for storing settings group schemas
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('UserSettingsSchema') AND type = 'U')
BEGIN
    CREATE TABLE UserSettingsSchema (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        GroupName   NVARCHAR(100)    NOT NULL,
        SchemaJson  NVARCHAR(MAX)    NOT NULL,
        UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_UserSettingsSchema_Group UNIQUE (GroupName)
    );
END
GO

-- Seed display settings schema (upsert so re-running migration is safe)
IF NOT EXISTS (SELECT 1 FROM UserSettingsSchema WHERE GroupName = 'display')
BEGIN
    INSERT INTO UserSettingsSchema (Id, GroupName, SchemaJson)
    VALUES (
        NEWID(),
        'display',
        N'{
          "group":"display","label":"Display Preferences",
          "settings":[
            {"key":"numberFormat","label":"Number Format","type":"select","default":"Fraction",
             "options":[{"value":"Fraction","label":"Fractions (1\u00bd)"},{"value":"Decimal","label":"Decimals (1.5)"}]},
            {"key":"unitSystem","label":"Unit System","type":"select","default":"US",
             "options":[{"value":"US","label":"US"},{"value":"Metric","label":"Metric"},{"value":"UK","label":"UK"}]}
          ]
        }'
    );
END
GO
