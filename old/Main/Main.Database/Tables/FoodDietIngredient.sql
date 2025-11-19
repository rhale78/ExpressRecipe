CREATE TABLE [dbo].[FoodDietIngredient] (
    [ID]            INT IDENTITY (1, 1) NOT NULL,
    [FoodDietID]    INT NULL,
    [IngredientID]  INT NULL,
    [ConsumeTypeID] INT NULL,
    CONSTRAINT [PK_FoodDietIngredient] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FoodDietIngredient_FoodConsumeType] FOREIGN KEY ([ConsumeTypeID]) REFERENCES [dbo].[FoodConsumeType] ([ID]),
    CONSTRAINT [FK_FoodDietIngredient_FoodDiet] FOREIGN KEY ([FoodDietID]) REFERENCES [dbo].[FoodDiet] ([ID]),
    CONSTRAINT [FK_FoodDietIngredient_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID])
);

