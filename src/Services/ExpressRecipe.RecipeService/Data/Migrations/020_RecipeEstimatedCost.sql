-- Migration: 016_RecipeEstimatedCost
-- Description: Add estimated cost per serving tracking to Recipe table
-- Date: 2026-03-09

ALTER TABLE Recipe ADD EstimatedCostPerServing DECIMAL(10,4) NULL;
ALTER TABLE Recipe ADD CostLastCalculatedAt    DATETIME2 NULL;
CREATE INDEX IX_Recipe_EstimatedCost ON Recipe(EstimatedCostPerServing) WHERE EstimatedCostPerServing IS NOT NULL;
GO
