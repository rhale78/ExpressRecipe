CREATE TABLE [dbo].[ProductWarningTypeIngredients] (
    [ID]                   INT IDENTITY (1, 1) NOT NULL,
    [ProductWarningTypeID] INT NULL,
    [IngredientID]         INT NULL,
    CONSTRAINT [PK_ProductWarningTypeIngredients] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ProductWarningTypeIngredients_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID]),
    CONSTRAINT [FK_ProductWarningTypeIngredients_ProductWarningType] FOREIGN KEY ([ProductWarningTypeID]) REFERENCES [dbo].[ProductWarningType] ([ID])
);

