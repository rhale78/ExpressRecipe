CREATE TABLE [dbo].[IngredientToFoodAllergyClassification] (
    [ID]                          INT IDENTITY (1, 1) NOT NULL,
    [IngredientID]                INT NULL,
    [FoodAllergyClassificaitonID] INT NULL,
    CONSTRAINT [PK_IngredientToFoodAllergyClassification] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_IngredientToFoodAllergyClassification_FoodAllergyClassification] FOREIGN KEY ([FoodAllergyClassificaitonID]) REFERENCES [dbo].[FoodAllergyClassification] ([ID]),
    CONSTRAINT [FK_IngredientToFoodAllergyClassification_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID])
);

