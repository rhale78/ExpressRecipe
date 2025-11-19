-- Migration: 005_EnhancedAllergenTracking
-- Description: Add severity levels, reaction types, and individual ingredient allergies
-- Date: 2024-11-19

-- AllergenReactionType: Types of allergic reactions
CREATE TABLE AllergenReactionType (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    SeverityLevel NVARCHAR(50) NULL, -- Mild, Moderate, Severe, Life-Threatening
    RequiresEpiPen BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_AllergenReactionType_Name UNIQUE (Name)
);

-- Seed common reaction types
INSERT INTO AllergenReactionType (Name, Description, SeverityLevel, RequiresEpiPen) VALUES
('Hives', 'Itchy, raised welts on the skin', 'Mild', 0),
('Itching', 'Generalized itching or tingling sensation', 'Mild', 0),
('Swelling', 'Swelling of face, lips, tongue, or throat', 'Moderate', 0),
('Respiratory Issues', 'Difficulty breathing, wheezing, or shortness of breath', 'Severe', 1),
('Anaphylaxis', 'Severe, life-threatening allergic reaction', 'Life-Threatening', 1),
('Digestive Issues', 'Nausea, vomiting, diarrhea, or abdominal pain', 'Mild', 0),
('Eczema Flare', 'Worsening of existing eczema or skin inflammation', 'Mild', 0),
('Angioedema', 'Deep swelling beneath the skin', 'Moderate', 0),
('Asthma Attack', 'Severe breathing difficulty and chest tightness', 'Severe', 1),
('Drop in Blood Pressure', 'Sudden drop in blood pressure leading to dizziness or fainting', 'Severe', 1),
('Oral Allergy Syndrome', 'Itching or swelling in mouth, lips, tongue, or throat', 'Mild', 0);
GO

-- Add severity and reaction tracking to UserAllergen
ALTER TABLE UserAllergen ADD SeverityLevel NVARCHAR(50) NULL; -- Mild, Moderate, Severe, Life-Threatening
ALTER TABLE UserAllergen ADD RequiresEpiPen BIT NOT NULL DEFAULT 0;
ALTER TABLE UserAllergen ADD OnsetTimeMinutes INT NULL; -- How quickly reaction typically occurs
ALTER TABLE UserAllergen ADD LastReactionDate DATETIME2 NULL;
ALTER TABLE UserAllergen ADD DiagnosedBy NVARCHAR(200) NULL; -- Doctor, Self-reported, Family history, etc.
ALTER TABLE UserAllergen ADD DiagnosisDate DATETIME2 NULL;
ALTER TABLE UserAllergen ADD Notes NVARCHAR(MAX) NULL; -- Additional information about the allergy
GO

