CREATE TABLE [dbo].[ProductIngredient] (
    [ID]                       INT            IDENTITY (1, 1) NOT NULL,
    [ProductInstanceID]        INT            NULL,
    [IngredientID]             INT            NULL,
    [IngredientIndex]          INT            NULL,
    [AdditionalIngredientText] NVARCHAR (255) NULL,
    CONSTRAINT [PK_ProductIngredient] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ProductIngredient_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID]),
    CONSTRAINT [FK_ProductIngredient_ProductInstance] FOREIGN KEY ([ProductInstanceID]) REFERENCES [dbo].[ProductInstance] ([ID])
);

