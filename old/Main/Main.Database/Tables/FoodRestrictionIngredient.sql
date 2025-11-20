CREATE TABLE [dbo].[FoodRestrictionIngredient] (
    [ID]                INT IDENTITY (1, 1) NOT NULL,
    [FoodRestrictionID] INT NULL,
    [IngredientID]      INT NULL,
    CONSTRAINT [PK_FoodRestrictionIngredient] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FoodRestrictionIngredient_FoodRestriction] FOREIGN KEY ([FoodRestrictionID]) REFERENCES [dbo].[FoodRestriction] ([ID]),
    CONSTRAINT [FK_FoodRestrictionIngredient_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID])
);

