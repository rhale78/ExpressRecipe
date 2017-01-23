CREATE TABLE [dbo].[IngredientToSubIngredient] (
    [ID]              INT IDENTITY (1, 1) NOT NULL,
    [IngredientID]    INT NULL,
    [SubIngredientID] INT NULL,
    [OrderIndex]      INT NULL,
    [IsMayContain]    BIT NULL,
    [IsAndOr]         BIT NULL,
    CONSTRAINT [PK_IngredientToSubIngredient] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_IngredientToSubIngredient_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID]),
    CONSTRAINT [FK_IngredientToSubIngredient_Ingredient1] FOREIGN KEY ([SubIngredientID]) REFERENCES [dbo].[Ingredient] ([ID])
);

