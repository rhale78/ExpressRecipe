CREATE TABLE [dbo].[IngredientClassification] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (25)  NULL,
    [Description] NVARCHAR (255) NULL,
    CONSTRAINT [PK_IngredientClassification] PRIMARY KEY CLUSTERED ([ID] ASC)
);

