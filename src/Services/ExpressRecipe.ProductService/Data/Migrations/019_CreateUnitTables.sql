-- Migration: 019_CreateUnitTables
-- Description: Create UnitDefinition and IngredientUnitDensity tables with seed data.
-- Date: 2026-03-09

CREATE TABLE UnitDefinition (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Code            NVARCHAR(20)  NOT NULL,
    DisplayName     NVARCHAR(100) NOT NULL,
    Dimension       NVARCHAR(20)  NOT NULL,
    CanonicalFactor DECIMAL(18,10) NOT NULL,
    CanonicalUnit   NVARCHAR(5)   NOT NULL,
    IsCanonical     BIT NOT NULL DEFAULT 0,
    UsSystem        BIT NOT NULL DEFAULT 0,
    MetricSystem    BIT NOT NULL DEFAULT 0,
    UkSystem        BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_UnitDefinition_Code UNIQUE (Code)
);
GO

CREATE TABLE IngredientUnitDensity (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IngredientId    UNIQUEIDENTIFIER NULL,
    IngredientName  NVARCHAR(200) NOT NULL,
    PreparationNote NVARCHAR(100) NULL,
    GramsPerMl      DECIMAL(10,6) NOT NULL,
    Source          NVARCHAR(50)  NOT NULL DEFAULT 'USDA',
    UsdaFdcId       INT NULL,
    IsVerified      BIT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NULL,
    CONSTRAINT UQ_IngredientDensity UNIQUE (IngredientId, PreparationNote)
);
CREATE INDEX IX_IngredientDensity_Name         ON IngredientUnitDensity(IngredientName);
CREATE INDEX IX_IngredientDensity_IngredientId ON IngredientUnitDensity(IngredientId);
GO

-- Seed UnitDefinition (19 rows)
INSERT INTO UnitDefinition (Id,Code,DisplayName,Dimension,CanonicalFactor,CanonicalUnit,IsCanonical,UsSystem,MetricSystem,UkSystem) VALUES
(NEWID(),'g',      'Gram',           'Mass',        1.0,         'g',  1,0,1,1),
(NEWID(),'kg',     'Kilogram',       'Mass',        1000.0,      'g',  0,0,1,1),
(NEWID(),'mg',     'Milligram',      'Mass',        0.001,       'g',  0,0,1,1),
(NEWID(),'oz',     'Ounce',          'Mass',        28.34952,    'g',  0,1,0,0),
(NEWID(),'lb',     'Pound',          'Mass',        453.59237,   'g',  0,1,0,0),
(NEWID(),'ml',     'Milliliter',     'Volume',      1.0,         'ml', 1,0,1,1),
(NEWID(),'L',      'Liter',          'Volume',      1000.0,      'ml', 0,0,1,1),
(NEWID(),'tsp',    'Teaspoon',       'Volume',      4.92892,     'ml', 0,1,0,0),
(NEWID(),'uktsp',  'UK Teaspoon',    'Volume',      5.0,         'ml', 0,0,0,1),
(NEWID(),'tbsp',   'Tablespoon',     'Volume',      14.78676,    'ml', 0,1,0,0),
(NEWID(),'uktbsp', 'UK Tablespoon',  'Volume',      15.0,        'ml', 0,0,0,1),
(NEWID(),'floz',   'Fluid Ounce',    'Volume',      29.57353,    'ml', 0,1,0,0),
(NEWID(),'cup',    'US Cup',         'Volume',      236.58824,   'ml', 0,1,0,0),
(NEWID(),'ukcup',  'UK Cup',         'Volume',      284.13063,   'ml', 0,0,0,1),
(NEWID(),'mcup',   'Metric Cup',     'Volume',      250.0,       'ml', 0,0,1,0),
(NEWID(),'C',      'Celsius',        'Temperature', 1.0,         'C',  1,0,1,1),
(NEWID(),'F',      'Fahrenheit',     'Temperature', 0.0,         'C',  0,1,0,0),
(NEWID(),'GM',     'Gas Mark',       'Temperature', 0.0,         'C',  0,0,0,1),
(NEWID(),'ea',     'Each',           'Count',       1.0,         'ea', 1,1,1,1);
GO

-- Seed IngredientUnitDensity (common ingredients)
INSERT INTO IngredientUnitDensity (Id,IngredientName,PreparationNote,GramsPerMl,Source,IsVerified) VALUES
(NEWID(),'all-purpose flour','sifted',   0.5292,'USDA',1),
(NEWID(),'all-purpose flour','packed',   0.6000,'USDA',1),
(NEWID(),'all-purpose flour',NULL,       0.5292,'USDA',1),
(NEWID(),'bread flour',NULL,             0.5613,'USDA',1),
(NEWID(),'cake flour','sifted',          0.4746,'USDA',1),
(NEWID(),'whole wheat flour',NULL,       0.5547,'USDA',1),
(NEWID(),'almond flour',NULL,            0.3840,'USDA',1),
(NEWID(),'granulated sugar',NULL,        0.8453,'USDA',1),
(NEWID(),'powdered sugar','sifted',      0.5600,'USDA',1),
(NEWID(),'brown sugar','packed',         0.9310,'USDA',1),
(NEWID(),'butter','room temperature',    0.9110,'USDA',1),
(NEWID(),'butter','melted',              0.8680,'USDA',1),
(NEWID(),'olive oil',NULL,               0.9110,'Manual',1),
(NEWID(),'honey',NULL,                   1.4200,'Manual',1),
(NEWID(),'whole milk',NULL,              1.0350,'USDA',1),
(NEWID(),'water',NULL,                   1.0000,'Manual',1),
(NEWID(),'kosher salt',NULL,             1.2170,'Manual',1),
(NEWID(),'baking soda',NULL,             0.9600,'Manual',1),
(NEWID(),'baking powder',NULL,           0.9000,'Manual',1),
(NEWID(),'cocoa powder',NULL,            0.4233,'USDA',1),
(NEWID(),'rolled oats',NULL,             0.4100,'USDA',1),
(NEWID(),'rice','uncooked',              0.7533,'USDA',1),
(NEWID(),'peanut butter',NULL,           1.0820,'USDA',1),
(NEWID(),'breadcrumbs',NULL,             0.3730,'USDA',1);
GO