-- UserAllergenReaction: Track specific reactions user experiences for each allergen
CREATE TABLE UserAllergenReaction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserAllergenId UNIQUEIDENTIFIER NOT NULL,
    ReactionTypeId UNIQUEIDENTIFIER NOT NULL,
    FrequencyOfOccurrence NVARCHAR(50) NULL, -- Always, Usually, Sometimes, Rarely
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserAllergenReaction_UserAllergen FOREIGN KEY (UserAllergenId)
        REFERENCES UserAllergen(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserAllergenReaction_ReactionType FOREIGN KEY (ReactionTypeId)
        REFERENCES AllergenReactionType(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserAllergenReaction_Allergen_Reaction UNIQUE (UserAllergenId, ReactionTypeId)
);

CREATE INDEX IX_UserAllergenReaction_UserAllergenId ON UserAllergenReaction(UserAllergenId);
CREATE INDEX IX_UserAllergenReaction_ReactionTypeId ON UserAllergenReaction(ReactionTypeId);
GO

-- UserIngredientAllergy: Individual ingredient allergies (not just broad allergen categories)
-- This allows tracking allergies to specific ingredients like "tomatoes" not just "nightshades"
CREATE TABLE UserIngredientAllergy (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NULL, -- References ProductService.Ingredient.Id
    BaseIngredientId UNIQUEIDENTIFIER NULL, -- References ProductService.BaseIngredient.Id
    IngredientName NVARCHAR(200) NULL, -- Free-form if not in database
    SeverityLevel NVARCHAR(50) NOT NULL, -- Mild, Moderate, Severe, Life-Threatening
    RequiresEpiPen BIT NOT NULL DEFAULT 0,
    OnsetTimeMinutes INT NULL,
    LastReactionDate DATETIME2 NULL,
    DiagnosedBy NVARCHAR(200) NULL,
    DiagnosisDate DATETIME2 NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL
);

CREATE INDEX IX_UserIngredientAllergy_UserId ON UserIngredientAllergy(UserId);
CREATE INDEX IX_UserIngredientAllergy_IngredientId ON UserIngredientAllergy(IngredientId);
CREATE INDEX IX_UserIngredientAllergy_BaseIngredientId ON UserIngredientAllergy(BaseIngredientId);
GO

-- UserIngredientAllergyReaction: Reactions for specific ingredient allergies
CREATE TABLE UserIngredientAllergyReaction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserIngredientAllergyId UNIQUEIDENTIFIER NOT NULL,
    ReactionTypeId UNIQUEIDENTIFIER NOT NULL,
    FrequencyOfOccurrence NVARCHAR(50) NULL,
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserIngredientAllergyReaction_Allergy FOREIGN KEY (UserIngredientAllergyId)
        REFERENCES UserIngredientAllergy(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserIngredientAllergyReaction_ReactionType FOREIGN KEY (ReactionTypeId)
        REFERENCES AllergenReactionType(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserIngredientAllergyReaction_Allergy_Reaction UNIQUE (UserIngredientAllergyId, ReactionTypeId)
);

CREATE INDEX IX_UserIngredientAllergyReaction_AllergyId ON UserIngredientAllergyReaction(UserIngredientAllergyId);
CREATE INDEX IX_UserIngredientAllergyReaction_ReactionTypeId ON UserIngredientAllergyReaction(ReactionTypeId);
GO

-- AllergyIncident: Track actual allergic reactions that occurred
CREATE TABLE AllergyIncident (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    UserAllergenId UNIQUEIDENTIFIER NULL,
    UserIngredientAllergyId UNIQUEIDENTIFIER NULL,
    IncidentDate DATETIME2 NOT NULL,
    Location NVARCHAR(500) NULL,
    ProductId UNIQUEIDENTIFIER NULL, -- If known which product caused it
    RestaurantId UNIQUEIDENTIFIER NULL, -- If occurred at restaurant
    MenuItemId UNIQUEIDENTIFIER NULL, -- Specific menu item
    RecipeId UNIQUEIDENTIFIER NULL, -- If from a recipe
    SeverityLevel NVARCHAR(50) NOT NULL,
    RequiredEpiPen BIT NOT NULL DEFAULT 0,
    RequiredHospitalization BIT NOT NULL DEFAULT 0,
    OnsetTimeMinutes INT NULL,
    Duration NVARCHAR(100) NULL, -- How long reaction lasted
    TreatmentReceived NVARCHAR(MAX) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_AllergyIncident_UserAllergen FOREIGN KEY (UserAllergenId)
        REFERENCES UserAllergen(Id) ON DELETE SET NULL,
    CONSTRAINT FK_AllergyIncident_UserIngredientAllergy FOREIGN KEY (UserIngredientAllergyId)
        REFERENCES UserIngredientAllergy(Id) ON DELETE SET NULL
);

CREATE INDEX IX_AllergyIncident_UserId ON AllergyIncident(UserId);
CREATE INDEX IX_AllergyIncident_IncidentDate ON AllergyIncident(IncidentDate);
CREATE INDEX IX_AllergyIncident_ProductId ON AllergyIncident(ProductId);
GO
