CREATE TABLE [dbo].[IngredientsRawData] (
    [ID]                   INT            IDENTITY (1, 1) NOT NULL,
    [IngredientsData]      NVARCHAR (MAX) NULL,
    [CreatedUpdatedDataID] INT            NULL,
    [IngredientID]         INT            NULL,
    CONSTRAINT [PK_IngredientsRawData] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_IngredientsRawData_CreatedUpdatedData] FOREIGN KEY ([CreatedUpdatedDataID]) REFERENCES [dbo].[CreatedUpdatedData] ([ID]),
    CONSTRAINT [FK_IngredientsRawData_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID])
);

