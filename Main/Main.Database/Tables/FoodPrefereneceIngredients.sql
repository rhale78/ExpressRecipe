CREATE TABLE [dbo].[FoodPrefereneceIngredients] (
    [ID]               INT IDENTITY (1, 1) NOT NULL,
    [FoodPreferenceID] INT NULL,
    [IngredientID]     INT NULL,
    CONSTRAINT [PK_FoodPrefereneceIngredients] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FoodPrefereneceIngredients_FoodPreference] FOREIGN KEY ([FoodPreferenceID]) REFERENCES [dbo].[FoodPreference] ([ID]),
    CONSTRAINT [FK_FoodPrefereneceIngredients_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID])
);

