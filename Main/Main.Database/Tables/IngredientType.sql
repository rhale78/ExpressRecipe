CREATE TABLE [dbo].[IngredientType] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (25)  NULL,
    [Description] NVARCHAR (255) NULL,
    CONSTRAINT [PK_IngredientType] PRIMARY KEY CLUSTERED ([ID] ASC)
);

