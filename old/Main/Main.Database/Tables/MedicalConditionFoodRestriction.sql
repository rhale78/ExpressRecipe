CREATE TABLE [dbo].[MedicalConditionFoodRestriction] (
    [ID]                 INT IDENTITY (1, 1) NOT NULL,
    [MedicalConditionID] INT NULL,
    [FoodRestrictionID]  INT NULL,
    CONSTRAINT [PK_MedicalConditionFoodRestriction] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_MedicalConditionFoodRestriction_FoodRestriction] FOREIGN KEY ([FoodRestrictionID]) REFERENCES [dbo].[FoodRestriction] ([ID]),
    CONSTRAINT [FK_MedicalConditionFoodRestriction_MedicalCondition] FOREIGN KEY ([MedicalConditionID]) REFERENCES [dbo].[MedicalCondition] ([ID])
);

