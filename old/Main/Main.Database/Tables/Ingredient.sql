CREATE TABLE [dbo].[Ingredient] (
    [ID]                         INT            IDENTITY (1, 1) NOT NULL,
    [Name]                       NVARCHAR (50)  NULL,
    [Description]                NVARCHAR (255) NULL,
    [IngredientTypeID]           INT            NULL,
    [IngredientClassificationID] INT            NULL,
    CONSTRAINT [PK_Ingredient] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Ingredient_IngredientClassification] FOREIGN KEY ([IngredientClassificationID]) REFERENCES [dbo].[IngredientClassification] ([ID]),
    CONSTRAINT [FK_Ingredient_IngredientType] FOREIGN KEY ([IngredientTypeID]) REFERENCES [dbo].[IngredientType] ([ID])
);

