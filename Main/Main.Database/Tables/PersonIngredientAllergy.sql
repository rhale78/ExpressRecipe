CREATE TABLE [dbo].[PersonIngredientAllergy] (
    [ID]                         INT IDENTITY (1, 1) NOT NULL,
    [PersonID]                   INT NULL,
    [IngredientID]               INT NULL,
    [AllergySeverityProxReactID] INT NULL,
    CONSTRAINT [PK_PersonIngredientAllergy] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonIngredientAllergy_AllergySeverityProxReact] FOREIGN KEY ([AllergySeverityProxReactID]) REFERENCES [dbo].[AllergySeverityProxReact] ([ID]),
    CONSTRAINT [FK_PersonIngredientAllergy_Ingredient] FOREIGN KEY ([IngredientID]) REFERENCES [dbo].[Ingredient] ([ID]),
    CONSTRAINT [FK_PersonIngredientAllergy_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID])
);

