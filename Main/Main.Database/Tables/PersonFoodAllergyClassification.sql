CREATE TABLE [dbo].[PersonFoodAllergyClassification] (
    [ID]                          INT IDENTITY (1, 1) NOT NULL,
    [PersonID]                    INT NULL,
    [FoodAllergyClassificationID] INT NULL,
    [AllergySeverityProxReactID]  INT NULL,
    CONSTRAINT [PK_PersonFoodAllergyClassification] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonFoodAllergyClassification_AllergySeverityProxReact] FOREIGN KEY ([AllergySeverityProxReactID]) REFERENCES [dbo].[AllergySeverityProxReact] ([ID]),
    CONSTRAINT [FK_PersonFoodAllergyClassification_FoodAllergyClassification] FOREIGN KEY ([FoodAllergyClassificationID]) REFERENCES [dbo].[FoodAllergyClassification] ([ID]),
    CONSTRAINT [FK_PersonFoodAllergyClassification_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID])
);

