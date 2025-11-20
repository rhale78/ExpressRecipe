CREATE TABLE [dbo].[PersonFoodRestriction] (
    [ID]                INT IDENTITY (1, 1) NOT NULL,
    [PersonID]          INT NULL,
    [FoodRestrictionID] INT NULL,
    CONSTRAINT [PK_PersonFoodRestriction] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonFoodRestriction_FoodRestriction] FOREIGN KEY ([FoodRestrictionID]) REFERENCES [dbo].[FoodRestriction] ([ID]),
    CONSTRAINT [FK_PersonFoodRestriction_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID])
);

