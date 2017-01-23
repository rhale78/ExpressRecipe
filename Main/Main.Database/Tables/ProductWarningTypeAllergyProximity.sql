CREATE TABLE [dbo].[ProductWarningTypeAllergyProximity] (
    [ID]                   INT IDENTITY (1, 1) NOT NULL,
    [ProductWarningTypeID] INT NULL,
    [AllergyProximityID]   INT NULL,
    CONSTRAINT [PK_ProductWarningTypeAllergyProximity] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ProductWarningTypeAllergyProximity_AllergyProximityType] FOREIGN KEY ([AllergyProximityID]) REFERENCES [dbo].[AllergyProximityType] ([ID]),
    CONSTRAINT [FK_ProductWarningTypeAllergyProximity_ProductWarningType] FOREIGN KEY ([ProductWarningTypeID]) REFERENCES [dbo].[ProductWarningType] ([ID])
);

