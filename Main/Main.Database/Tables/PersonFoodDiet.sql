CREATE TABLE [dbo].[PersonFoodDiet] (
    [ID]         INT IDENTITY (1, 1) NOT NULL,
    [PersonID]   INT NULL,
    [FoodDietID] INT NULL,
    CONSTRAINT [PK_PersonFoodDiet] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonFoodDiet_FoodDiet] FOREIGN KEY ([FoodDietID]) REFERENCES [dbo].[FoodDiet] ([ID]),
    CONSTRAINT [FK_PersonFoodDiet_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID])
);

