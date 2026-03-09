-- Migration: 008_PlanTemplateEnhancements
-- Description: Add category, span, household and usage tracking to PlanTemplate
-- Date: 2025-03-09

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlanTemplate') AND name = 'HouseholdId')
    ALTER TABLE PlanTemplate ADD HouseholdId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlanTemplate') AND name = 'Category')
    ALTER TABLE PlanTemplate ADD Category NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlanTemplate') AND name = 'SpanDays')
    ALTER TABLE PlanTemplate ADD SpanDays INT NOT NULL DEFAULT 7;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlanTemplate') AND name = 'Tags')
    ALTER TABLE PlanTemplate ADD Tags NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlanTemplate') AND name = 'UseCount')
    ALTER TABLE PlanTemplate ADD UseCount INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlanTemplate') AND name = 'UpdatedAt')
    ALTER TABLE PlanTemplate ADD UpdatedAt DATETIME2 NULL;
GO
