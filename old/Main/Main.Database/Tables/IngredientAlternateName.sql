CREATE TABLE [dbo].[IngredientAlternateName] (
    [ID]           INT            IDENTITY (1, 1) NOT NULL,
    [IngredientID] INT            NULL,
    [Name]         NVARCHAR (50)  NULL,
    [Description]  NVARCHAR (255) NULL,
    CONSTRAINT [PK_IngredientAlternateName] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_IngredientAlternateName_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID])
);

