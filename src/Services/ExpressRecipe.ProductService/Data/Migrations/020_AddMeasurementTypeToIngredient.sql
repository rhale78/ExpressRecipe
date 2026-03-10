-- Migration: 020_AddMeasurementTypeToIngredient
-- Description: Add measurement type and default unit columns to Ingredient and Product tables.
-- Date: 2026-03-09

ALTER TABLE Ingredient
    ADD MeasurementType NVARCHAR(20) NULL,   -- Mass|Volume|Count|MassOrVolume|Uncountable
        DefaultUnit     NVARCHAR(20) NULL;

ALTER TABLE Product
    ADD CanonicalServingSize  DECIMAL(10,4) NULL,
        CanonicalServingUnit  NVARCHAR(5)   NULL,
        PackageCanonicalSize  DECIMAL(10,4) NULL,
        PackageCanonicalUnit  NVARCHAR(5)   NULL;
GO
